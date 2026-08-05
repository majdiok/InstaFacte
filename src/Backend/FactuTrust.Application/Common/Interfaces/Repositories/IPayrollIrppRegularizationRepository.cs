using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Accès aux régularisations IRPP/CSS annuelles, saisies par salarié et par mois de paie.
/// </summary>
public interface IPayrollIrppRegularizationRepository
{
    Task<PayrollIrppRegularization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PayrollIrppRegularization?> GetByEmployeeAndMonthAsync(
        Guid employeeId,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PayrollIrppRegularization>> ListForMonthAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<PayrollIrppRegularization> AddAsync(PayrollIrppRegularization entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(PayrollIrppRegularization entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(PayrollIrppRegularization entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enregistre en une passe les créations et mises à jour issues d'une génération batch.
    /// </summary>
    Task SaveBatchAsync(
        IReadOnlyList<PayrollIrppRegularization> added,
        IReadOnlyList<PayrollIrppRegularization> updated,
        CancellationToken cancellationToken = default);
}
