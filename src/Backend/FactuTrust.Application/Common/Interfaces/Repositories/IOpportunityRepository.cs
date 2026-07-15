using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IOpportunityRepository
{
    Task<Opportunity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Opportunity>> GetAllAsync(OpportunityStage? stage = null, Guid? assignedUserId = null, Guid? clientId = null, CancellationToken cancellationToken = default);
    Task<Opportunity> AddAsync(Opportunity entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(Opportunity entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Opportunity entity, CancellationToken cancellationToken = default);
}
