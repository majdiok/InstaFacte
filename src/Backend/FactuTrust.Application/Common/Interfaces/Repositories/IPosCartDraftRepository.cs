using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IPosCartDraftRepository : IRepository<PosCartDraft>
{
    Task<PosCartDraft?> GetByUserAndRegisterAsync(
        Guid userId,
        Guid cashRegisterId,
        CancellationToken cancellationToken = default);

    Task<PosCartDraft?> GetLatestByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task DeleteByUserAndRegisterAsync(
        Guid userId,
        Guid cashRegisterId,
        CancellationToken cancellationToken = default);
}
