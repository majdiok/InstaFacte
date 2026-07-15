using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository interface for employee salary advances (module Paie).
/// </summary>
public interface IEmployeeAdvanceRepository : IRepository<EmployeeAdvance>
{
    Task<IReadOnlyList<EmployeeAdvance>> ListByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>Outstanding (unsettled) advances for an employee.</summary>
    Task<IReadOnlyList<EmployeeAdvance>> ListOutstandingAsync(Guid employeeId, CancellationToken cancellationToken = default);
}
