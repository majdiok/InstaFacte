using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>Un cycle de paie et le nombre de bulletins qu'il porte.</summary>
public sealed record PayrollRunWithPayslipCount(PayrollRun Run, int PayslipCount);

/// <summary>
/// Repository interface for the PayrollRun aggregate (module Paie).
/// </summary>
public interface IPayrollRunRepository
{
    /// <summary>Loads a run without its payslips (suited for status transitions that emit domain events).</summary>
    Task<PayrollRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Loads a run with payslips only (no lines) — suited for treasury payment flows.</summary>
    Task<PayrollRun?> GetByIdWithPayslipsForPaymentAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Loads a run with all its payslips and lines.</summary>
    Task<PayrollRun?> GetByIdWithPayslipsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PayrollRun?> GetByPeriodAsync(int year, int month, CancellationToken cancellationToken = default);

    /// <summary>Loads a single payslip (with its lines).</summary>
    Task<Payslip?> GetPayslipByIdAsync(Guid payslipId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PayrollRun>> ListAsync(int? year = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs of a year with only the count of their payslips.
    /// </summary>
    /// <remarks>
    /// La liste des cycles n'a besoin que du nombre de bulletins. Le chemin naïf — charger chaque
    /// cycle avec ses bulletins <i>et leurs lignes</i> pour en compter les éléments — coûtait douze
    /// chargements complets pour afficher un exercice.
    /// </remarks>
    Task<IReadOnlyList<PayrollRunWithPayslipCount>> ListWithPayslipCountsAsync(
        int? year = null,
        CancellationToken cancellationToken = default);

    /// <summary>Runs whose period falls within a civil quarter (used for the DTS declaration).</summary>
    Task<IReadOnlyList<PayrollRun>> ListByQuarterWithPayslipsAsync(int year, int quarter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs of a month range with their payslips (payroll control reports — livre de paie).
    /// Draft runs are always excluded; calculated runs only when <paramref name="includeCalculated"/> is set.
    /// </summary>
    Task<IReadOnlyList<PayrollRun>> ListByMonthRangeWithPayslipsAsync(
        int year,
        int fromMonth,
        int toMonth,
        bool includeCalculated,
        CancellationToken cancellationToken = default);

    /// <summary>Run of a single month with its payslips but without their lines (journal de paie).</summary>
    Task<PayrollRun?> GetByPeriodWithPayslipsAsync(int year, int month, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulletins de l'exercice issus des cycles <b>Validés ou Clôturés</b> uniquement, pour le
    /// cumul annuel de la régularisation IRPP. Se limiter aux cycles arrêtés garantit que le
    /// cumul est stable d'un calcul à l'autre : un cycle encore modifiable ne doit pas faire
    /// varier une régularisation déjà proposée.
    /// </summary>
    Task<IReadOnlyList<Payslip>> ListSettledPayslipsForYearAsync(
        int year,
        int untilMonthExclusive,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulletins figés d'un salarié issus des cycles Validés ou Clôturés uniquement,
    /// triés par période croissante.
    /// </summary>
    Task<IReadOnlyList<Payslip>> ListSettledPayslipsForEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsForPeriodAsync(int year, int month, CancellationToken cancellationToken = default);

    /// <summary>True if a run for the period is validated or closed (locks monthly variable edits).</summary>
    Task<bool> HasValidatedOrClosedRunForMonthAsync(int year, int month, CancellationToken cancellationToken = default);

    /// <summary>Inserts a brand new (draft) run.</summary>
    Task<PayrollRun> AddAsync(PayrollRun run, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a calculated run: removes any previous payslips and inserts the freshly computed
    /// ones, updating the run totals and status.
    /// </summary>
    Task PersistCalculationAsync(PayrollRun run, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates run scalar fields only (status, validation/close markers). The passed instance
    /// carries any domain events, which are dispatched on save.
    /// </summary>
    Task UpdateScalarAsync(PayrollRun run, CancellationToken cancellationToken = default);

    /// <summary>
    /// R-15 : persiste les bulletins modifiés (figeage du compte auxiliaire 425 à la validation).
    /// Attache et marque chaque bulletin modifié — ciblé pour ne pas réécrire tout le cycle.
    /// </summary>
    Task UpdatePayslipsAsync(IReadOnlyCollection<Payslip> payslips, CancellationToken cancellationToken = default);
}
