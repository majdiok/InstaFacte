using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IPayrollVariableAllowanceRepository
{
    Task<PayrollVariableAllowanceLine?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PayrollVariableAllowanceLine>> ListForMonthAsync(int year, int month, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PayrollVariableAllowanceLine>> ListByEmployeeAndMonthAsync(
        Guid employeeId,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<PayrollVariableAllowanceLine> AddAsync(PayrollVariableAllowanceLine entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(PayrollVariableAllowanceLine entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(PayrollVariableAllowanceLine entity, CancellationToken cancellationToken = default);

    Task<PayrollVariableAllowanceLine?> GetAutoAnnualBonusLineAsync(
        Guid employeeId, int year, int month, Guid annualBonusRuleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PayrollVariableAllowanceLine>> ListAutoAnnualBonusForMonthAsync(
        int year, int month, CancellationToken cancellationToken = default);

    Task SaveBatchAsync(
        IReadOnlyList<PayrollVariableAllowanceLine> added,
        IReadOnlyList<PayrollVariableAllowanceLine> updated,
        CancellationToken cancellationToken = default);
}
