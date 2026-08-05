using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IEmployeeInKindBenefitRepository
{
    Task<EmployeeInKindBenefit?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeInKindBenefit>> ListByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeInKindBenefit>> ListActiveForEmployeesAsync(
        IReadOnlyCollection<Guid> employeeIds,
        DateTime referenceDate,
        CancellationToken cancellationToken = default);

    Task<EmployeeInKindBenefit> AddAsync(EmployeeInKindBenefit entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(EmployeeInKindBenefit entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(EmployeeInKindBenefit entity, CancellationToken cancellationToken = default);
}
