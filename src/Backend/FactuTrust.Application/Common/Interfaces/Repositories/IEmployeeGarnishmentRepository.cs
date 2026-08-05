using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IEmployeeGarnishmentRepository
{
    Task<EmployeeGarnishment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EmployeeGarnishment?> GetByIdWithInstallmentsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeGarnishment>> ListByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeGarnishment>> ListActiveForEmployeesAsync(
        IReadOnlyCollection<Guid> employeeIds,
        DateTime referenceDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeGarnishment>> ListWithInstallmentsForRunAsync(
        Guid payrollRunId,
        CancellationToken cancellationToken = default);

    Task<EmployeeGarnishment> AddAsync(EmployeeGarnishment entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(EmployeeGarnishment entity, CancellationToken cancellationToken = default);

    Task UpdateRangeAsync(IReadOnlyList<EmployeeGarnishment> entities, CancellationToken cancellationToken = default);
}
