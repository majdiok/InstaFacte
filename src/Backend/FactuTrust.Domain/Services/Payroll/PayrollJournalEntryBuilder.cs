using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Construit les lignes d'écriture OD de paie (comptes SCE) à partir des totaux d'un cycle.
/// Fonction pure : omet les buckets à montant nul pour respecter les invariants de
/// <see cref="JournalEntry"/>, et crédite le compte 425 pour les autres retenues (avances).
/// </summary>
public static class PayrollJournalEntryBuilder
{
    public const string SalaryAccount = "640";
    public const string EmployerChargesAccount = "647";
    public const string PersonnelPayableAccount = "421";
    public const string StateWithholdingAccount = "432";
    public const string SocialOrgAccount = "453";
    public const string AdvancesAccount = "425";

    /// <summary>
    /// Agrège les buckets paie et produit les lignes journal (montants strictement &gt; 0 uniquement).
    /// </summary>
    public static Result<IReadOnlyList<JournalLineInput>> BuildLines(
        decimal totalGross,
        decimal totalNet,
        decimal totalCnssEmployee,
        decimal totalCnssEmployer,
        decimal totalIrpp,
        decimal totalCss,
        decimal totalTfp,
        decimal totalFoprolos,
        decimal totalWorkAccident,
        decimal totalOtherDeductions,
        string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        var employerCharges = R(totalCnssEmployer + totalTfp + totalFoprolos + totalWorkAccident);
        var stateWithholding = R(totalIrpp + totalCss + totalTfp + totalFoprolos);
        var socialOrg = R(totalCnssEmployee + totalCnssEmployer + totalWorkAccident);
        var otherDeductions = R(totalOtherDeductions);
        var gross = R(totalGross);
        var net = R(totalNet);
        var entryLabel = label.Trim();
        var advancesLabel = $"Avances et autres retenues — {entryLabel}";

        var lines = new List<JournalLineInput>();
        AddDebit(lines, SalaryAccount, entryLabel, gross);
        AddDebit(lines, EmployerChargesAccount, entryLabel, employerCharges);
        AddCredit(lines, PersonnelPayableAccount, entryLabel, net);
        AddCredit(lines, StateWithholdingAccount, entryLabel, stateWithholding);
        AddCredit(lines, SocialOrgAccount, entryLabel, socialOrg);
        AddCredit(lines, AdvancesAccount, advancesLabel, otherDeductions);

        if (lines.Count < 2)
        {
            return Result.Failure<IReadOnlyList<JournalLineInput>>(Error.Validation(
                "Lines",
                "Impossible de générer l'écriture de paie : au moins deux lignes non nulles sont requises."));
        }

        return Result.Success<IReadOnlyList<JournalLineInput>>(lines);
    }

    private static void AddDebit(List<JournalLineInput> lines, string account, string label, decimal amount)
    {
        if (amount <= 0)
            return;
        lines.Add(new JournalLineInput(account, label, amount, 0, null, ThirdPartyKind.None));
    }

    private static void AddCredit(List<JournalLineInput> lines, string account, string label, decimal amount)
    {
        if (amount <= 0)
            return;
        lines.Add(new JournalLineInput(account, label, 0, amount, null, ThirdPartyKind.None));
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
