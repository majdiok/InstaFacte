using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IEmployeeSocialFundEnrollmentRepository
{
    Task<EmployeeSocialFundEnrollment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeSocialFundEnrollment>> ListByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeSocialFundEnrollment>> ListActiveForEmployeesAsync(
        IReadOnlyCollection<Guid> employeeIds,
        DateTime referenceDate,
        CancellationToken cancellationToken = default);

    Task<EmployeeSocialFundEnrollment> AddAsync(EmployeeSocialFundEnrollment entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(EmployeeSocialFundEnrollment entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(EmployeeSocialFundEnrollment entity, CancellationToken cancellationToken = default);
}
