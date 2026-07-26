using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Commands;

/// <summary>Aperçu (dry-run) d'un import de référentiel (plan comptable / plan tiers / balance d'ouverture).</summary>
public sealed record PreviewReferenceImportCommand(
    byte[] Content, ReferenceImportTarget Target, JournalImportFormat Format, int? FiscalYear)
    : IRequest<Result<ReferenceImportPreviewDto>>;

public sealed class PreviewReferenceImportCommandHandler
    : IRequestHandler<PreviewReferenceImportCommand, Result<ReferenceImportPreviewDto>>
{
    private readonly IReferenceDataImportService _importService;

    public PreviewReferenceImportCommandHandler(IReferenceDataImportService importService)
    {
        _importService = importService;
    }

    public Task<Result<ReferenceImportPreviewDto>> Handle(PreviewReferenceImportCommand request, CancellationToken cancellationToken)
        => _importService.PreviewAsync(request.Content, request.Target, request.Format, request.FiscalYear, cancellationToken);
}

public sealed record CommitReferenceImportCommand(
    byte[] Content, ReferenceImportTarget Target, JournalImportFormat Format, int? FiscalYear)
    : IRequest<Result<ReferenceImportCommitResultDto>>;

public sealed class CommitReferenceImportCommandHandler
    : IRequestHandler<CommitReferenceImportCommand, Result<ReferenceImportCommitResultDto>>
{
    private readonly IReferenceDataImportService _importService;
    private readonly IAuditService _auditService;

    public CommitReferenceImportCommandHandler(IReferenceDataImportService importService, IAuditService auditService)
    {
        _importService = importService;
        _auditService = auditService;
    }

    public async Task<Result<ReferenceImportCommitResultDto>> Handle(CommitReferenceImportCommand request, CancellationToken cancellationToken)
    {
        var result = await _importService.CommitAsync(request.Content, request.Target, request.Format, request.FiscalYear, cancellationToken);
        if (result.IsSuccess)
        {
            await _auditService.LogAsync(
                AuditActions.Accounting.DossierImported,
                "ReferenceData",
                newValues: new { request.Target, request.Format, result.Value.CreatedCount, result.Value.SkippedCount },
                cancellationToken: cancellationToken);
        }

        return result;
    }
}
