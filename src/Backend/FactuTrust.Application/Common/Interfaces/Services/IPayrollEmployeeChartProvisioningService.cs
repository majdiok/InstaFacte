using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Alloue et crée le compte auxiliaire 425 d'un salarié dans le plan comptable.
/// </summary>
/// <remarks>
/// Pendant salarié de <see cref="IBankAccountChartProvisioningService"/> : même principe
/// d'allocation séquentielle sous un compte collectif, même typage auxiliaire, et un libellé qui
/// identifie le tiers. Il remplace la dérivation par troncature du matricule
/// (<c>PayrollEmployeeAuxiliaryAccountResolver</c>), qui reste le repli des salariés antérieurs.
/// </remarks>
public interface IPayrollEmployeeChartProvisioningService
{
    /// <summary>
    /// Crée le prochain compte auxiliaire libre sous 425 et le retourne.
    /// </summary>
    /// <param name="employeeFullName">Nom du salarié — devient le libellé du compte.</param>
    /// <returns>
    /// Le numéro alloué, ou un échec si le compte collectif 425 est absent du plan comptable.
    /// </returns>
    Task<Result<string>> AllocateAsync(
        string employeeFullName,
        CancellationToken cancellationToken = default);
}
