using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.AI.Export.Storage;

/// <summary>
/// Configuration for <see cref="FilesystemExportStorageService"/>. Bind via
/// <c>Configuration.GetSection("AiExport:Storage")</c>.
/// </summary>
public sealed class ExportStorageOptions
{
    /// <summary>Absolute or workspace-relative root directory where exports are stored.</summary>
    public string RootDirectory { get; set; } = "App_Data/exports";

    /// <summary>Override the default retention; when null, falls back to <see cref="PowerPointDeckLimits.StorageRetention"/>.</summary>
    public TimeSpan? RetentionOverride { get; set; }

    /// <summary>Override the default download link TTL.</summary>
    public TimeSpan? DownloadLinkTtlOverride { get; set; }
}

/// <summary>
/// Filesystem-based implementation of <see cref="IExportStorageService"/>. Stores .pptx files
/// in <c>{root}/{tenantId}/{exportId}.pptx</c> alongside a small JSON manifest used by the
/// cleanup job. Download URLs are protected by short-lived signed tokens (ASP.NET Data Protection).
/// </summary>
public sealed class FilesystemExportStorageService : IExportStorageService
{
    private const string TokenPurpose = "FactuTrust.AiExport.DownloadToken.v1";
    private const string ManifestExtension = ".manifest.json";
    private const string PptxContentType = "application/vnd.openxmlformats-officedocument.presentationml.presentation";

    private readonly ITenantContext _tenantContext;
    private readonly IDataProtectionProvider _dataProtection;
    private readonly ExportStorageOptions _options;
    private readonly ILogger<FilesystemExportStorageService> _logger;
    private readonly IDataProtector _protector;

    public FilesystemExportStorageService(
        ITenantContext tenantContext,
        IDataProtectionProvider dataProtection,
        IOptions<ExportStorageOptions> options,
        ILogger<FilesystemExportStorageService> logger)
    {
        _tenantContext = tenantContext;
        _dataProtection = dataProtection;
        _options = options.Value;
        _logger = logger;
        _protector = _dataProtection.CreateProtector(TokenPurpose);
    }

    public async Task<StoredExport> StoreAsync(
        Guid tenantId,
        Guid userId,
        string format,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant id must not be empty.", nameof(tenantId));
        if (userId == Guid.Empty) throw new ArgumentException("User id must not be empty.", nameof(userId));
        if (content.Length == 0) throw new ArgumentException("Cannot store empty export content.", nameof(content));

        var exportId = Guid.NewGuid();
        var tenantFolder = ResolveTenantFolder(tenantId);
        Directory.CreateDirectory(tenantFolder);

        var safeFileName = SanitizeFileName(fileName);
        var binaryPath = Path.Combine(tenantFolder, $"{exportId:N}.bin");
        var manifestPath = Path.Combine(tenantFolder, $"{exportId:N}{ManifestExtension}");

        await File.WriteAllBytesAsync(binaryPath, content, cancellationToken);

        var ttl = _options.DownloadLinkTtlOverride ?? PowerPointDeckLimits.DownloadLinkTtl;
        var expiresAt = DateTime.UtcNow.Add(ttl);

        var manifest = new ExportManifest
        {
            ExportId = exportId,
            TenantId = tenantId,
            UserId = userId,
            Format = format,
            FileName = safeFileName,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(_options.RetentionOverride ?? PowerPointDeckLimits.StorageRetention),
            SizeBytes = content.LongLength,
            ContentType = ResolveContentType(format)
        };

        var manifestJson = JsonSerializer.Serialize(manifest, ManifestJsonOptions);
        await File.WriteAllTextAsync(manifestPath, manifestJson, cancellationToken);

        var token = ProtectToken(exportId, tenantId, userId, expiresAt);

        _logger.LogInformation(
            "Stored AI export {ExportId} ({Format}, {SizeBytes} bytes) for tenant {TenantId} / user {UserId}",
            exportId,
            format,
            content.LongLength,
            tenantId,
            userId);

        return new StoredExport
        {
            ExportId = exportId,
            FileName = safeFileName,
            Token = token,
            ExpiresAt = expiresAt,
            StoragePath = binaryPath,
            SizeBytes = content.LongLength
        };
    }

    public async Task<StoredExportContent?> ResolveAsync(Guid exportId, string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        if (!TryUnprotect(token, out var payload))
        {
            _logger.LogWarning("Rejected AI export download — invalid signature for export {ExportId}", exportId);
            return null;
        }
        if (payload.ExportId != exportId)
        {
            _logger.LogWarning("Rejected AI export download — export id mismatch for export {ExportId}", exportId);
            return null;
        }
        if (payload.ExpiresAt <= DateTime.UtcNow)
        {
            _logger.LogInformation("Rejected AI export download — token expired for export {ExportId}", exportId);
            return null;
        }

        var currentTenant = _tenantContext.TenantId;
        if (currentTenant.HasValue && currentTenant.Value != payload.TenantId)
        {
            _logger.LogWarning(
                "Rejected AI export download — tenant mismatch (requested {Requested}, current {Current})",
                payload.TenantId,
                currentTenant);
            return null;
        }

        var tenantFolder = ResolveTenantFolder(payload.TenantId);
        var binaryPath = Path.Combine(tenantFolder, $"{exportId:N}.bin");
        var manifestPath = Path.Combine(tenantFolder, $"{exportId:N}{ManifestExtension}");

        if (!File.Exists(binaryPath) || !File.Exists(manifestPath))
        {
            _logger.LogInformation("AI export {ExportId} not found on disk (already cleaned up?)", exportId);
            return null;
        }

        ExportManifest? manifest;
        try
        {
            var manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            manifest = JsonSerializer.Deserialize<ExportManifest>(manifestJson, ManifestJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read manifest for AI export {ExportId}", exportId);
            return null;
        }

        if (manifest is null || manifest.ExpiresAt <= DateTime.UtcNow)
        {
            return null;
        }

        var content = await File.ReadAllBytesAsync(binaryPath, cancellationToken);

        return new StoredExportContent
        {
            ExportId = exportId,
            FileName = manifest.FileName,
            ContentType = manifest.ContentType ?? ResolveContentType(manifest.Format),
            Content = content
        };
    }

    public Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        var root = ResolveRoot();
        if (!Directory.Exists(root))
            return Task.FromResult(0);

        var now = DateTime.UtcNow;
        var deleted = 0;

        foreach (var tenantDir in Directory.EnumerateDirectories(root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var manifestPath in Directory.EnumerateFiles(tenantDir, $"*{ManifestExtension}"))
            {
                try
                {
                    var manifestJson = File.ReadAllText(manifestPath);
                    var manifest = JsonSerializer.Deserialize<ExportManifest>(manifestJson, ManifestJsonOptions);
                    if (manifest is null) continue;

                    if (manifest.ExpiresAt > now)
                        continue;

                    var binaryPath = Path.Combine(tenantDir, $"{manifest.ExportId:N}.bin");
                    if (File.Exists(binaryPath)) File.Delete(binaryPath);
                    if (File.Exists(manifestPath)) File.Delete(manifestPath);
                    deleted++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup AI export manifest {ManifestPath}", manifestPath);
                }
            }

            try
            {
                if (Directory.Exists(tenantDir) && !Directory.EnumerateFileSystemEntries(tenantDir).Any())
                    Directory.Delete(tenantDir);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Unable to remove empty tenant export folder {Folder}", tenantDir);
            }
        }

        if (deleted > 0)
            _logger.LogInformation("AI export cleanup removed {Count} expired files.", deleted);

        return Task.FromResult(deleted);
    }

    private string ResolveTenantFolder(Guid tenantId)
        => Path.Combine(ResolveRoot(), tenantId.ToString("N"));

    private string ResolveRoot()
    {
        var raw = string.IsNullOrWhiteSpace(_options.RootDirectory) ? "App_Data/exports" : _options.RootDirectory;
        return Path.IsPathRooted(raw) ? raw : Path.Combine(AppContext.BaseDirectory, raw);
    }

    private string ProtectToken(Guid exportId, Guid tenantId, Guid userId, DateTime expiresAt)
    {
        var payload = new TokenPayload
        {
            ExportId = exportId,
            TenantId = tenantId,
            UserId = userId,
            ExpiresAt = expiresAt
        };
        var json = JsonSerializer.Serialize(payload, ManifestJsonOptions);
        return _protector.Protect(json);
    }

    private bool TryUnprotect(string token, out TokenPayload payload)
    {
        payload = default!;
        try
        {
            var json = _protector.Unprotect(token);
            payload = JsonSerializer.Deserialize<TokenPayload>(json, ManifestJsonOptions) ?? default!;
            return payload is not null;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new StringBuilder(fileName.Length);
        foreach (var c in fileName)
            sanitized.Append(Array.IndexOf(invalidChars, c) >= 0 ? '_' : c);
        var result = sanitized.ToString().Trim('.', ' ');
        if (string.IsNullOrEmpty(result))
            result = "export.pptx";
        if (result.Length > 200)
            result = result[..200];
        return result;
    }

    private static string ResolveContentType(string format) => format.ToLowerInvariant() switch
    {
        "pptx" or "powerpoint" => PptxContentType,
        _ => "application/octet-stream"
    };

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private sealed class ExportManifest
    {
        public Guid ExportId { get; set; }
        public Guid TenantId { get; set; }
        public Guid UserId { get; set; }
        public string Format { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public long SizeBytes { get; set; }
        public string? ContentType { get; set; }
    }

    private sealed record TokenPayload
    {
        public Guid ExportId { get; init; }
        public Guid TenantId { get; init; }
        public Guid UserId { get; init; }
        public DateTime ExpiresAt { get; init; }
    }
}
