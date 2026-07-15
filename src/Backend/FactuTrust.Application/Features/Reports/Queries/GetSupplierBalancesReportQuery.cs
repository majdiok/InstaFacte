using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Reports.Queries;

/// <summary>
/// Query to get supplier balances (solde = total facturé - total payé) for all suppliers.
/// </summary>
public sealed record GetSupplierBalancesReportQuery
    : IRequest<Result<IReadOnlyList<SupplierBalanceReportRowDto>>>;

/// <summary>
/// Handler for GetSupplierBalancesReportQuery.
/// </summary>
public sealed class GetSupplierBalancesReportQueryHandler
    : IRequestHandler<GetSupplierBalancesReportQuery, Result<IReadOnlyList<SupplierBalanceReportRowDto>>>
{
    private readonly ISupplierInvoiceRepository _supplierInvoiceRepository;
    private readonly ISupplierPaymentRepository _supplierPaymentRepository;

    public GetSupplierBalancesReportQueryHandler(
        ISupplierInvoiceRepository supplierInvoiceRepository,
        ISupplierPaymentRepository supplierPaymentRepository)
    {
        _supplierInvoiceRepository = supplierInvoiceRepository;
        _supplierPaymentRepository = supplierPaymentRepository;
    }

    public async Task<Result<IReadOnlyList<SupplierBalanceReportRowDto>>> Handle(
        GetSupplierBalancesReportQuery request,
        CancellationToken cancellationToken)
    {
        var invoices = await _supplierInvoiceRepository.GetAllAsync(cancellationToken);
        var nonCancelled = invoices.Where(i => i.Status != SupplierInvoiceStatus.Cancelled).ToList();
        if (nonCancelled.Count == 0)
            return Result.Success<IReadOnlyList<SupplierBalanceReportRowDto>>(Array.Empty<SupplierBalanceReportRowDto>());

        var totalPaidByInvoice = await _supplierPaymentRepository.GetTotalPaidBySupplierInvoiceIdsAsync(
            nonCancelled.Select(i => i.Id),
            cancellationToken);

        var bySupplier = nonCancelled
            .GroupBy(i => new { i.SupplierId, SupplierName = i.Supplier?.Name ?? "", Currency = i.TotalAmount.Currency })
            .Select(g =>
            {
                var totalInvoiced = g.Sum(i => i.TotalAmount.Amount);
                var totalPaid = g.Sum(i => totalPaidByInvoice.TryGetValue(i.Id, out var paid) ? paid : 0m);
                return new SupplierBalanceReportRowDto
                {
                    SupplierId = g.Key.SupplierId,
                    SupplierName = g.Key.SupplierName,
                    TotalInvoiced = totalInvoiced,
                    TotalPaid = totalPaid,
                    Balance = totalInvoiced - totalPaid,
                    Currency = g.Key.Currency
                };
            })
            .OrderBy(r => r.SupplierName)
            .ToList();

        return Result.Success<IReadOnlyList<SupplierBalanceReportRowDto>>(bySupplier);
    }
}
