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
}
