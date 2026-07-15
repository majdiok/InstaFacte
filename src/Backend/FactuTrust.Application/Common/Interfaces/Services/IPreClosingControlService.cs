using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Contrôles de pré-clôture : batterie de vérifications de révision exécutée avant la clôture
/// annuelle et le verrouillage définitif d'un exercice. Piloté par
/// <c>AccountingSettings.PreClosingControlsEnabled</c>.
/// </summary>
public interface IPreClosingControlService
{
    /// <summary>Exécute tous les contrôles pour un exercice et renvoie la checklist détaillée.</summary>
    Task<Result<PreClosingChecklistDto>> RunAsync(int fiscalYear, CancellationToken cancellationToken = default);
}
