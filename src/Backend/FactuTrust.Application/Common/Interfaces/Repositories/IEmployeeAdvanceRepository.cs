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

    /// <summary>Outstanding (unsettled) advances for a batch of employees (validation de paie : évite N requêtes).</summary>
    Task<IReadOnlyList<EmployeeAdvance>> ListOutstandingByEmployeeIdsAsync(IReadOnlyCollection<Guid> employeeIds, CancellationToken cancellationToken = default);

    /// <summary>Advances settled by a given payroll run (réouverture : remplace le chargement de toute la table).</summary>
    Task<IReadOnlyList<EmployeeAdvance>> ListSettledByPayrollRunIdAsync(Guid payrollRunId, CancellationToken cancellationToken = default);

    /// <summary>Persists a batch of modified advances in a single SaveChanges.</summary>
    Task UpdateRangeAsync(IReadOnlyList<EmployeeAdvance> entities, CancellationToken cancellationToken = default);
}
