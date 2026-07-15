using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.SupplierInvoices.Commands;

/// <summary>
/// Command to cancel a supplier invoice.
/// </summary>
public sealed record CancelSupplierInvoiceCommand(Guid Id, string Reason) : IRequest<Result>;

/// <summary>
/// Handler for CancelSupplierInvoiceCommand.
/// </summary>
public sealed class CancelSupplierInvoiceCommandHandler : IRequestHandler<CancelSupplierInvoiceCommand, Result>
{
    private readonly ISupplierInvoiceRepository _repository;
    private readonly IAuditService _auditService;
    private readonly IAccountingService _accountingService;

    public CancelSupplierInvoiceCommandHandler(
        ISupplierInvoiceRepository repository,
        IAuditService auditService,
        IAccountingService accountingService)
    {
        _repository = repository;
        _auditService = auditService;
        _accountingService = accountingService;
    }

    public async Task<Result> Handle(CancelSupplierInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _repository.GetByIdWithLinesAsync(request.Id, cancellationToken);
        if (invoice is null)
            return Result.Failure(Error.NotFound("SupplierInvoice", request.Id));

        var result = invoice.Cancel(request.Reason);
        if (result.IsFailure)
            return result;

        await _repository.UpdateAsync(invoice, cancellationToken);

        // Reverse the accounting entry for the cancelled supplier invoice
        var reversalResult = await _accountingService.ReverseSupplierInvoiceEntryAsync(
            invoice.Id, invoice.InvoiceNumber, cancellationToken);
        if (reversalResult.IsFailure)
        {
            // Log but don't fail the cancellation — the reversal can be done manually
        }

        await _auditService.LogAsync(
            AuditActions.SupplierInvoice.Cancelled,
            "SupplierInvoice",
            invoice.Id,
            newValues: new { invoice.InvoiceNumber, request.Reason },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
