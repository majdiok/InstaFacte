using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Configuration;

/// <summary>Accès natif cabinet (JWT modules + permissions paie interne).</summary>
public static class FirmGovernanceNativeAccess
{
    public static bool IsInternalPayrollEnabled(FirmGovernanceOptions options) =>
        options.Enabled && options.EnableFirmInternalPayroll;

    public static bool ShouldAutoProvisionPayrollOnCollaboratorCreate(FirmGovernanceOptions options) =>
        IsInternalPayrollEnabled(options) && options.AutoProvisionPayrollOnCollaboratorCreate;

    public static IReadOnlyList<int> BuildNativeFirmModuleIds(FirmGovernanceOptions options)
    {
        var modules = new List<int>
        {
            (int)AppModule.Administration,
            (int)AppModule.Honoraires
        };

        if (IsInternalPayrollEnabled(options))
            modules.Add((int)AppModule.Payroll);

        return modules;
    }

    /// <param name="accountingFirms">
    /// Optionnel pour rétro-compatibilité : null = comportement historique (paie interne seule),
    /// aucune permission d'agent cabinet ajoutée.
    /// </param>
    public static IReadOnlyList<string> AugmentNativeFirmPermissions(
        IReadOnlyList<string> current,
        UserRole role,
        FirmGovernanceOptions options,
        AccountingFirmsOptions? accountingFirms = null)
    {
        var extra = new List<string>();

        if (IsInternalPayrollEnabled(options))
        {
            extra.AddRange(role switch
            {
                UserRole.FirmManager => DelegatedPermissionCatalog.FirmNativePayrollManagerPermissions,
                UserRole.FirmAccountant => DelegatedPermissionCatalog.FirmNativePayrollAccountantPermissions,
                _ => Array.Empty<string>()
            });
        }

        extra.AddRange(BuildFirmAgentPermissions(role, accountingFirms));
        extra.AddRange(BuildFirmRevisionPermissions(role, accountingFirms));

        if (extra.Count == 0)
            return current;

        return current.Concat(extra).Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Permissions du réviseur de portefeuille. Responsable et collaborateur consultent tous deux
    /// — l'ACL dossier restreint ensuite chacun à ses affectations ; seul le responsable déclenche
    /// un balayage, qui mobilise toutes les bases dossiers du cabinet.
    /// </summary>
    private static IReadOnlyList<string> BuildFirmRevisionPermissions(
        UserRole role,
        AccountingFirmsOptions? accountingFirms)
    {
        if (accountingFirms is not { Enabled: true, FirmRevisionEnabled: true })
            return Array.Empty<string>();

        return role switch
        {
            UserRole.FirmManager => [Permissions.Firm.RevisionView, Permissions.Firm.RevisionManage],
            UserRole.FirmAccountant => [Permissions.Firm.RevisionView],
            _ => Array.Empty<string>()
        };
    }

    /// <summary>
    /// Permissions de l'agent « Chef de mission ». Le responsable et le collaborateur consultent
    /// tous deux (l'ACL dossier restreint ensuite ce que chacun voit) ; seul le responsable relance.
    /// </summary>
    private static IReadOnlyList<string> BuildFirmAgentPermissions(
        UserRole role,
        AccountingFirmsOptions? accountingFirms)
    {
        if (accountingFirms is not { Enabled: true, FirmAgentEnabled: true })
            return Array.Empty<string>();

        if (role is not (UserRole.FirmManager or UserRole.FirmAccountant))
            return Array.Empty<string>();

        if (role == UserRole.FirmManager && accountingFirms.FirmAgentReminderToolEnabled)
            return new[] { Permissions.Firm.AiChat, Permissions.Firm.AiRemind };

        return new[] { Permissions.Firm.AiChat };
    }
}
