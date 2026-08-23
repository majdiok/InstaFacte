using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IZReportRepository : IRepository<ZReport>
{
    Task<ZReport?> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ZReport>> ListAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default);
}
