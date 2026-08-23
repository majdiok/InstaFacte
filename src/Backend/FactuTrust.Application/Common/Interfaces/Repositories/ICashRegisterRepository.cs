using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface ICashRegisterRepository : IRepository<CashRegister>
{
    Task<CashRegister?> GetByWarehouseIdAsync(Guid warehouseId, CancellationToken cancellationToken = default);

    Task<CashRegister?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);
}
