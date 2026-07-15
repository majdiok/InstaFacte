using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IUserDashboardLayoutRepository
{
    Task<UserDashboardLayout?> GetAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(UserDashboardLayout layout, CancellationToken cancellationToken = default);
    Task UpdateAsync(UserDashboardLayout layout, CancellationToken cancellationToken = default);
}
