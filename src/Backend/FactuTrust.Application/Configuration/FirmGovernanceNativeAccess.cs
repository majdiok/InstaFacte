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

    public static IReadOnlyList<string> AugmentNativeFirmPermissions(
        IReadOnlyList<string> current,
        UserRole role,
        FirmGovernanceOptions options)
    {
        if (!IsInternalPayrollEnabled(options))
            return current;

        var extra = role switch
        {
            UserRole.FirmManager => DelegatedPermissionCatalog.FirmNativePayrollManagerPermissions,
            UserRole.FirmAccountant => DelegatedPermissionCatalog.FirmNativePayrollAccountantPermissions,
            _ => Array.Empty<string>()
        };

        if (extra.Count == 0)
            return current;

        return current.Concat(extra).Distinct(StringComparer.Ordinal).ToList();
    }
}
