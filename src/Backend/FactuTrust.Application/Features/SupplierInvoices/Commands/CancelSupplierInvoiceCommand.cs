using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.SupplierInvoices.Commands;

public sealed record CancelSupplierInvoiceCommand(Guid Id, string Reason) : IRequest<Result>;

public sealed class CancelSupplierInvoiceCommandHandler : IRequestHandler<CancelSupplierInvoiceCommand, Result>
{
    private readonly ISupplierInvoiceRepository _repository;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IPurchaseReceiptRepository _purchaseReceiptRepository;
    private readonly IAuditService _auditService;
    private readonly IAccountingService _accountingService;

    public CancelSupplierInvoiceCommandHandler(
        ISupplierInvoiceRepository repository,
        IPurchaseOrderRepository purchaseOrderRepository,
        IPurchaseReceiptRepository purchaseReceiptRepository,
        IAuditService auditService,
        IAccountingService accountingService)
    {
        _repository = repository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _auditService = auditService;
        _accountingService = accountingService;
    }

    public async Task<Result> Handle(CancelSupplierInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _repository.GetByIdWithLinesAsync(request.Id, cancellationToken);
        if (invoice is null)
            return Result.Failure(Error.NotFound("SupplierInvoice", request.Id));

        var poImputations = invoice.GetPurchaseOrderImputations();
        var prImputations = invoice.GetPurchaseReceiptImputations();

        var result = invoice.Cancel(request.Reason);
        if (result.IsFailure)
            return result;

        await _repository.UpdateAsync(invoice, cancellationToken);

        if (poImputations.Count > 0 && invoice.PurchaseOrderId is { } poId)
        {
            var po = await _purchaseOrderRepository.GetByIdWithLinesAsync(poId, cancellationToken);
            if (po is not null)
            {
                var reverseResult = po.ReverseInvoicing(poImputations);
                if (reverseResult.IsFailure)
                    return reverseResult;

                await _purchaseOrderRepository.UpdateAsync(po, cancellationToken);
            }
        }

        if (prImputations.Count > 0 && invoice.SourcePurchaseReceiptId is { } receiptId)
        {
            var receipt = await _purchaseReceiptRepository.GetByIdWithLinesAsync(receiptId, cancellationToken);
            if (receipt is not null)
            {
                var reverseResult = receipt.ReverseInvoicing(prImputations);
                if (reverseResult.IsFailure)
                    return reverseResult;

                await _purchaseReceiptRepository.UpdateAsync(receipt, cancellationToken);
            }
        }

        await _accountingService.ReverseSupplierInvoiceEntryAsync(
            invoice.Id, invoice.InvoiceNumber, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.SupplierInvoice.Cancelled,
            "SupplierInvoice",
            invoice.Id,
            newValues: new { invoice.InvoiceNumber, request.Reason },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
