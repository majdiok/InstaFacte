using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository interface for leave/absence requests (module Paie).
/// </summary>
public interface ILeaveRequestRepository : IRepository<LeaveRequest>
{
    Task<IReadOnlyList<LeaveRequest>> ListByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>Approved leaves overlapping a given month (used to derive unpaid-absence days).</summary>
    Task<IReadOnlyList<LeaveRequest>> ListForMonthAsync(int year, int month, CancellationToken cancellationToken = default);

    /// <summary>Approved sick leaves for a calendar year (IJ annual cap).</summary>
    Task<IReadOnlyList<LeaveRequest>> ListSickLeavesForYearAsync(int year, CancellationToken cancellationToken = default);
}
