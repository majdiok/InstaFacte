using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IEmployeeAnnualBonusRuleRepository
{
    Task<EmployeeAnnualBonusRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EmployeeAnnualBonusRule?> GetByEmployeeAndRuleAsync(
        Guid employeeId, Guid ruleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeAnnualBonusRule>> ListActiveAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeAnnualBonusRule>> ListByEmployeeAsync(
        Guid employeeId, CancellationToken cancellationToken = default);

    Task<EmployeeAnnualBonusRule> AddAsync(EmployeeAnnualBonusRule entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(EmployeeAnnualBonusRule entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(EmployeeAnnualBonusRule entity, CancellationToken cancellationToken = default);
}
