using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IStockTransferRepository : IRepository<StockTransfer>
{
    Task<StockTransfer?> GetByIdWithLinesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<string?> GetLatestNumberAsync(int year, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockTransfer>> GetFilteredAsync(
        StockTransferStatus? status = null,
        Guid? warehouseId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);
}
