using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>Repository des suspensions de contrat (module Paie).</summary>
public interface IEmployeePayrollSuspensionRepository : IRepository<EmployeePayrollSuspension>
{
    Task<IReadOnlyList<EmployeePayrollSuspension>> ListByEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeePayrollSuspension>> ListForMonthAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default);
}
