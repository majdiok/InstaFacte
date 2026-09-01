using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Services.Payroll;

namespace FactuTrust.Application.Features.Payroll.Validation;

/// <summary>
/// Refuse deux matricules aboutissant au même compte auxiliaire 425xxxx.
/// </summary>
/// <remarks>
/// Le compte n'emprunte que les 7 derniers chiffres du matricule : « 1 » et « 0000001 », ou deux
/// CIN aux 7 derniers chiffres identiques, produisent le même compte. Les deux salariés
/// partageraient alors une seule dette de salaire — soldes confondus, lettrage du règlement
/// arbitraire. Le contrôle est posé à la saisie du matricule, là où il reste corrigible sans
/// toucher à la comptabilité ; <c>PayrollRun.FreezeEmployeeAuxiliaryAccounts</c> le rejoue à la
/// validation pour les données antérieures à ce garde-fou.
/// </remarks>
public static class PayrollAuxiliaryAccountGuard
{
    public static async Task<Result> EnsureNoCollisionAsync(
        IEmployeeRepository employees,
        string employeeNumber,
        Guid? excludeEmployeeId,
        CancellationToken cancellationToken)
    {
        // Un matricule sans chiffre est rejeté plus tard, à la validation du cycle (message dédié) :
        // ici il ne peut entrer en collision avec rien.
        if (!PayrollEmployeeAuxiliaryAccountResolver.CanResolve(employeeNumber))
            return Result.Success();

        var candidate = PayrollEmployeeAuxiliaryAccountResolver.Resolve(employeeNumber);

        var all = await employees.GetAllAsync(cancellationToken);
        var conflict = all.FirstOrDefault(e =>
            e.Id != excludeEmployeeId
            && !string.IsNullOrWhiteSpace(e.EmployeeNumber)
            && PayrollEmployeeAuxiliaryAccountResolver.CanResolve(e.EmployeeNumber)
            && string.Equals(
                PayrollEmployeeAuxiliaryAccountResolver.Resolve(e.EmployeeNumber),
                candidate,
                StringComparison.Ordinal));

        if (conflict is null)
            return Result.Success();

        return Result.Failure(Error.Conflict(
            $"Le matricule « {employeeNumber} » produit le même compte auxiliaire {candidate} que "
            + $"« {conflict.EmployeeNumber} » ({conflict.FullName}) : leurs dettes de salaire se "
            + "confondraient en comptabilité. Différenciez les matricules sur leurs 7 derniers chiffres."));
    }
}
