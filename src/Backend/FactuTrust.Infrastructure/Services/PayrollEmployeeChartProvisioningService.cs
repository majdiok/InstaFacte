using System.Globalization;

using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Accounting;
using FactuTrust.Infrastructure.MultiTenancy;

using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Alloue le compte auxiliaire 425 d'un salarié, calqué sur
/// <see cref="BankAccountChartProvisioningService"/>.
/// </summary>
public sealed class PayrollEmployeeChartProvisioningService : IPayrollEmployeeChartProvisioningService
{
    /// <summary>Compte collectif « Personnel - rémunérations dues » (NCT 01).</summary>
    private const string CollectiveAccount = Employee.PersonnelPayableCollectiveAccount;

    /// <summary>
    /// Longueur du suffixe séquentiel : <c>425</c> + 4 chiffres = 7 caractères, sous le plafond de
    /// <see cref="AccountNumberRules.MaxDigits"/> chiffres, et 9 999 salariés par dossier.
    /// </summary>
    private const int SuffixLength = 4;

    private readonly ITenantDbContextFactory _contextFactory;

    public PayrollEmployeeChartProvisioningService(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Result<string>> AllocateAsync(
        string employeeFullName,
        CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();

        var collective = await ctx.ChartOfAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.AccountNumber == CollectiveAccount, cancellationToken);

        if (collective is null || !collective.IsActive)
        {
            return Result.Failure<string>(Error.Validation(
                "ChartOfAccount",
                $"Le compte {CollectiveAccount} « Personnel - rémunérations dues » est absent du plan "
                + "comptable : impossible d'allouer un compte auxiliaire salarié."));
        }

        var taken = (await ctx.ChartOfAccounts.AsNoTracking()
                .Where(a => a.AccountNumber.StartsWith(CollectiveAccount))
                .Select(a => a.AccountNumber)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var number = AllocateNextNumber(taken);
        if (number is null)
        {
            return Result.Failure<string>(Error.Validation(
                "AuxiliaryAccountNumber",
                $"Plus aucun compte auxiliaire libre sous {CollectiveAccount} "
                + $"({SuffixLength} chiffres épuisés) : élargissez la numérotation."));
        }

        var label = string.IsNullOrWhiteSpace(employeeFullName)
            ? $"{collective.Label} — {number}"
            : employeeFullName.Trim();

        var create = ChartOfAccount.Create(
            number,
            label,
            collective.AccountClass,
            CollectiveAccount,
            collective.NatureType,
            isSystem: false,
            accountType: AccountType.Other,
            isAuxiliary: true,
            affectationAccountNumber: CollectiveAccount);

        if (create.IsFailure)
            return Result.Failure<string>(create.Error);

        create.Value.SetAuditInfo("system", false);
        ctx.ChartOfAccounts.Add(create.Value);
        await ctx.SaveChangesAsync(cancellationToken);

        return Result.Success(number);
    }

    /// <summary>
    /// Premier suffixe séquentiel libre. On repart systématiquement de 1 en sautant les numéros
    /// pris, plutôt que de prendre « le plus grand + 1 » : la renumérotation des comptes hérités
    /// (<c>ChartAccountDigitCompactionService</c>) attribue ses cibles dans le même espace, et un
    /// « max + 1 » sauterait inutilement les numéros qu'elle a libérés.
    /// </summary>
    private static string? AllocateNextNumber(IReadOnlySet<string> taken)
    {
        var max = (int)Math.Pow(10, SuffixLength) - 1;

        for (var i = 1; i <= max; i++)
        {
            var candidate = CollectiveAccount
                + i.ToString(new string('0', SuffixLength), CultureInfo.InvariantCulture);

            if (!taken.Contains(candidate))
                return candidate;
        }

        return null;
    }
}
