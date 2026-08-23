using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.StockVouchers.Queries;

internal static class StockVoucherDtoMapper
{
    public static StockVoucherListDto ToListDto(StockVoucher voucher) => new()
    {
        Id = voucher.Id,
        Number = voucher.Number.Value,
        Kind = voucher.Kind,
        KindDisplay = voucher.Kind.ToDisplayString(),
        VoucherDate = voucher.VoucherDate,
        Status = voucher.Status,
        StatusDisplay = voucher.Status.ToDisplayString(),
        StatusCss = voucher.Status.ToCssClass(),
        Reason = voucher.Reason,
        ReasonDisplay = voucher.Kind.ToVoucherReasonDisplay(voucher.Reason),
        WarehouseId = voucher.WarehouseId,
        WarehouseName = voucher.Warehouse?.Name,
        ExternalReference = voucher.ExternalReference,
        LineCount = voucher.Lines.Count,
        TotalQuantity = voucher.TotalQuantity,
        TotalValue = voucher.TotalValue
    };

    public static StockVoucherDetailDto ToDetailDto(StockVoucher voucher) => new()
    {
        Id = voucher.Id,
        Number = voucher.Number.Value,
        Kind = voucher.Kind,
        KindDisplay = voucher.Kind.ToDisplayString(),
        VoucherDate = voucher.VoucherDate,
        Status = voucher.Status,
        StatusDisplay = voucher.Status.ToDisplayString(),
        StatusCss = voucher.Status.ToCssClass(),
        Reason = voucher.Reason,
        ReasonDisplay = voucher.Kind.ToVoucherReasonDisplay(voucher.Reason),
        WarehouseId = voucher.WarehouseId,
        WarehouseName = voucher.Warehouse?.Name,
        ExternalReference = voucher.ExternalReference,
        Notes = voucher.Notes,
        Lines = voucher.Lines.OrderBy(l => l.LineNumber).Select(l => new StockVoucherLineDto
        {
            Id = l.Id,
            LineNumber = l.LineNumber,
            ProductId = l.ProductId,
            ProductCode = l.ProductCode,
            ProductName = l.ProductName,
            Unit = l.Unit,
            Quantity = l.Quantity,
            UnitCost = l.UnitCost,
            LineValue = l.LineValue,
            Notes = l.Notes
        }).ToList(),
        TotalQuantity = voucher.TotalQuantity,
        TotalValue = voucher.TotalValue,
        ValidatedAt = voucher.ValidatedAt,
        CancelledAt = voucher.CancelledAt,
        CancellationReason = voucher.CancellationReason,
        CreatedAt = voucher.CreatedAt
    };
}
