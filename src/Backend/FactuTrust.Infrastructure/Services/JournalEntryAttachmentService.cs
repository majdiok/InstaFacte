using System.Collections.Frozen;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;

namespace FactuTrust.Infrastructure.Services;

/// <summary>Options de stockage des pièces justificatives comptables. BasePath est HORS wwwroot
/// (les fichiers ne sont jamais servis statiquement — téléchargement via l'API authentifiée).</summary>
public sealed class AccountingAttachmentsOptions
{
    public const string SectionName = "AccountingAttachments";

    /// <summary>Racine de stockage. Relative au répertoire de l'application si non absolue.</summary>
    public string BasePath { get; set; } = "App_Data/attachments";
}

/// <summary>
/// GED des écritures comptables : stockage disque durci (whitelist MIME, 10 Mo max,
/// confinement anti path-traversal — pattern StudioFileStorageService) + métadonnées en base.
/// Chemin : {BasePath}/tenants/{tenantId}/journal/{entryId}/{guid}{ext}.
/// </summary>
public sealed class JournalEntryAttachmentService : IJournalEntryAttachmentService
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 Mo

    private static readonly FrozenDictionary<string, string> AllowedContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = ".pdf",
        ["image/jpeg"] = ".jpg",
        ["image/jpg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = ".xlsx",
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = ".docx"
    }.ToFrozenDictionary();

    private readonly ITenantDbContextFactory _contextFactory;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private readonly AccountingAttachmentsOptions _options;
    private readonly ILogger<JournalEntryAttachmentService> _logger;

    public JournalEntryAttachmentService(
        ITenantDbContextFactory contextFactory,
        ICurrentUser currentUser,
        IAuditService auditService,
        IOptions<AccountingAttachmentsOptions> options,
        ILogger<JournalEntryAttachmentService> logger)
    {
        _contextFactory = contextFactory;
        _currentUser = currentUser;
        _auditService = auditService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<JournalEntryAttachmentDto>>> ListAsync(Guid journalEntryId, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        var rows = await ctx.JournalEntryAttachments.AsNoTracking()
            .Where(a => a.JournalEntryId == journalEntryId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<JournalEntryAttachmentDto>>(rows.Select(Map).ToList());
    }

    public async Task<Result<JournalEntryAttachmentDto>> UploadAsync(
        Guid journalEntryId, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentType) || !AllowedContentTypes.TryGetValue(contentType.Trim(), out var ext))
            return Result.Failure<JournalEntryAttachmentDto>(Error.Validation("ContentType",
                "Type de fichier non autorisé. Formats acceptés : PDF, JPEG, PNG, WebP, Excel (.xlsx), Word (.docx)."));

        var tenantId = _currentUser.TenantId;
        if (tenantId is null)
            return Result.Failure<JournalEntryAttachmentDto>(Error.Validation("Tenant", "Tenant courant introuvable."));

        await using var ctx = _contextFactory.CreateContext();
        var entryExists = await ctx.JournalEntries.AsNoTracking()
            .AnyAsync(e => e.Id == journalEntryId, cancellationToken);
        if (!entryExists)
            return Result.Failure<JournalEntryAttachmentDto>(Error.NotFound("JournalEntry", journalEntryId));

        var basePath = ResolveBasePath();
        var relativePath = Path.Combine("tenants", tenantId.Value.ToString(), "journal",
            journalEntryId.ToString(), Guid.NewGuid().ToString("N") + ext);
        var fullPath = Path.GetFullPath(Path.Combine(basePath, relativePath));
        if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<JournalEntryAttachmentDto>(Error.Validation("Storage", "Chemin de stockage invalide."));

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        long totalRead = 0;
        try
        {
            await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            var buffer = new byte[81920];
            int read;
            while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
            {
                totalRead += read;
                if (totalRead > MaxFileSizeBytes)
                {
                    fileStream.Close();
                    TryDeleteFile(fullPath);
                    return Result.Failure<JournalEntryAttachmentDto>(Error.Validation("Size",
                        $"Le fichier ne doit pas dépasser {MaxFileSizeBytes / (1024 * 1024)} Mo."));
                }
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }
        catch
        {
            TryDeleteFile(fullPath);
            throw;
        }

        var create = JournalEntryAttachment.Create(journalEntryId, fileName, relativePath, contentType.Trim(), totalRead);
        if (create.IsFailure)
        {
            TryDeleteFile(fullPath);
            return Result.Failure<JournalEntryAttachmentDto>(create.Error);
        }

        var attachment = create.Value;
        attachment.SetAuditInfo(_currentUser.Email ?? "system", false);
        ctx.JournalEntryAttachments.Add(attachment);
        await ctx.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Accounting.AttachmentAdded,
            "JournalEntryAttachment",
            attachment.Id,
            newValues: new { journalEntryId, attachment.FileName, attachment.SizeBytes, attachment.ContentType },
            cancellationToken: cancellationToken);

        return Result.Success(Map(attachment));
    }

    public async Task<Result<AttachmentDownload>> DownloadAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        var attachment = await ctx.JournalEntryAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken);
        if (attachment is null)
            return Result.Failure<AttachmentDownload>(Error.NotFound("JournalEntryAttachment", attachmentId));

        var fullPath = Path.GetFullPath(Path.Combine(ResolveBasePath(), attachment.StoragePath));
        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("Attachment file missing on disk: {Path}", attachment.StoragePath);
            return Result.Failure<AttachmentDownload>(Error.Validation("Storage", "Le fichier n'existe plus sur le stockage."));
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Result.Success(new AttachmentDownload(stream, attachment.ContentType, attachment.FileName));
    }

    public async Task<Result> DeleteAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        var attachment = await ctx.JournalEntryAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken);
        if (attachment is null)
            return Result.Failure(Error.NotFound("JournalEntryAttachment", attachmentId));

        // Intégrité : la pièce d'une écriture dont la période est close reste consultable mais
        // ne peut plus être supprimée (l'ajout, lui, reste permis — on complète le dossier).
        var periodClosed = await ctx.JournalEntries.AsNoTracking()
            .Where(e => e.Id == attachment.JournalEntryId)
            .Select(e => e.AccountingPeriod!.IsClosed)
            .FirstOrDefaultAsync(cancellationToken);
        if (periodClosed)
            return Result.Failure(Error.Validation("Period",
                "La période de cette écriture est clôturée : la pièce justificative ne peut plus être supprimée."));

        ctx.JournalEntryAttachments.Remove(attachment);
        await ctx.SaveChangesAsync(cancellationToken);

        TryDeleteFile(Path.GetFullPath(Path.Combine(ResolveBasePath(), attachment.StoragePath)));

        await _auditService.LogAsync(
            AuditActions.Accounting.AttachmentDeleted,
            "JournalEntryAttachment",
            attachment.Id,
            oldValues: new { attachment.JournalEntryId, attachment.FileName },
            cancellationToken: cancellationToken);

        return Result.Success();
    }

    private string ResolveBasePath()
    {
        var basePath = string.IsNullOrWhiteSpace(_options.BasePath) ? "App_Data/attachments" : _options.BasePath;
        return Path.GetFullPath(basePath);
    }

    private void TryDeleteFile(string fullPath)
    {
        try
        {
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete attachment file {Path}", fullPath);
        }
    }

    private static JournalEntryAttachmentDto Map(JournalEntryAttachment a) => new()
    {
        Id = a.Id,
        JournalEntryId = a.JournalEntryId,
        FileName = a.FileName,
        ContentType = a.ContentType,
        SizeBytes = a.SizeBytes,
        CreatedAt = a.CreatedAt,
        UploadedBy = a.CreatedBy
    };
}
