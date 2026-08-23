using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IPosHeldTicketRepository : IRepository<PosHeldTicket>
{
    Task<IReadOnlyList<PosHeldTicket>> ListByRegisterAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default);

    Task<int> CountByRegisterAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default);
}
