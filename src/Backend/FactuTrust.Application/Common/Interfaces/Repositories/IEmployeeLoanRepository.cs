using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IEmployeeLoanRepository
{
    Task<EmployeeLoan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EmployeeLoan?> GetByIdWithInstallmentsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Charge le prêt (avec échéances) contenant une échéance donnée (outil firm-only de dé-solde review).</summary>
    Task<EmployeeLoan?> GetByInstallmentIdAsync(Guid installmentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeLoan>> ListByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeLoan>> ListWithDueInstallmentsForMonthAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeLoan>> ListWithSettledInstallmentsForRunAsync(
        Guid payrollRunId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tous les prêts actifs avec leurs échéances (réglées ou non). Lecture seule utilisée par le
    /// diagnostic de conformité paie (plan §5.4) : composition du solde 421.1 et détection des
    /// échéances réglées sans ligne de bulletin correspondante.
    /// </summary>
    Task<IReadOnlyList<EmployeeLoan>> ListAllWithInstallmentsAsync(
        CancellationToken cancellationToken = default);

    Task<EmployeeLoan> AddAsync(EmployeeLoan entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(EmployeeLoan entity, CancellationToken cancellationToken = default);

    Task UpdateRangeAsync(IReadOnlyList<EmployeeLoan> entities, CancellationToken cancellationToken = default);
}
