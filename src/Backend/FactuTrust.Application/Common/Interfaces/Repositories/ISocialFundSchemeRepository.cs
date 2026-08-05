using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface ISocialFundSchemeRepository
{
    Task<SocialFundScheme?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SocialFundScheme?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SocialFundScheme>> ListAsync(bool activeOnly = false, CancellationToken cancellationToken = default);

    Task<SocialFundScheme> AddAsync(SocialFundScheme entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(SocialFundScheme entity, CancellationToken cancellationToken = default);
}
