using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Alloue et crée le compte auxiliaire 425 d'un salarié dans le plan comptable.
/// </summary>
/// <remarks>
/// Pendant salarié de <see cref="IBankAccountChartProvisioningService"/> : même principe
/// d'allocation séquentielle sous un compte collectif, même typage auxiliaire, et un libellé qui
/// identifie le tiers. Il a <b>remplacé</b> la dérivation par troncature du matricule, supprimée du
/// chemin d'écriture : elle produisait un numéro de 10 chiffres et pouvait faire collisionner deux
/// salariés sur une même dette de salaire.
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
