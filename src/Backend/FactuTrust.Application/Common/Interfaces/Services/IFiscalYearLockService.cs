using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Verrouillage définitif (irréversible) d'un exercice comptable. Piloté par
/// <c>AccountingSettings.DefinitiveLockEnabled</c>.
/// </summary>
public interface IFiscalYearLockService
{
    /// <summary>
    /// Verrouille définitivement un exercice : exige que toutes ses périodes soient clôturées et
    /// qu'aucun contrôle de pré-clôture bloquant ne subsiste. Opération irréversible.
    /// </summary>
    Task<Result> LockYearAsync(int fiscalYear, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<FiscalYearLockDto>>> GetLocksAsync(CancellationToken cancellationToken = default);
}
