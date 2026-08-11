using FactuTrust.Application.Common.Interfaces;

namespace FactuTrust.Application.Configuration;

/// <summary>
/// Détermine si la validation d'un cycle paie cabinet doit déclencher l'import des coûts collaborateurs.
/// </summary>
public static class FirmPayrollValidateCostSyncPolicy
{
    public static bool ShouldSyncAfterValidate(FirmGovernanceOptions options, ICurrentUser currentUser) =>
        options.AutoImportOnPayrollValidate
        && !currentUser.IsAccountingFirmDelegatedContext
        && currentUser.TenantId.HasValue;
}
