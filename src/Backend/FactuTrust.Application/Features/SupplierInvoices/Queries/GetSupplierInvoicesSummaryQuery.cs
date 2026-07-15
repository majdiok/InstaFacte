using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.SupplierInvoices.Queries;

/// <summary>
/// Query to get aggregated totals for the supplier invoice list over the ENTIRE filtered set.
/// Mirrors <see cref="GetSupplierInvoicesQuery"/> filters (without pagination) so the UI totals
/// zone reflects exactly the active filters, not just the current page.
/// </summary>
public sealed record GetSupplierInvoicesSummaryQuery(
    string? Search = null,
    SupplierInvoiceStatus? Status = null,
    Guid? SupplierId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    bool UnpaidOnly = false) : IRequest<SupplierInvoiceListSummaryDto>;

/// <summary>
/// Handler for GetSupplierInvoicesSummaryQuery.
/// </summary>
public sealed class GetSupplierInvoicesSummaryQueryHandler
    : IRequestHandler<GetSupplierInvoicesSummaryQuery, SupplierInvoiceListSummaryDto>
{
    private readonly ISupplierInvoiceRepository _repository;

    public GetSupplierInvoicesSummaryQueryHandler(ISupplierInvoiceRepository repository)
    {
        _repository = repository;
    }

    public Task<SupplierInvoiceListSummaryDto> Handle(GetSupplierInvoicesSummaryQuery request, CancellationToken cancellationToken)
        => _repository.GetSummaryAsync(
            request.Search,
            request.Status,
            request.SupplierId,
            request.FromDate,
            request.ToDate,
            request.UnpaidOnly,
            cancellationToken);
}
