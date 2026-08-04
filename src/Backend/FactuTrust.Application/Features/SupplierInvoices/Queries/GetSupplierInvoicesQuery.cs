using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.SupplierInvoices.Queries;

/// <summary>
/// Query to get paginated supplier invoices with optional filters.
/// </summary>
public sealed record GetSupplierInvoicesQuery(
    string? Search = null,
    SupplierInvoiceStatus? Status = null,
    Guid? SupplierId = null,
    Guid? PurchaseOrderId = null,
    Guid? PurchaseReceiptId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 20,
    bool UnpaidOnly = false) : IRequest<PagedResult<SupplierInvoiceListDto>>;

/// <summary>
/// Handler for GetSupplierInvoicesQuery.
/// </summary>
public sealed class GetSupplierInvoicesQueryHandler : IRequestHandler<GetSupplierInvoicesQuery, PagedResult<SupplierInvoiceListDto>>
{
    private readonly ISupplierInvoiceRepository _repository;

    public GetSupplierInvoicesQueryHandler(ISupplierInvoiceRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<SupplierInvoiceListDto>> Handle(GetSupplierInvoicesQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _repository.SearchAsync(
            request.Search,
            request.Status,
            request.SupplierId,
            request.PurchaseOrderId,
            request.PurchaseReceiptId,
            request.FromDate,
            request.ToDate,
            request.Page,
            request.PageSize,
            request.UnpaidOnly,
            cancellationToken);

        var dtos = items.Select(si =>
        {
            var totalPaid = si.Payments.Sum(p => p.Amount.Amount);
            var remainingAmount = si.TotalAmount.Amount - totalPaid;
            return new SupplierInvoiceListDto
            {
                Id = si.Id,
                InvoiceNumber = si.InvoiceNumber,
                InvoiceDate = si.InvoiceDate,
                DueDate = si.DueDate,
                SupplierName = si.Supplier?.Name ?? "—",
                SupplierId = si.SupplierId,
                PurchaseOrderNumber = si.PurchaseOrder?.Number.Value,
                PurchaseOrderId = si.PurchaseOrderId,
                Status = si.Status,
                StatusDisplay = si.Status.ToDisplayString(),
                StatusCss = si.Status.ToCssClass(),
                TotalHT = si.SubTotal.Amount,
                TotalTTC = si.TotalAmount.Amount,
                LineCount = si.Lines.Count,
                TotalPaid = totalPaid,
                RemainingAmount = remainingAmount,
                PaidAt = si.PaidAt,
                WarehouseId = si.WarehouseId,
                WarehouseName = si.Warehouse?.Name,
                HasFixedAssetLines = si.Lines.Any(l => l.IsFixedAsset)
            };
        }).ToList();

        return PagedResult<SupplierInvoiceListDto>.Create(dtos, request.Page, request.PageSize, totalCount);
    }
}
