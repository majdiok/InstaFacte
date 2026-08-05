using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IEmployeeLoanRepository
{
    Task<EmployeeLoan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EmployeeLoan?> GetByIdWithInstallmentsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeLoan>> ListByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeLoan>> ListWithDueInstallmentsForMonthAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeLoan>> ListWithSettledInstallmentsForRunAsync(
        Guid payrollRunId,
        CancellationToken cancellationToken = default);

    Task<EmployeeLoan> AddAsync(EmployeeLoan entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(EmployeeLoan entity, CancellationToken cancellationToken = default);

    Task UpdateRangeAsync(IReadOnlyList<EmployeeLoan> entities, CancellationToken cancellationToken = default);
}
