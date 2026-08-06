using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Payroll;
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
        string label,
        decimal totalIrppRegularization = 0m,
        decimal totalCssRegularization = 0m,
        decimal totalCssEmployer = 0m)
    {
        return BuildLines(
            totalGross,
            totalNet,
            totalCnssEmployee,
            totalCnssEmployer,
            totalIrpp,
            totalCss,
            totalTfp,
            totalFoprolos,
            totalWorkAccident,
            totalOtherDeductions,
            label,
            employeeAuxiliaryCredits: null,
            totalIrppRegularization,
            totalCssRegularization,
            totalCssEmployer);
    }

    /// <summary>
    /// Variante avec ventilation du crédit 421 par salarié (comptes auxiliaires).
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
        string label,
        IReadOnlyList<EmployeeAuxiliaryCredit>? employeeAuxiliaryCredits,
        decimal totalIrppRegularization = 0m,
        decimal totalCssRegularization = 0m,
        decimal totalCssEmployer = 0m)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        var employerCharges = R(totalCnssEmployer + totalTfp + totalFoprolos + totalWorkAccident + totalCssEmployer);
        var stateWithholding = R(
            totalIrpp + totalCss + totalTfp + totalFoprolos + totalCssEmployer
            + totalIrppRegularization + totalCssRegularization);
        var socialOrg = R(totalCnssEmployee + totalCnssEmployer + totalWorkAccident);
        var otherDeductions = R(totalOtherDeductions);
        var gross = R(totalGross);
        var net = R(totalNet);
        var entryLabel = label.Trim();
        var advancesLabel = $"Avances et autres retenues — {entryLabel}";

        var lines = new List<JournalLineInput>();
        AddDebit(lines, SalaryAccount, entryLabel, gross);
        AddDebit(lines, EmployerChargesAccount, entryLabel, employerCharges);

        if (employeeAuxiliaryCredits is { Count: > 0 })
        {
            foreach (var credit in employeeAuxiliaryCredits.Where(c => c.Amount > 0))
            {
                lines.Add(new JournalLineInput(
                    credit.AuxiliaryAccount,
                    $"{entryLabel} — {credit.EmployeeName}",
                    0,
                    R(credit.Amount),
                    credit.EmployeeId,
                    ThirdPartyKind.Employee));
            }

            var auxiliaryTotal = R(employeeAuxiliaryCredits.Sum(c => c.Amount));
            if (Math.Abs(auxiliaryTotal - net) > 0.001m)
            {
                return Result.Failure<IReadOnlyList<JournalLineInput>>(Error.Validation(
                    "Lines",
                    "La ventilation des comptes 421 par salarié ne correspond pas au net total du cycle."));
            }
        }
        else
        {
            AddCredit(lines, PersonnelPayableAccount, entryLabel, net);
        }

        AddSigned(lines, StateWithholdingAccount, entryLabel, stateWithholding);
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

    /// <summary>
    /// Variante avec ventilation typée des retenues (prêts, saisies, mutuelle…) et charges patronales complémentaires.
    /// </summary>
    public static Result<IReadOnlyList<JournalLineInput>> BuildLinesFromRun(
        PayrollRun payrollRun,
        string label,
        PayrollJournalEntryAccountMap accountMap,
        IReadOnlyList<EmployeeAuxiliaryCredit>? employeeAuxiliaryCredits = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(payrollRun);
        ArgumentNullException.ThrowIfNull(accountMap);

        var extraEmployerCharges = payrollRun.Payslips
            .SelectMany(p => p.Lines)
            .Where(l => l.Kind == PayslipLineKind.EmployerContribution && !IsStandardEmployerLabel(l.Label))
            .Sum(l => l.Amount);

        var employerCharges = R(
            payrollRun.TotalCnssEmployer
            + payrollRun.TotalTfp
            + payrollRun.TotalFoprolos
            + payrollRun.TotalWorkAccident
            + payrollRun.TotalCssEmployer
            + extraEmployerCharges);

        // La régularisation annuelle transite par le même compte que la retenue mensuelle : le
        // bucket peut donc devenir négatif si les restitutions l'emportent (cf. AddSigned).
        var stateWithholding = R(
            payrollRun.TotalIrpp + payrollRun.TotalCss + payrollRun.TotalTfp + payrollRun.TotalFoprolos
            + payrollRun.TotalCssEmployer
            + payrollRun.TotalIrppRegularization + payrollRun.TotalCssRegularization);
        var socialOrg = R(payrollRun.TotalCnssEmployee + payrollRun.TotalCnssEmployer + payrollRun.TotalWorkAccident);
        var gross = R(payrollRun.TotalGross);
        var net = R(payrollRun.TotalNet);
        var entryLabel = label.Trim();

        var typedDeductions = AggregateTypedDeductions(payrollRun, accountMap);

        var lines = new List<JournalLineInput>();
        AddDebit(lines, SalaryAccount, entryLabel, gross);
        AddDebit(lines, EmployerChargesAccount, entryLabel, employerCharges);

        if (employeeAuxiliaryCredits is { Count: > 0 })
        {
            foreach (var credit in employeeAuxiliaryCredits.Where(c => c.Amount > 0))
            {
                lines.Add(new JournalLineInput(
                    credit.AuxiliaryAccount,
                    $"{entryLabel} — {credit.EmployeeName}",
                    0,
                    R(credit.Amount),
                    credit.EmployeeId,
                    ThirdPartyKind.Employee));
            }

            var auxiliaryTotal = R(employeeAuxiliaryCredits.Sum(c => c.Amount));
            if (Math.Abs(auxiliaryTotal - net) > 0.001m)
            {
                return Result.Failure<IReadOnlyList<JournalLineInput>>(Error.Validation(
                    "Lines",
                    "La ventilation des comptes 421 par salarié ne correspond pas au net total du cycle."));
            }
        }
        else
        {
            AddCredit(lines, PersonnelPayableAccount, entryLabel, net);
        }

        AddSigned(lines, StateWithholdingAccount, entryLabel, stateWithholding);
        AddCredit(lines, SocialOrgAccount, entryLabel, socialOrg);

        foreach (var bucket in typedDeductions.Where(b => b.Amount > 0))
            AddCredit(lines, bucket.Account, bucket.Label, bucket.Amount);

        if (lines.Count < 2)
        {
            return Result.Failure<IReadOnlyList<JournalLineInput>>(Error.Validation(
                "Lines",
                "Impossible de générer l'écriture de paie : au moins deux lignes non nulles sont requises."));
        }

        return Result.Success<IReadOnlyList<JournalLineInput>>(lines);
    }

    private static List<TypedDeductionBucket> AggregateTypedDeductions(PayrollRun payrollRun, PayrollJournalEntryAccountMap accountMap)
    {
        var buckets = new Dictionary<(string Account, string Label), decimal>();

        foreach (var payslip in payrollRun.Payslips)
        {
            foreach (var line in payslip.Lines.Where(l => l.Kind == PayslipLineKind.Deduction && l.Amount > 0))
            {
                if (line.DeductionKind is null)
                    continue;

                var kind = line.DeductionKind!.Value;
                if (kind is DeductionKind.Garnishment or DeductionKind.Alimony
                    or DeductionKind.MutuelleEmployee or DeductionKind.Loan
                    or DeductionKind.MealVoucherEmployeeShare or DeductionKind.InKindBenefitOffset
                    or DeductionKind.Advance or DeductionKind.Other)
                {
                    var account = accountMap.ResolveCreditAccount(kind);
                    var label = $"{line.Label} — Paie {payrollRun.Month:D2}/{payrollRun.Year}";
                    var key = (account, label);
                    buckets[key] = R(buckets.GetValueOrDefault(key) + line.Amount);
                }
            }
        }

        return buckets
            .Select(kv => new TypedDeductionBucket(kv.Key.Account, kv.Key.Label, kv.Value))
            .ToList();
    }

    private static bool IsStandardEmployerLabel(string label) =>
        label.StartsWith("CNSS patronale", StringComparison.Ordinal)
        || label.StartsWith("Accident de travail", StringComparison.Ordinal)
        || label.StartsWith("TFP", StringComparison.Ordinal)
        || label.StartsWith("FOPROLOS", StringComparison.Ordinal)
        || label.StartsWith("CSS patronale", StringComparison.Ordinal);

    private readonly record struct TypedDeductionBucket(string Account, string Label, decimal Amount);

    public readonly record struct EmployeeAuxiliaryCredit(
        Guid EmployeeId,
        string EmployeeName,
        string AuxiliaryAccount,
        decimal Amount);

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

    /// <summary>
    /// Impute un bucket dont le signe peut s'inverser : au crédit s'il est positif, au débit
    /// s'il est négatif. Indispensable pour le compte 432 — un mois de régularisation à
    /// dominante restitution rend la retenue globale négative, et <see cref="AddCredit"/>
    /// supprimerait alors la ligne en silence, déséquilibrant l'écriture.
    /// </summary>
    private static void AddSigned(List<JournalLineInput> lines, string account, string label, decimal amount)
    {
        if (amount > 0)
            AddCredit(lines, account, label, amount);
        else if (amount < 0)
            AddDebit(lines, account, label, -amount);
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
