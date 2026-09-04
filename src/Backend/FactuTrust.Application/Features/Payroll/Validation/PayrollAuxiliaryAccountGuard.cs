using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Features.Payroll.Validation;

/// <summary>
/// Refuse d'attribuer à un salarié un compte auxiliaire 425 déjà porté par un autre.
/// </summary>
/// <remarks>
/// <para>
/// Le contrôle portait auparavant sur le <b>matricule</b> : le compte s'en dérivait par troncature
/// aux 7 derniers chiffres, si bien que « 1 » et « 0000001 », ou deux CIN de même queue, aboutissaient
/// au même compte. Cette dérivation a été supprimée — elle produisait un numéro de 10 chiffres, au-delà
/// du plafond de 8 — au profit d'une allocation séquentielle, unique par construction.
/// </para>
/// <para>
/// Garder le contrôle sur le matricule refuserait désormais <b>à tort</b> deux salariés dont les
/// matricules se ressemblent alors que leurs comptes sont distincts. Il porte donc sur le compte
/// effectivement attribué, ce qui reste utile : les fiches reprises depuis un bulletin figé peuvent
/// hériter d'une collision née de l'ancienne troncature.
/// </para>
/// </remarks>
public static class PayrollAuxiliaryAccountGuard
{
    public static async Task<Result> EnsureAccountNotTakenAsync(
        IEmployeeRepository employees,
        string? auxiliaryAccountNumber,
        Guid? excludeEmployeeId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(auxiliaryAccountNumber))
            return Result.Success();

        var candidate = auxiliaryAccountNumber.Trim();

        var all = await employees.GetAllAsync(cancellationToken);
        var conflict = all.FirstOrDefault(e =>
            e.Id != excludeEmployeeId
            && !string.IsNullOrWhiteSpace(e.AuxiliaryAccountNumber)
            && string.Equals(e.AuxiliaryAccountNumber!.Trim(), candidate, StringComparison.Ordinal));

        if (conflict is null)
            return Result.Success();

        return Result.Failure(Error.Conflict(
            $"Le compte auxiliaire {candidate} est déjà attribué à « {conflict.EmployeeNumber} » "
            + $"({conflict.FullName}) : deux salariés sur un même compte confondraient leurs dettes de "
            + "salaire et rendraient le lettrage du règlement arbitraire."));
    }
}
