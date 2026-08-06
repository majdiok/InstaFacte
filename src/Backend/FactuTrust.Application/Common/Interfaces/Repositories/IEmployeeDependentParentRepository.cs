using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>Déclarations nominatives de parents à charge (non-cumul art. 40 IRPP).</summary>
public interface IEmployeeDependentParentRepository
{
    Task<IReadOnlyList<EmployeeDependentParent>> GetActiveByEmployeeIdAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeDependentParent>> GetActiveByEmployeeIdsAsync(
        IReadOnlyCollection<Guid> employeeIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeDependentParent>> ListAllActiveAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Index CIN parent actif → EmployeeId du déclarant (pour preflight et conflits).
    /// </summary>
    Task<IReadOnlyDictionary<string, Guid>> GetActiveCinIndexAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Autre salarié actif déclarant déjà ce CIN (hors <paramref name="excludeEmployeeId"/>).
    /// </summary>
    Task<EmployeeDependentParent?> FindActiveConflictAsync(
        string normalizedParentCin,
        Guid? excludeEmployeeId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remplace les déclarations actives d'un salarié : clôture les anciennes, insère les nouvelles.
    /// </summary>
    Task ReplaceActiveClaimsAsync(
        Guid employeeId,
        IReadOnlyList<EmployeeDependentParent> newClaims,
        CancellationToken cancellationToken = default);

    /// <summary>Clôture toutes les déclarations actives (désactivation / sortie salarié).</summary>
    Task EndAllActiveForEmployeeAsync(
        Guid employeeId,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}
