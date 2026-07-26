using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Centre de contrôle d'intégrité comptable (LECTURE SEULE). Exécute à la demande, hors du contexte
/// de clôture, les contrôles de pré-clôture partagés plus des contrôles d'anomalies structurelles.
/// Ne mute JAMAIS de données — uniquement des diagnostics avec liens de résolution.
/// Piloté par <c>AccountingSettings.AccountingHealthEnabled</c>.
/// </summary>
public interface IAccountingHealthService
{
    /// <summary>
    /// Exécute tous les contrôles. <paramref name="fiscalYear"/> null = diagnostic sur tout
    /// l'historique (les contrôles de pré-clôture par exercice sont alors omis).
    /// </summary>
    Task<Result<AccountingHealthReportDto>> RunAsync(int? fiscalYear, CancellationToken cancellationToken = default);
}
