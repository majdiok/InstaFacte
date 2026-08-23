using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IStockVoucherRepository : IRepository<StockVoucher>
{
    Task<StockVoucher?> GetByIdWithLinesAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<StockVoucher> Items, int TotalCount)> SearchAsync(
        StockVoucherKind? kind,
        string? searchTerm,
        StockVoucherStatus? status,
        Guid? warehouseId,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<StockVoucherListSummaryDto> GetSummaryAsync(
        StockVoucherKind? kind,
        string? searchTerm,
        StockVoucherStatus? status,
        Guid? warehouseId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default);
}
