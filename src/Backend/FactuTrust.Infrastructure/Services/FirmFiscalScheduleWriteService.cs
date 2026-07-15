using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Commands;
using FactuTrust.Application.Features.Accounting.Queries;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Infrastructure.Services;

public sealed class FirmFiscalScheduleWriteService : IFirmFiscalScheduleWriteService
{
    private readonly IFirmAssignmentService _assignmentService;
    private readonly ITenantService _tenantService;
    private readonly ITenantContext _tenantContext;
    private readonly IMediator _mediator;
    private readonly IFiscalScheduleAttachmentService _attachments;

    public FirmFiscalScheduleWriteService(
        IFirmAssignmentService assignmentService,
        ITenantService tenantService,
        ITenantContext tenantContext,
        IMediator mediator,
        IFiscalScheduleAttachmentService attachments)
    {
        _assignmentService = assignmentService;
        _tenantService = tenantService;
        _tenantContext = tenantContext;
        _mediator = mediator;
        _attachments = attachments;
    }

    public Task<IReadOnlyList<FirmClientDossierDto>> GetCompaniesAsync(Guid firmTenantId, CancellationToken cancellationToken = default)
        => _assignmentService.GetActiveClientsAsync(firmTenantId, cancellationToken);

    public Task<Result<int>> EnsureFiscalYearAsync(Guid firmTenantId, Guid companyTenantId, int fiscalYear, CancellationToken cancellationToken = default)
        => ExecuteAsync(firmTenantId, companyTenantId, () => _mediator.Send(new EnsureFiscalScheduleCommand(fiscalYear), cancellationToken), cancellationToken);

    public Task<Result<FiscalScheduleEntryDto>> CreateEntryAsync(Guid firmTenantId, Guid companyTenantId, CreateFiscalScheduleEntryRequest request, CancellationToken cancellationToken = default)
        => ExecuteAsync(firmTenantId, companyTenantId, () => _mediator.Send(new CreateFiscalScheduleEntryCommand(request), cancellationToken), cancellationToken);

    public Task<Result<FiscalScheduleEntryDto>> UpdateEntryAsync(Guid firmTenantId, Guid companyTenantId, Guid entryId, UpdateFiscalScheduleEntryRequest request, CancellationToken cancellationToken = default)
        => ExecuteAsync(firmTenantId, companyTenantId, () => _mediator.Send(new UpdateFiscalScheduleEntryCommand(entryId, request), cancellationToken), cancellationToken);

    public Task<Result> DeleteEntryAsync(Guid firmTenantId, Guid companyTenantId, Guid entryId, CancellationToken cancellationToken = default)
        => ExecuteVoidAsync(firmTenantId, companyTenantId, () => _mediator.Send(new DeleteFiscalScheduleEntryCommand(entryId), cancellationToken), cancellationToken);

    public Task<Result<FiscalScheduleEntryDto>> MarkDepositedAsync(Guid firmTenantId, Guid companyTenantId, Guid entryId, MarkFiscalScheduleDepositedRequest request, CancellationToken cancellationToken = default)
        => ExecuteAsync(firmTenantId, companyTenantId, () => _mediator.Send(new MarkFiscalScheduleDepositedCommand(entryId, request), cancellationToken), cancellationToken);

    public Task<Result<FiscalScheduleEntryDto>> CapturePaymentAsync(Guid firmTenantId, Guid companyTenantId, Guid entryId, CaptureFiscalSchedulePaymentRequest request, CancellationToken cancellationToken = default)
        => ExecuteAsync(firmTenantId, companyTenantId, () => _mediator.Send(new CaptureFiscalSchedulePaymentCommand(entryId, request), cancellationToken), cancellationToken);

    public Task<Result<FiscalScheduleEntryDto>> MarkValidatedAsync(Guid firmTenantId, Guid companyTenantId, Guid entryId, MarkFiscalScheduleValidatedRequest request, CancellationToken cancellationToken = default)
        => ExecuteAsync(firmTenantId, companyTenantId, () => _mediator.Send(new MarkFiscalScheduleValidatedCommand(entryId, request), cancellationToken), cancellationToken);

    public Task<Result<FiscalScheduleEntryDto>> ScheduleReminderAsync(Guid firmTenantId, Guid companyTenantId, Guid entryId, ScheduleFiscalReminderRequest request, CancellationToken cancellationToken = default)
        => ExecuteAsync(firmTenantId, companyTenantId, () => _mediator.Send(new ScheduleFiscalReminderCommand(entryId, request), cancellationToken), cancellationToken);

    public Task<Result<IReadOnlyList<FiscalScheduleHistoryDto>>> GetHistoryAsync(Guid firmTenantId, Guid companyTenantId, Guid entryId, CancellationToken cancellationToken = default)
        => ExecuteAsync(firmTenantId, companyTenantId, () => _mediator.Send(new GetFiscalScheduleHistoryQuery(entryId), cancellationToken), cancellationToken);

    public Task<Result<IReadOnlyList<FiscalScheduleAttachmentDto>>> GetAttachmentsAsync(Guid firmTenantId, Guid companyTenantId, Guid entryId, CancellationToken cancellationToken = default)
        => ExecuteAsync(firmTenantId, companyTenantId, () => _attachments.ListAsync(entryId, cancellationToken), cancellationToken);

    public Task<Result<FiscalScheduleAttachmentDto>> UploadAttachmentAsync(Guid firmTenantId, Guid companyTenantId, Guid entryId, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default)
        => ExecuteAsync(firmTenantId, companyTenantId, () => _attachments.UploadAsync(entryId, fileName, contentType, content, cancellationToken), cancellationToken);

    public Task<Result<AttachmentDownload>> DownloadAttachmentAsync(Guid firmTenantId, Guid companyTenantId, Guid entryId, Guid attachmentId, CancellationToken cancellationToken = default)
        => ExecuteAsync(firmTenantId, companyTenantId, () => _attachments.DownloadAsync(attachmentId, cancellationToken), cancellationToken);

    public Task<Result> DeleteAttachmentAsync(Guid firmTenantId, Guid companyTenantId, Guid entryId, Guid attachmentId, CancellationToken cancellationToken = default)
        => ExecuteVoidAsync(firmTenantId, companyTenantId, () => _attachments.DeleteAsync(entryId, attachmentId, cancellationToken), cancellationToken);

    private async Task<Result> ExecuteVoidAsync(
        Guid firmTenantId,
        Guid companyTenantId,
        Func<Task<Result>> action,
        CancellationToken cancellationToken)
    {
        var guard = await EnsureAssignmentAndConnectionAsync(firmTenantId, companyTenantId, cancellationToken);
        if (guard.IsFailure)
            return Result.Failure(guard.Error);

        var previousTenantId = _tenantContext.TenantId;
        var previousConnection = _tenantContext.ConnectionString;
        try
        {
            _tenantContext.SetTenant(companyTenantId, guard.Value);
            return await action();
        }
        finally
        {
            if (previousTenantId.HasValue && !string.IsNullOrWhiteSpace(previousConnection))
                _tenantContext.SetTenant(previousTenantId.Value, previousConnection);
            else
                _tenantContext.Clear();
        }
    }

    private async Task<Result<T>> ExecuteAsync<T>(
        Guid firmTenantId,
        Guid companyTenantId,
        Func<Task<Result<T>>> action,
        CancellationToken cancellationToken)
    {
        var guard = await EnsureAssignmentAndConnectionAsync(firmTenantId, companyTenantId, cancellationToken);
        if (guard.IsFailure)
            return Result.Failure<T>(guard.Error);

        var previousTenantId = _tenantContext.TenantId;
        var previousConnection = _tenantContext.ConnectionString;
        try
        {
            _tenantContext.SetTenant(companyTenantId, guard.Value);
            return await action();
        }
        finally
        {
            if (previousTenantId.HasValue && !string.IsNullOrWhiteSpace(previousConnection))
                _tenantContext.SetTenant(previousTenantId.Value, previousConnection);
            else
                _tenantContext.Clear();
        }
    }

    private async Task<Result<string>> EnsureAssignmentAndConnectionAsync(
        Guid firmTenantId,
        Guid companyTenantId,
        CancellationToken cancellationToken)
    {
        if (!await _assignmentService.HasActiveAssignmentAsync(firmTenantId, companyTenantId, cancellationToken))
            return Result.Failure<string>(Error.Forbidden("Aucune affectation active pour cette societe."));

        var connectionString = await _tenantService.GetConnectionStringAsync(companyTenantId, cancellationToken);
        if (string.IsNullOrWhiteSpace(connectionString))
            return Result.Failure<string>(Error.NotFound("Tenant", companyTenantId));

        return Result.Success(connectionString);
    }
}
