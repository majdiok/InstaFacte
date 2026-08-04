using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.AI;

/// <summary>
/// Reads / writes the platform-wide AI model setting. The table
/// <c>PlatformAiSettings</c> holds a single row, created on first use.
/// </summary>
public sealed class PlatformAiSettingsService : IPlatformAiSettingsService
{
    public const string DataProtectionPurpose = "PlatformAiProviderSecrets";

    private const string CacheKeyDefaultModel = "platform:ai:default-model";
    private const string CacheKeyImportModel = "platform:ai:import-model";
    private const string CacheKeyStudioModel = "platform:ai:studio-model";
    private const string CacheKeyInferenceDevice = "platform:ai:inference-device";
    private const string CacheKeyOpenRouter = "platform:ai:openrouter";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly MasterDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IDataProtector _protector;
    private readonly OpenRouterSettings _openRouterDefaults;
    private readonly ILogger<PlatformAiSettingsService> _logger;

    public PlatformAiSettingsService(
        MasterDbContext db,
        IMemoryCache cache,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<OpenRouterSettings> openRouterSettings,
        ILogger<PlatformAiSettingsService> logger)
    {
        _db = db;
        _cache = cache;
        _protector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);
        _openRouterDefaults = openRouterSettings.Value;
        _logger = logger;
    }

    public Task<string?> GetDefaultModelRefAsync(CancellationToken cancellationToken = default) =>
        _cache.GetOrCreateAsync(CacheKeyDefaultModel, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await _db.PlatformAiSettings
                .AsNoTracking()
                .Select(s => s.DefaultModelRef)
                .FirstOrDefaultAsync(cancellationToken);
        });

    public async Task<string?> SetDefaultModelRefAsync(
        string? modelRef,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var normalized = string.IsNullOrWhiteSpace(modelRef)
            ? null
            : ModelRef.NormalizeStored(modelRef);

        var current = await GetOrCreateRowAsync(actorUserId, cancellationToken);
        current.SetDefaultModel(normalized);
        current.SetAuditInfo(actorUserId.ToString(), isUpdate: true);

        await _db.SaveChangesAsync(cancellationToken);
        InvalidateReadCache();
        return current.DefaultModelRef;
    }

    public Task<string?> GetInvoiceImportModelRefAsync(CancellationToken cancellationToken = default) =>
        _cache.GetOrCreateAsync(CacheKeyImportModel, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            try
            {
                return await _db.PlatformAiSettings
                    .AsNoTracking()
                    .Select(s => s.InvoiceImportModelRef)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is SqlException or DbUpdateException or InvalidOperationException)
            {
                _logger.LogWarning(ex,
                    "Impossible de lire InvoiceImportModelRef depuis PlatformAiSettings ; fallback configuration serveur.");
                return null;
            }
        });

    public async Task<string?> SetInvoiceImportModelRefAsync(
        string? modelRef,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var normalized = string.IsNullOrWhiteSpace(modelRef)
            ? null
            : ModelRef.NormalizeStored(modelRef);

        var current = await GetOrCreateRowAsync(actorUserId, cancellationToken);
        current.SetInvoiceImportModel(normalized);
        current.SetAuditInfo(actorUserId.ToString(), isUpdate: true);

        await _db.SaveChangesAsync(cancellationToken);
        InvalidateReadCache();
        return current.InvoiceImportModelRef;
    }

    public Task<string?> GetStudioAiModelRefAsync(CancellationToken cancellationToken = default) =>
        _cache.GetOrCreateAsync(CacheKeyStudioModel, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            try
            {
                return await _db.PlatformAiSettings
                    .AsNoTracking()
                    .Select(s => s.StudioAiModelRef)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is SqlException or DbUpdateException or InvalidOperationException)
            {
                _logger.LogWarning(ex,
                    "Impossible de lire StudioAiModelRef depuis PlatformAiSettings ; fallback modèle Assistant.");
                return null;
            }
        });

    public async Task<string?> SetStudioAiModelRefAsync(
        string? modelRef,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var normalized = string.IsNullOrWhiteSpace(modelRef)
            ? null
            : ModelRef.NormalizeStored(modelRef);

        var current = await GetOrCreateRowAsync(actorUserId, cancellationToken);
        current.SetStudioAiModel(normalized);
        current.SetAuditInfo(actorUserId.ToString(), isUpdate: true);

        await _db.SaveChangesAsync(cancellationToken);
        InvalidateReadCache();
        return current.StudioAiModelRef;
    }

    public Task<OllamaInferenceDevice> GetInferenceDeviceAsync(CancellationToken cancellationToken = default) =>
        _cache.GetOrCreateAsync(CacheKeyInferenceDevice, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            try
            {
                var device = await _db.PlatformAiSettings
                    .AsNoTracking()
                    .Select(s => (OllamaInferenceDevice?)s.InferenceDevice)
                    .FirstOrDefaultAsync(cancellationToken);

                return device ?? OllamaInferenceDevice.Gpu;
            }
            catch (Exception ex) when (ex is SqlException or DbUpdateException or InvalidOperationException)
            {
                _logger.LogWarning(ex,
                    "Impossible de lire InferenceDevice depuis PlatformAiSettings ; fallback GPU.");
                return OllamaInferenceDevice.Gpu;
            }
        });

    public async Task<OllamaInferenceDevice> SetInferenceDeviceAsync(
        OllamaInferenceDevice device,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(device))
            throw new ArgumentOutOfRangeException(nameof(device), device, "Valeur InferenceDevice invalide.");

        var current = await GetOrCreateRowAsync(actorUserId, cancellationToken);
        current.SetInferenceDevice(device);
        current.SetAuditInfo(actorUserId.ToString(), isUpdate: true);

        await _db.SaveChangesAsync(cancellationToken);
        InvalidateReadCache();
        return current.InferenceDevice;
    }

    public async Task<PlatformOpenRouterSettingsDto> GetOpenRouterSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetOrCreateAsync(CacheKeyOpenRouter, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            try
            {
                return await _db.PlatformAiSettings
                    .AsNoTracking()
                    .Select(s => new OpenRouterCacheRow(
                        s.OpenRouterIsEnabled,
                        s.OpenRouterDisplayName,
                        s.OpenRouterBaseUrl,
                        s.OpenRouterEncryptedApiKey,
                        s.OpenRouterApiKeyLast4))
                    .FirstOrDefaultAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is SqlException or DbUpdateException or InvalidOperationException)
            {
                _logger.LogWarning(ex,
                    "Impossible de lire les credentials OpenRouter depuis PlatformAiSettings.");
                return null;
            }
        });

        return MapOpenRouterDto(cached);
    }

    public async Task<(bool Success, string? Error)> SetOpenRouterConfigAsync(
        bool isEnabled,
        string? displayName,
        string? baseUrl,
        string? apiKey,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var current = await GetOrCreateRowAsync(actorUserId, cancellationToken);

        string? encrypted = null;
        string? last4 = null;
        var hasNewKey = !string.IsNullOrWhiteSpace(apiKey);
        if (hasNewKey)
        {
            var plain = apiKey!.Trim();
            encrypted = _protector.Protect(plain);
            last4 = plain.Length >= 4 ? plain[^4..] : plain;
        }
        else if (isEnabled && !current.HasOpenRouterApiKey)
        {
            return (false, "Une clé API est requise pour activer OpenRouter.");
        }

        current.SetOpenRouterConfig(
            isEnabled,
            displayName,
            baseUrl,
            hasNewKey ? encrypted : null,
            hasNewKey ? last4 : null);
        current.SetAuditInfo(actorUserId.ToString(), isUpdate: true);

        await _db.SaveChangesAsync(cancellationToken);
        InvalidateReadCache();
        return (true, null);
    }

    public async Task<PlatformOpenRouterCredentials> GetOpenRouterCredentialsAsync(
        CancellationToken cancellationToken = default)
    {
        var defaultBase = (_openRouterDefaults.DefaultBaseUrl ?? "https://openrouter.ai/api/v1").TrimEnd('/');

        OpenRouterCacheRow? row;
        try
        {
            row = await _db.PlatformAiSettings
                .AsNoTracking()
                .Select(s => new OpenRouterCacheRow(
                    s.OpenRouterIsEnabled,
                    s.OpenRouterDisplayName,
                    s.OpenRouterBaseUrl,
                    s.OpenRouterEncryptedApiKey,
                    s.OpenRouterApiKeyLast4))
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is SqlException or DbUpdateException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Impossible de déchiffrer les credentials OpenRouter plateforme.");
            return new PlatformOpenRouterCredentials(false, null, defaultBase);
        }

        var baseUrl = !string.IsNullOrWhiteSpace(row?.BaseUrl)
            ? row!.BaseUrl!.TrimEnd('/')
            : defaultBase;

        if (row is null || !row.IsEnabled || string.IsNullOrWhiteSpace(row.EncryptedApiKey))
            return new PlatformOpenRouterCredentials(row?.IsEnabled ?? false, null, baseUrl);

        try
        {
            var plain = _protector.Unprotect(row.EncryptedApiKey);
            if (string.IsNullOrWhiteSpace(plain))
                return new PlatformOpenRouterCredentials(true, null, baseUrl);
            return new PlatformOpenRouterCredentials(true, plain, baseUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Échec du déchiffrement de la clé OpenRouter plateforme.");
            return new PlatformOpenRouterCredentials(true, null, baseUrl);
        }
    }

    private PlatformOpenRouterSettingsDto MapOpenRouterDto(OpenRouterCacheRow? row)
    {
        var defaultBase = (_openRouterDefaults.DefaultBaseUrl ?? "https://openrouter.ai/api/v1").TrimEnd('/');
        var configured = row is not null
            && !string.IsNullOrWhiteSpace(row.EncryptedApiKey)
            && !string.IsNullOrWhiteSpace(row.ApiKeyLast4);

        return new PlatformOpenRouterSettingsDto(
            row?.IsEnabled ?? false,
            row?.DisplayName,
            row?.BaseUrl,
            defaultBase,
            configured,
            configured ? row!.ApiKeyLast4 : null);
    }

    private async Task<PlatformAiSettings> GetOrCreateRowAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var current = await _db.PlatformAiSettings.FirstOrDefaultAsync(cancellationToken);
        if (current is not null)
            return current;

        current = PlatformAiSettings.CreateDefaults();
        current.SetAuditInfo(actorUserId.ToString());
        _db.PlatformAiSettings.Add(current);
        return current;
    }

    private void InvalidateReadCache()
    {
        _cache.Remove(CacheKeyDefaultModel);
        _cache.Remove(CacheKeyImportModel);
        _cache.Remove(CacheKeyStudioModel);
        _cache.Remove(CacheKeyInferenceDevice);
        _cache.Remove(CacheKeyOpenRouter);
    }

    private sealed record OpenRouterCacheRow(
        bool IsEnabled,
        string? DisplayName,
        string? BaseUrl,
        string? EncryptedApiKey,
        string? ApiKeyLast4);
}
