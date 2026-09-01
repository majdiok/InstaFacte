using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Accès aux <see cref="PayrollAccountingSettings"/> du dossier (tenant) — singleton par tenant.
/// </summary>
public interface IPayrollAccountingSettingsRepository
{
    /// <summary>
    /// Réglage persisté du dossier, ou <c>null</c> s'il n'en porte pas. Le <c>null</c> est
    /// significatif : il signifie « repli sur la configuration globale », donc comportement
    /// historique inchangé pour tout dossier non paramétré.
    /// </summary>
    Task<PayrollAccountingSettings?> GetForTenantAsync(CancellationToken cancellationToken = default);

    /// <summary>Crée ou met à jour le réglage du dossier (upsert idempotent).</summary>
    Task<Result<PayrollAccountingSettings>> UpsertAsync(
        PayrollAccountProfile accountProfile,
        DateTime? accountProfileEffectiveDate,
        string? inKindOffsetAccount,
        bool disbursementEntriesEnabled,
        bool detailedSalarySplitEnabled,
        bool employeeAuxiliaryEnabled,
        string updatedBy,
        CancellationToken cancellationToken = default);
}
