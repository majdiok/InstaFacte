using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using MediatR;

namespace FactuTrust.Application.Features.Suppliers.Queries;

/// <summary>
/// Query to get aggregated totals for the supplier list over the ENTIRE filtered set.
/// Mirrors <see cref="GetSuppliersQuery"/> filters (without pagination) so the UI totals zone
/// reflects exactly the active filters, not just the current page.
/// </summary>
public sealed record GetSuppliersSummaryQuery(
    string? Search = null,
    SupplierType? Type = null,
    bool? IsActive = null) : IRequest<SupplierListSummaryDto>;

/// <summary>
/// Handler for GetSuppliersSummaryQuery.
/// </summary>
public sealed class GetSuppliersSummaryQueryHandler
    : IRequestHandler<GetSuppliersSummaryQuery, SupplierListSummaryDto>
{
    private readonly ISupplierRepository _supplierRepository;

    public GetSuppliersSummaryQueryHandler(ISupplierRepository supplierRepository)
    {
        _supplierRepository = supplierRepository;
    }

    public Task<SupplierListSummaryDto> Handle(GetSuppliersSummaryQuery request, CancellationToken cancellationToken)
        => _supplierRepository.GetSummaryAsync(
            request.Search,
            request.Type,
            request.IsActive,
            cancellationToken);
}
