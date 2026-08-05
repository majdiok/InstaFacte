using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository interface for the Employee aggregate (module Paie).
/// </summary>
public interface IEmployeeRepository : IRepository<Employee>
{
    /// <summary>Gets an employee with its contracts and allowances.</summary>
    Task<Employee?> GetByIdWithContractsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>All active employees with their contracts (used when computing a payroll run).</summary>
    Task<IReadOnlyList<Employee>> GetActiveWithContractsAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmployeeNumberAsync(string employeeNumber, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, string>> GetFullNamesByIdsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Charge les salariés par identifiants (sans contrats) — utilisé pour export virement (RIB).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, Employee>> GetByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Employee> Items, int TotalCount)> SearchAsync(
        string? searchTerm,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    // ── Contract operations (contracts belong to the Employee aggregate) ──
    Task<EmploymentContract> AddContractAsync(EmploymentContract contract, CancellationToken cancellationToken = default);
    Task<EmploymentContract?> GetContractAsync(Guid contractId, CancellationToken cancellationToken = default);
    Task UpdateContractAsync(EmploymentContract contract, CancellationToken cancellationToken = default);
    Task DeleteContractAsync(Guid contractId, CancellationToken cancellationToken = default);
}
