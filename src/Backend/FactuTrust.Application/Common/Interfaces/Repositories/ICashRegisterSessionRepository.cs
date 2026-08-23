using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface ICashRegisterSessionRepository : IRepository<CashRegisterSession>
{
    Task<CashRegisterSession?> GetOpenByRegisterIdAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default);

    Task<CashRegisterSession?> GetByIdWithRegisterAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
