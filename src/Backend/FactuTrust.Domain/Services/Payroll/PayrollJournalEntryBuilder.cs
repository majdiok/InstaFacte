using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Construit les lignes d'écriture OD de paie (comptes SCE) à partir des totaux d'un cycle.
/// Fonction pure : omet les buckets à montant nul pour respecter les invariants de
/// <see cref="JournalEntry"/>, et crédite le compte 421 pour les autres retenues (avances).
/// </summary>
public static class PayrollJournalEntryBuilder
{
    public const string SalaryAccount = "640";
    public const string IndemnityAccount = "641";
    public const string EmployerChargesAccount = "647";
    public const string PersonnelPayableAccount = "425";
    public const string StateWithholdingAccount = "432";
    public const string SocialOrgAccount = "453";
    public const string AdvancesAccount = "421";

    // ── Comptes SCE (profil Sce2026) — cf. plan §4.1.1. Les anciennes constantes ci-dessus
    //    sont conservées pour les chemins de lecture / repli Legacy. ──
    /// <summary>Charge TFP (débit) sous le profil SCE.</summary>
    public const string TfpExpenseAccount = "6611";
    /// <summary>Charge FOPROLOS (débit) sous le profil SCE.</summary>
    public const string FoprolosExpenseAccount = "6612";
    /// <summary>Dette TFP + FOPROLOS + CSS patronale (crédit 437) sous le profil SCE.</summary>
    public const string PayrollTaxesPayableAccount = "437";
    /// <summary>Indemnités de préavis et de licenciement / gratification de fin de service (débit).</summary>
    public const string TerminationIndemnityAccount = "64602";
    /// <summary>Avantages en nature (débit 6404) sous le profil SCE.</summary>
    public const string InKindBenefitExpenseAccount = "6404";
    /// <summary>Compensation avantage en nature (crédit clearing) — défaut doctrinal 4386.</summary>
    public const string InKindBenefitOffsetPayableAccount = "4386";
    /// <summary>Organismes sociaux - charges à payer (dette patronale fonds sociaux/mutuelle).</summary>
    public const string SocialFundEmployerPayableAccount = "4538";

    /// <summary>
    /// Agrège les buckets paie et produit les lignes journal (montants strictement &gt; 0 uniquement).
    /// Chemin agrégé (Legacy) : reproduit la cartographie historique. Sous <see cref="PayrollAccountProfile.Sce2026"/>,
    /// applique la ventilation SCE (TFP→6611, FOPROLOS→6612, taxes→437, indemnités de rupture→64602).
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
        decimal totalCssEmployer = 0m,
        decimal totalTerminationIndemnities = 0m,
        decimal totalInKindBenefits = 0m,
        PayrollAccountProfile profile = PayrollAccountProfile.Legacy)
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
            totalCssEmployer,
            totalTerminationIndemnities,
            totalInKindBenefits,
            profile);
    }

    /// <summary>
    /// Variante avec ventilation du crédit 425 par salarié (comptes auxiliaires).
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
        decimal totalCssEmployer = 0m,
        decimal totalTerminationIndemnities = 0m,
        decimal totalInKindBenefits = 0m,
        PayrollAccountProfile profile = PayrollAccountProfile.Legacy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        var gross = R(totalGross);
        var net = R(totalNet);
        var otherDeductions = R(totalOtherDeductions);
        var entryLabel = label.Trim();
        var advancesLabel = $"Avances et autres retenues — {entryLabel}";

        // Sous SCE, les taxes sur salaires (TFP, FOPROLOS, CSS patronale) sortent du bucket 647/432
        // vers 6611/6612 (charges) et 437 (dette). En Legacy, tout reste dans 647/432 (historique).
        var tfp = R(totalTfp);
        var foprolos = R(totalFoprolos);
        var cssEmployer = R(totalCssEmployer);
        var cnssEmployer = R(totalCnssEmployer);
        var workAccident = R(totalWorkAccident);

        var employerCharges = profile == PayrollAccountProfile.Sce2026
            ? R(cnssEmployer + workAccident + cssEmployer)
            : R(cnssEmployer + tfp + foprolos + workAccident + cssEmployer);

        var stateWithholding = profile == PayrollAccountProfile.Sce2026
            ? R(totalIrpp + totalCss + totalIrppRegularization + totalCssRegularization)
            : R(totalIrpp + totalCss + tfp + foprolos + cssEmployer + totalIrppRegularization + totalCssRegularization);

        var payrollTaxesPayable = profile == PayrollAccountProfile.Sce2026
            ? R(tfp + foprolos + cssEmployer)
            : 0m;

        var socialOrg = R(totalCnssEmployee + cnssEmployer + workAccident);

        // Indemnités de rupture : 641 en Legacy, 64602 en SCE. Les indemnités ordinaires restent
        // dans le brut (640) — seul le libellé « Indemnité » (rupture) est isolé ici. Les avantages
        // en nature (gain imposable) sortent du 640 vers 6404 sous SCE (le clearing 4386 est porté
        // par la retenue typée InKindBenefitOffset ; en Legacy l'AN reste dans le brut au 640).
        var terminationIndemnities = R(Math.Min(totalTerminationIndemnities, gross));
        var inKindBenefits = profile == PayrollAccountProfile.Sce2026
            ? R(Math.Min(totalInKindBenefits, gross - terminationIndemnities))
            : 0m;
        var salaries = R(gross - terminationIndemnities - inKindBenefits);
        var indemnityAccount = profile == PayrollAccountProfile.Sce2026
            ? TerminationIndemnityAccount
            : IndemnityAccount;

        var lines = new List<JournalLineInput>();
        AddDebit(lines, SalaryAccount, entryLabel, salaries);
        AddDebit(lines, indemnityAccount, entryLabel, terminationIndemnities);
        AddDebit(lines, InKindBenefitExpenseAccount, entryLabel, inKindBenefits);

        if (profile == PayrollAccountProfile.Sce2026)
        {
            AddDebit(lines, TfpExpenseAccount, entryLabel, tfp);
            AddDebit(lines, FoprolosExpenseAccount, entryLabel, foprolos);
        }

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
            if (auxiliaryTotal != net)
            {
                return Result.Failure<IReadOnlyList<JournalLineInput>>(Error.Validation(
                    "Lines",
                    $"La ventilation des comptes 425 par salarié ({auxiliaryTotal:N3}) ne correspond pas au net total du cycle ({net:N3})."));
            }
        }
        else
        {
            AddCredit(lines, PersonnelPayableAccount, entryLabel, net);
        }

        AddSigned(lines, StateWithholdingAccount, entryLabel, stateWithholding);
        AddCredit(lines, SocialOrgAccount, entryLabel, socialOrg);
        AddCredit(lines, PayrollTaxesPayableAccount, entryLabel, payrollTaxesPayable);
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
    /// Variante avec ventilation typée des retenues (prêts, saisies, mutuelle…) et charges patronales
    /// complémentaires. Profil par défaut <see cref="PayrollAccountProfile.Sce2026"/> : ce chemin sert
    /// aux cycles récents (lignes typées <see cref="DeductionKind"/>/<see cref="EarningKind"/>).
    /// </summary>
    public static Result<IReadOnlyList<JournalLineInput>> BuildLinesFromRun(
        PayrollRun payrollRun,
        string label,
        PayrollJournalEntryAccountMap accountMap,
        IReadOnlyList<EmployeeAuxiliaryCredit>? employeeAuxiliaryCredits = null,
        PayrollAccountProfile profile = PayrollAccountProfile.Sce2026)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(payrollRun);
        ArgumentNullException.ThrowIfNull(accountMap);

        var gross = R(payrollRun.TotalGross);
        var net = R(payrollRun.TotalNet);
        var entryLabel = label.Trim();

        var tfp = R(payrollRun.TotalTfp);
        var foprolos = R(payrollRun.TotalFoprolos);
        var cssEmployer = R(payrollRun.TotalCssEmployer);
        var cnssEmployer = R(payrollRun.TotalCnssEmployer);
        var workAccident = R(payrollRun.TotalWorkAccident);

        // Charges patronales complémentaires (fonds sociaux / mutuelle, part employeur) : agrégées
        // par (compte SCE, libellé). Le compte figé sur la ligne (R-14) prime, sinon 4538 (R-01).
        // Un crédit homogène est émis quel que soit le profil : sans lui l'écriture serait
        // déséquilibrée du montant de la part employeur (R-01).
        var extraEmployerCredits = AggregateExtraEmployerCharges(payrollRun, accountMap);
        var extraEmployerCharges = R(extraEmployerCredits.Sum(b => b.Amount));

        var employerCharges = profile == PayrollAccountProfile.Sce2026
            ? R(cnssEmployer + workAccident + cssEmployer + extraEmployerCharges)
            : R(cnssEmployer + tfp + foprolos + workAccident + cssEmployer + extraEmployerCharges);

        // La régularisation annuelle transite par le même compte que la retenue mensuelle : le
        // bucket peut donc devenir négatif si les restitutions l'emportent (cf. AddSigned).
        var stateWithholding = profile == PayrollAccountProfile.Sce2026
            ? R(payrollRun.TotalIrpp + payrollRun.TotalCss + payrollRun.TotalIrppRegularization + payrollRun.TotalCssRegularization)
            : R(payrollRun.TotalIrpp + payrollRun.TotalCss + tfp + foprolos + cssEmployer
                + payrollRun.TotalIrppRegularization + payrollRun.TotalCssRegularization);

        var payrollTaxesPayable = profile == PayrollAccountProfile.Sce2026
            ? R(tfp + foprolos + cssEmployer)
            : 0m;

        var socialOrg = R(payrollRun.TotalCnssEmployee + cnssEmployer + workAccident);

        // Ventilation des gains par nature (R-04/R-05). EarningKind figé au calcul fait foi ;
        // repli par préfixe de libellé uniquement pour les bulletins antérieurs (EarningKind null).
        var terminationIndemnities = R(Math.Min(ResolveTerminationIndemnities(payrollRun, profile), gross));
        var inKindBenefits = profile == PayrollAccountProfile.Sce2026
            ? R(Math.Min(ResolveInKindBenefits(payrollRun), gross - terminationIndemnities))
            : 0m;
        var salaries = R(gross - terminationIndemnities - inKindBenefits);

        var typedDeductions = AggregateTypedDeductions(payrollRun, accountMap);

        var lines = new List<JournalLineInput>();
        AddDebit(lines, SalaryAccount, entryLabel, salaries);
        AddDebit(lines, IndemnityAccount, entryLabel, profile == PayrollAccountProfile.Sce2026 ? 0m : terminationIndemnities);
        AddDebit(lines, TerminationIndemnityAccount, entryLabel, profile == PayrollAccountProfile.Sce2026 ? terminationIndemnities : 0m);
        AddDebit(lines, InKindBenefitExpenseAccount, entryLabel, inKindBenefits);

        if (profile == PayrollAccountProfile.Sce2026)
        {
            AddDebit(lines, TfpExpenseAccount, entryLabel, tfp);
            AddDebit(lines, FoprolosExpenseAccount, entryLabel, foprolos);
        }

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
            if (auxiliaryTotal != net)
            {
                return Result.Failure<IReadOnlyList<JournalLineInput>>(Error.Validation(
                    "Lines",
                    $"La ventilation des comptes 425 par salarié ({auxiliaryTotal:N3}) ne correspond pas au net total du cycle ({net:N3})."));
            }
        }
        else
        {
            AddCredit(lines, PersonnelPayableAccount, entryLabel, net);
        }

        AddSigned(lines, StateWithholdingAccount, entryLabel, stateWithholding);
        AddCredit(lines, SocialOrgAccount, entryLabel, socialOrg);
        AddCredit(lines, PayrollTaxesPayableAccount, entryLabel, payrollTaxesPayable);

        foreach (var bucket in typedDeductions.Where(b => b.Amount > 0))
            AddCredit(lines, bucket.Account, bucket.Label, bucket.Amount);

        // Crédits de la dette patronale des fonds sociaux (R-01/R-14) — émis quel que soit le profil.
        foreach (var bucket in extraEmployerCredits.Where(b => b.Amount > 0))
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
                    // Compte du régime figé sur la ligne (R-14) : passé à ResolveCreditAccount pour
                    // la mutuelle et la compensation d'avantage en nature.
                    var account = accountMap.ResolveCreditAccount(kind, line.AccountSce);
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

    /// <summary>
    /// Agrège les charges patronales complémentaires (fonds sociaux / mutuelle, part employeur) par
    /// (compte SCE, libellé). Le compte figé sur la ligne (<see cref="PayslipLine.AccountSce"/>) prime,
    /// sinon le compte d'attente 4538. Les libellés standard (CNSS pat, AT, TFP, FOPROLOS, CSS pat)
    /// sont exclus — ils sont déjà dans les totaux du cycle.
    /// </summary>
    private static List<TypedDeductionBucket> AggregateExtraEmployerCharges(PayrollRun payrollRun, PayrollJournalEntryAccountMap accountMap)
    {
        var buckets = new Dictionary<(string Account, string Label), decimal>();
        var periodSuffix = $"Paie {payrollRun.Month:D2}/{payrollRun.Year}";

        foreach (var line in payrollRun.Payslips
                     .SelectMany(p => p.Lines)
                     .Where(l => l.Kind == PayslipLineKind.EmployerContribution
                                 && !IsStandardEmployerLabel(l.Label)
                                 && l.Amount > 0))
        {
            var account = string.IsNullOrWhiteSpace(line.AccountSce)
                ? accountMap.SocialFundEmployerPayableAccount
                : line.AccountSce!;
            var label = $"{line.Label} — {periodSuffix}";
            var key = (account, label);
            buckets[key] = R(buckets.GetValueOrDefault(key) + line.Amount);
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

    /// <summary>
    /// Somme des indemnités de rupture. Sous SCE, <see cref="EarningKind.TerminationIndemnity"/> fait
    /// foi ; repli par préfixe « Indemnité » uniquement pour les bulletins antérieurs
    /// (<see cref="PayslipLine.EarningKind"/> null). En Legacy, le préfixe seul est utilisé (historique).
    /// </summary>
    public static decimal ResolveTerminationIndemnities(PayrollRun payrollRun, PayrollAccountProfile profile) =>
        payrollRun.Payslips
            .SelectMany(p => p.Lines)
            .Where(l => l.Kind == PayslipLineKind.Earning && IsTerminationIndemnity(l, profile))
            .Sum(l => l.Amount);

    private static bool IsTerminationIndemnity(PayslipLine line, PayrollAccountProfile profile)
    {
        if (profile == PayrollAccountProfile.Sce2026)
        {
            if (line.EarningKind == EarningKind.TerminationIndemnity)
                return true;
            // Repli legacy : bulletins antérieurs à l'introduction d'EarningKind.
            return line.EarningKind is null
                   && line.Label.StartsWith("Indemnité", StringComparison.Ordinal);
        }

        return line.Label.StartsWith("Indemnité", StringComparison.Ordinal);
    }

    /// <summary>Somme des avantages en nature (gain imposable) — débit 6404 sous SCE.</summary>
    public static decimal ResolveInKindBenefits(PayrollRun payrollRun) =>
        payrollRun.Payslips
            .SelectMany(p => p.Lines)
            .Where(l => l.Kind == PayslipLineKind.Earning && IsInKindBenefit(l))
            .Sum(l => l.Amount);

    /// <summary>
    /// Somme des compensations d'avantage en nature (retenue salarié typée
    /// <see cref="DeductionKind.InKindBenefitOffset"/>) — crédit clearing (4386 sous SCE, 421 en Legacy).
    /// Sert au reclassement à isoler la part AN du 421 des avances (R-09/M1).
    /// </summary>
    public static decimal ResolveInKindBenefitOffset(PayrollRun payrollRun) =>
        payrollRun.Payslips
            .SelectMany(p => p.Lines)
            .Where(l => l.Kind == PayslipLineKind.Deduction && l.DeductionKind == DeductionKind.InKindBenefitOffset)
            .Sum(l => l.Amount);

    private static bool IsInKindBenefit(PayslipLine line) =>
        line.EarningKind == EarningKind.InKindBenefit
        || (line.EarningKind is null
            && line.Label.StartsWith("Avantage en nature", StringComparison.Ordinal));

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
