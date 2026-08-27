using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.CashDesk.Services;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.CashDesk.Queries;

/// <summary>
/// Query to get paginated non-cancelled cash desk operations for a month.
/// </summary>
public sealed record GetCashOperationListQuery(
    int Year,
    int Month,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<CashOperationListItemDto>>;

public sealed class GetCashOperationListQueryHandler
    : IRequestHandler<GetCashOperationListQuery, PagedResult<CashOperationListItemDto>>
{
    private readonly ICashOperationRepository _cashOperationRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly ISupplierPaymentRepository _supplierPaymentRepository;

    public GetCashOperationListQueryHandler(
        ICashOperationRepository cashOperationRepository,
        IPaymentRepository paymentRepository,
        ISupplierPaymentRepository supplierPaymentRepository)
    {
        _cashOperationRepository = cashOperationRepository;
        _paymentRepository = paymentRepository;
        _supplierPaymentRepository = supplierPaymentRepository;
    }

    public async Task<PagedResult<CashOperationListItemDto>> Handle(
        GetCashOperationListQuery request,
        CancellationToken cancellationToken)
    {
        var from = new DateTime(request.Year, request.Month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var (items, totalCount) = await _cashOperationRepository.GetNonCancelledByDateRangeAsync(
            from, to, request.Page, request.PageSize, cancellationToken);

        var paymentSourceIds = items
            .Where(op => op.SourceType == "Payment" && op.SourceId.HasValue)
            .Select(op => op.SourceId!.Value)
            .Distinct()
            .ToList();

        var paymentSourceMap = new Dictionary<Guid, (Guid InvoiceId, string InvoiceNumber)>();
        foreach (var paymentId in paymentSourceIds)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId, cancellationToken);
            if (payment?.Invoice is null)
                continue;

            paymentSourceMap[paymentId] = (payment.InvoiceId, payment.Invoice.Number.Value);
        }

        var supplierPaymentSourceIds = items
            .Where(op => op.SourceType == "SupplierPayment" && op.SourceId.HasValue)
            .Select(op => op.SourceId!.Value)
            .Distinct()
            .ToList();

        var supplierPaymentSourceMap = new Dictionary<Guid, (Guid SupplierInvoiceId, string InvoiceNumber)>();
        if (supplierPaymentSourceIds.Count > 0)
        {
            var supplierPayments = await _supplierPaymentRepository.GetByIdsWithInvoiceAsync(
                supplierPaymentSourceIds,
                cancellationToken);

            foreach (var p in supplierPayments)
            {
                if (p.SupplierInvoice is null)
                    continue;

                supplierPaymentSourceMap[p.Id] = (p.SupplierInvoiceId, p.SupplierInvoice.InvoiceNumber);
            }
        }

        var dtos = items.Select(op =>
        {
            Guid? sourceInvoiceId = null;
            string? sourceInvoiceNumber = null;
            if (op.SourceType == "Payment" &&
                op.SourceId.HasValue &&
                paymentSourceMap.TryGetValue(op.SourceId.Value, out var sourceInvoice))
            {
                sourceInvoiceId = sourceInvoice.InvoiceId;
                sourceInvoiceNumber = sourceInvoice.InvoiceNumber;
            }

            Guid? sourceSupplierInvoiceId = null;
            string? sourceSupplierInvoiceNumber = null;
            if (op.SourceType == "SupplierPayment" &&
                op.SourceId.HasValue &&
                supplierPaymentSourceMap.TryGetValue(op.SourceId.Value, out var supplierSrc))
            {
                sourceSupplierInvoiceId = supplierSrc.SupplierInvoiceId;
                sourceSupplierInvoiceNumber = supplierSrc.InvoiceNumber;
            }

            var vatSplit = CashOperationVatCalculator.TrySplitTtc(op.Amount.Amount, op.VatRate);

            return new CashOperationListItemDto
        {
            Id = op.Id,
            OperationType = op.OperationType,
            OperationTypeDisplay = op.OperationType == CashOperationType.Debit ? "Débit" : "Crédit",
            OperationDate = op.OperationDate,
            Method = op.Method,
            MethodDisplay = op.Method.ToDisplayString(),
            Label = op.Label,
            Category = op.Category,
            CategoryDisplay = op.Category?.ToDisplayString(),
            RevenueCategory = op.RevenueCategory,
            RevenueCategoryDisplay = op.RevenueCategory?.ToDisplayString(),
            Document = op.Number.Value,
            Amount = op.Amount.Amount,
            Currency = op.Amount.Currency,
            Reference = op.Reference,
            Notes = op.Notes,
            Status = op.Status,
            Origin = op.Origin,
            SourceType = op.SourceType,
            SourceId = op.SourceId,
            SourceInvoiceId = sourceInvoiceId,
            SourceInvoiceNumber = sourceInvoiceNumber,
            SourceSupplierInvoiceId = sourceSupplierInvoiceId,
            SourceSupplierInvoiceNumber = sourceSupplierInvoiceNumber,
            VatRatePercent = vatSplit?.RatePercent,
            HtAmount = vatSplit?.Ht,
            VatAmount = vatSplit?.Vat
        };
        }).ToList();

        return PagedResult<CashOperationListItemDto>.Create(
            dtos,
            request.Page,
            request.PageSize,
            totalCount);
    }
}
