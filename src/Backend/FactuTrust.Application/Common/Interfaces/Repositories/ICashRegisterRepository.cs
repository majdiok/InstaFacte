using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface ICashRegisterRepository : IRepository<CashRegister>
{
    /// <summary>
    /// Default active register for the warehouse, with fallback to the historical
    /// FirstOrDefault (active then code) when no row is flagged default yet.
    /// </summary>
    Task<CashRegister?> GetByWarehouseIdAsync(Guid warehouseId, CancellationToken cancellationToken = default);

    Task<CashRegister?> GetDefaultByWarehouseIdAsync(Guid warehouseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CashRegister>> ListByWarehouseIdAsync(Guid warehouseId, CancellationToken cancellationToken = default);

    Task<CashRegister?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);
}
