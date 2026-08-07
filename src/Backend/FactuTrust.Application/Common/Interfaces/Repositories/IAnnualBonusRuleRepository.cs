using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IAnnualBonusRuleRepository
{
    Task<AnnualBonusRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AnnualBonusRule?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AnnualBonusRule>> ListAsync(int? fiscalYear = null, CancellationToken cancellationToken = default);

    Task<AnnualBonusRule> AddAsync(AnnualBonusRule entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(AnnualBonusRule entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(AnnualBonusRule entity, CancellationToken cancellationToken = default);
}
