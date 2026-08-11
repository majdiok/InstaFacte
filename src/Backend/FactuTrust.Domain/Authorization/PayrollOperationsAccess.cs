using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Authorization;

/// <summary>
/// Accès aux opérations sensibles de paie : réservées au cabinet comptable en mode dossier délégué
/// lorsqu'une affectation cabinet active existe pour la société cliente.
/// </summary>
public static class PayrollOperationsAccess
{
    public const string WriteDeniedErrorCode = "PayrollOperations.FirmExclusive";

    public const string WriteDeniedMessage =
        "Seul le cabinet comptable, en mode dossier client, peut exécuter cette opération de paie.";

    private static readonly HashSet<string> FirmExclusivePermissions = new(StringComparer.Ordinal)
    {
        Permissions.Payroll.RunPayroll,
        Permissions.Payroll.Validate,
        Permissions.Payroll.Settings,
        Permissions.Payroll.Declare,
        Permissions.Payroll.Pay
    };

    public static IReadOnlyCollection<string> FirmExclusivePermissionList => FirmExclusivePermissions;

    public static bool IsFirmExclusivePermission(string permission) =>
        FirmExclusivePermissions.Contains(permission);

    /// <summary>
    /// True when firm-exclusive payroll operations must be executed by the delegated firm.
    /// </summary>
    public static bool RequiresFirmExclusiveExecution(bool hasActiveFirmAssignment) =>
        hasActiveFirmAssignment;

    /// <summary>
    /// True when the caller may execute firm-exclusive payroll operations.
    /// </summary>
    public static bool CanExecuteFirmExclusiveOperations(
        bool hasActiveFirmAssignment,
        bool isAccountingFirmDelegatedContext) =>
        !hasActiveFirmAssignment || isAccountingFirmDelegatedContext;

    public static IReadOnlyList<string> FilterCompanyPermissionsWhenFirmAssigned(
        IEnumerable<string> permissions) =>
        permissions.Where(p => !IsFirmExclusivePermission(p)).ToList();

    public static Error WriteDenied() =>
        new(WriteDeniedErrorCode, WriteDeniedMessage);
}
