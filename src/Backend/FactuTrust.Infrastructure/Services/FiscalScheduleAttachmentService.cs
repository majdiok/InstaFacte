using System.Collections.Frozen;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.FiscalSchedule;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;

namespace FactuTrust.Infrastructure.Services;

public sealed class FiscalScheduleAttachmentService : IFiscalScheduleAttachmentService
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

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

    private readonly IFiscalScheduleRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private readonly AccountingAttachmentsOptions _options;
    private readonly ILogger<FiscalScheduleAttachmentService> _logger;

    public FiscalScheduleAttachmentService(
        IFiscalScheduleRepository repository,
        ICurrentUser currentUser,
        IAuditService auditService,
        IOptions<AccountingAttachmentsOptions> options,
        ILogger<FiscalScheduleAttachmentService> logger)
    {
        _repository = repository;
        _currentUser = currentUser;
        _auditService = auditService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<FiscalScheduleAttachmentDto>>> ListAsync(Guid fiscalScheduleEntryId, CancellationToken cancellationToken = default)
    {
        var rows = await _repository.GetAttachmentsAsync(fiscalScheduleEntryId, cancellationToken);
        return Result.Success<IReadOnlyList<FiscalScheduleAttachmentDto>>(rows.Select(FiscalScheduleMappings.ToDto).ToList());
    }

    public async Task<Result<FiscalScheduleAttachmentDto>> UploadAsync(
        Guid fiscalScheduleEntryId,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentType) || !AllowedContentTypes.TryGetValue(contentType.Trim(), out var ext))
            return Result.Failure<FiscalScheduleAttachmentDto>(Error.Validation("ContentType",
                "Type de fichier non autorise. Formats acceptes : PDF, JPEG, PNG, WebP, Excel (.xlsx), Word (.docx)."));

        var tenantId = _currentUser.TenantId;
        if (tenantId is null)
            return Result.Failure<FiscalScheduleAttachmentDto>(Error.Validation("Tenant", "Tenant courant introuvable."));

        var entry = await _repository.GetByIdAsync(fiscalScheduleEntryId, cancellationToken);
        if (entry is null)
            return Result.Failure<FiscalScheduleAttachmentDto>(Error.NotFound("FiscalScheduleEntry", fiscalScheduleEntryId));

        var basePath = ResolveBasePath();
        var relativePath = Path.Combine("tenants", tenantId.Value.ToString(), "fiscal-schedule",
            fiscalScheduleEntryId.ToString(), Guid.NewGuid().ToString("N") + ext);
        var fullPath = Path.GetFullPath(Path.Combine(basePath, relativePath));
        if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<FiscalScheduleAttachmentDto>(Error.Validation("Storage", "Chemin de stockage invalide."));

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
                    return Result.Failure<FiscalScheduleAttachmentDto>(Error.Validation("Size",
                        $"Le fichier ne doit pas depasser {MaxFileSizeBytes / (1024 * 1024)} Mo."));
                }
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }
        catch
        {
            TryDeleteFile(fullPath);
            throw;
        }

        var create = FiscalScheduleAttachment.Create(
            fiscalScheduleEntryId,
            fileName,
            contentType.Trim(),
            totalRead,
            relativePath);
        if (create.IsFailure)
        {
            TryDeleteFile(fullPath);
            return Result.Failure<FiscalScheduleAttachmentDto>(create.Error);
        }

        var attachment = create.Value;
        attachment.SetAuditInfo(_currentUser.Email ?? "system", false);
        await _repository.AddAttachmentAsync(
            attachment,
            FiscalScheduleHistoryEntry.Create(fiscalScheduleEntryId, "AttachmentAdded", $"Piece jointe ajoutee : {attachment.FileName}."),
            cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Accounting.FiscalScheduleAttachmentAdded,
            "FiscalScheduleAttachment",
            attachment.Id,
            newValues: new { fiscalScheduleEntryId, attachment.FileName, attachment.SizeBytes, attachment.ContentType },
            cancellationToken: cancellationToken);

        return Result.Success(FiscalScheduleMappings.ToDto(attachment));
    }

    public async Task<Result<AttachmentDownload>> DownloadAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await _repository.GetAttachmentAsync(attachmentId, cancellationToken);
        if (attachment is null)
            return Result.Failure<AttachmentDownload>(Error.NotFound("FiscalScheduleAttachment", attachmentId));

        var fullPath = Path.GetFullPath(Path.Combine(ResolveBasePath(), attachment.StoragePath));
        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("Fiscal schedule attachment file missing on disk: {Path}", attachment.StoragePath);
            return Result.Failure<AttachmentDownload>(Error.Validation("Storage", "Le fichier n'existe plus sur le stockage."));
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Result.Success(new AttachmentDownload(stream, attachment.ContentType, attachment.FileName));
    }

    public async Task<Result> DeleteAsync(Guid fiscalScheduleEntryId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await _repository.GetAttachmentAsync(attachmentId, cancellationToken);
        if (attachment is null)
            return Result.Failure(Error.NotFound("FiscalScheduleAttachment", attachmentId));
        if (attachment.FiscalScheduleEntryId != fiscalScheduleEntryId)
            return Result.Failure(Error.Validation("Attachment", "La piece jointe ne correspond pas a cette echeance."));

        await _repository.DeleteAttachmentAsync(
            attachment,
            FiscalScheduleHistoryEntry.Create(fiscalScheduleEntryId, "AttachmentDeleted", $"Piece jointe supprimee : {attachment.FileName}."),
            cancellationToken);

        TryDeleteFile(Path.GetFullPath(Path.Combine(ResolveBasePath(), attachment.StoragePath)));

        await _auditService.LogAsync(
            AuditActions.Accounting.FiscalScheduleAttachmentDeleted,
            "FiscalScheduleAttachment",
            attachment.Id,
            oldValues: new { fiscalScheduleEntryId, attachment.FileName },
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
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete fiscal schedule attachment file {Path}", fullPath);
        }
    }
}
