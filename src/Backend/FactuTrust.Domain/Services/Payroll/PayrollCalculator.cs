using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Moteur de calcul de paie tunisien — fonction pure et déterministe (sans dépendance base
/// de données ni infrastructure). Applique, dans l'ordre : brut, CNSS salariale, frais
/// professionnels, déductions familiales, IRPP (barème progressif annualisé), CSS salariale, puis le
/// net à payer et les charges patronales (CNSS, accident de travail, TFP, FOPROLOS, CSS patronale).
///
/// Tous les taux et déductions proviennent de <see cref="PayrollYearParameters"/> : aucune
/// valeur légale n'est codée en dur ici.
/// </summary>
public static class PayrollCalculator
{
    /// <summary>
    /// Calcule un bulletin à partir des variables du mois et des paramètres de l'exercice.
    /// Les montants monétaires sont arrondis à 3 décimales (millimes).
    /// </summary>
    public static PayrollComputation Compute(PayrollComputationInput input, PayrollYearParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(parameters);

        // 1. Brut soumis à cotisation / imposition.
        var grossDeductions = input.UnpaidAbsenceAmount + input.ProrataDeductionAmount + input.SickLeaveDeductionAmount;
        var statutoryNonTaxable = input.SickLeaveTopUpAmount + input.SickLeaveSubrogationAmount
            + input.MaternityTopUpAmount + input.PaternityMaintenanceAmount;
        var cnssableGross = R(
            input.BaseSalary
            + input.TaxableCnssableAllowances
            + input.CnssOnlyAllowances
            + input.InKindTaxableCnssableBenefits
            + input.OvertimeAmount
            - grossDeductions);
        if (cnssableGross < 0) cnssableGross = 0m;

        var taxableGross = R(
            input.BaseSalary
            + input.TaxableCnssableAllowances
            + input.TaxableOnlyAllowances
            + input.InKindTaxableCnssableBenefits
            + input.OvertimeAmount
            - grossDeductions);
        if (taxableGross < 0) taxableGross = 0m;

        // Brut total affiché (inclut les éléments non soumis, ex. transport dans les limites légales).
        var totalGross = R(cnssableGross + input.TaxableOnlyAllowances + input.NonTaxableAllowances + statutoryNonTaxable);

        // 2. CNSS salariale (assiette plafonnée si paramétrée).
        var (cnssEmployeeRate, cnssEmployerRate) = ResolveCnssRates(input.Regime, parameters);
        var cnssContributionBase = ApplyCnssCeiling(cnssableGross, parameters);
        var cnssEmployee = R(cnssContributionBase * cnssEmployeeRate / 100m);

        // 3. Base imposable après CNSS.
        var baseAfterCnss = R(taxableGross - cnssEmployee);

        // 4. Frais professionnels (mensualisés, plafonnés).
        var monthlyCap = R(parameters.ProfessionalExpensesAnnualCap / 12m);
        var professionalExpenses = R(baseAfterCnss * parameters.ProfessionalExpensesRate / 100m);
        var professionalExpensesCapped = professionalExpenses > monthlyCap;
        if (professionalExpensesCapped)
            professionalExpenses = monthlyCap;

        // 5. Déductions familiales (mensualisées) — art. 40 du code de l'IRPP.
        var familyDeductions = ComputeMonthlyFamilyDeductions(input, parameters, baseAfterCnss, professionalExpenses);

        // 6. Net imposable mensuel puis annualisation pour le barème progressif.
        var monthlyNetTaxable = baseAfterCnss - professionalExpenses - familyDeductions;
        if (monthlyNetTaxable < 0) monthlyNetTaxable = 0m;
        monthlyNetTaxable = R(monthlyNetTaxable);

        var annualNetTaxable = R(monthlyNetTaxable * 12m);

        var annualIrpp = ComputeProgressiveTax(annualNetTaxable, parameters);
        var irppBeforeExemption = R(annualIrpp / 12m);

        var smigExemptionResult = SmigIrppExemptionCalculator.ApplyMonthly(
            irppBeforeExemption, monthlyNetTaxable, input.BaseSalary, parameters);
        var irpp = smigExemptionResult.IrppFinal;
        var smigExemption = smigExemptionResult.ExemptionAmount;

        // 7. CSS : exonérée si le revenu annuel imposable reste dans la tranche exonérée.
        decimal css = 0m;
        if (annualNetTaxable > parameters.CssAnnualExemptionThreshold)
        {
            var annualCss = R(annualNetTaxable * parameters.CssRate / 100m);
            css = R(annualCss / 12m);
        }

        // 8. Net à payer (retenues pré-impôt, régularisation annuelle, puis retenues post-impôt).
        var preTaxDeductions = ResolvePreTaxDeductions(input);
        var postTaxDeductions = R(input.PostTaxDeductionLines.Sum(l => l.Amount));

        var netBeforeRegularization = R(
            cnssableGross
            + input.TaxableOnlyAllowances
            + input.NonTaxableAllowances
            + statutoryNonTaxable
            - cnssEmployee
            - irpp
            - css
            - preTaxDeductions);
        if (netBeforeRegularization < 0) netBeforeRegularization = 0m;

        // Régularisation IRPP/CSS annuelle (décembre ou solde de tout compte) : le rappel est
        // écrêté au net disponible, la restitution augmente le net et n'est jamais écrêtée.
        // Hors mois de régularisation les deux montants valent zéro et le net reste identique.
        var regularization = ApplyRegularizationCap(
            input.IrppRegularization, input.CssRegularization, netBeforeRegularization);

        var netBeforePostTax = R(netBeforeRegularization - regularization.Irpp - regularization.Css);
        if (netBeforePostTax < 0) netBeforePostTax = 0m;

        var netSalary = R(netBeforePostTax - postTaxDeductions);
        if (netSalary < 0) netSalary = 0m;

        // 9. Charges patronales (hors net à payer).
        var cnssEmployer = R(cnssContributionBase * cnssEmployerRate / 100m);
        // L'assurance accident de travail est une branche CNSS : exonérée si le régime l'est.
        var workAccidentRate = input.Regime.IsSubjectToCnss() ? input.WorkAccidentRate : 0m;
        var accidentBase = ApplyAccidentCeiling(cnssContributionBase, parameters);
        var workAccident = R(accidentBase * workAccidentRate / 100m);
        var tfpRate = input.IsIndustrialSector ? parameters.TfpRateIndustry : parameters.TfpRateOther;
        var tfp = R(cnssContributionBase * tfpRate / 100m);
        var foprolos = R(cnssContributionBase * parameters.FoprolosRate / 100m);
        var cssEmployer = R(cnssContributionBase * parameters.CssEmployerRate / 100m);

        var lines = BuildLines(
            input, parameters, cnssEmployeeRate, cnssEmployerRate, tfpRate,
            cnssableGross, cnssContributionBase, accidentBase, cnssEmployee, baseAfterCnss,
            professionalExpenses, professionalExpensesCapped, familyDeductions,
            monthlyNetTaxable, irpp, css, smigExemption, regularization.Irpp, regularization.Css,
            preTaxDeductions, postTaxDeductions,
            cnssEmployer, workAccident, tfp, foprolos, cssEmployer);

        return new PayrollComputation
        {
            GrossSalary = totalGross,
            CnssableGross = cnssableGross,
            CnssEmployee = cnssEmployee,
            TaxableBaseAfterCnss = baseAfterCnss,
            ProfessionalExpenses = professionalExpenses,
            FamilyDeductions = familyDeductions,
            MonthlyNetTaxable = monthlyNetTaxable,
            AnnualNetTaxable = annualNetTaxable,
            Irpp = irpp,
            Css = css,
            IrppBeforeSmigExemption = irppBeforeExemption,
            IrppSmigExemption = smigExemption,
            OtherDeductions = R(preTaxDeductions + postTaxDeductions),
            NonTaxableAllowances = R(input.NonTaxableAllowances + statutoryNonTaxable),
            IrppRegularization = regularization.Irpp,
            CssRegularization = regularization.Css,
            RegularizationDeferred = regularization.Deferred,
            IsRegularizationCapped = regularization.IsCapped,
            NetSalary = netSalary,
            CnssEmployer = cnssEmployer,
            WorkAccidentContribution = workAccident,
            Tfp = tfp,
            Foprolos = foprolos,
            CssEmployer = cssEmployer,
            Lines = lines
        };
    }

    /// <summary>
    /// Applique le barème IRPP progressif au revenu net annuel imposable.
    /// Chaque tranche est taxée à son propre taux marginal.
    /// </summary>
    public static decimal ComputeProgressiveTax(decimal annualTaxableIncome, PayrollYearParameters parameters)
    {
        if (annualTaxableIncome <= 0)
            return 0m;

        var brackets = parameters.IrppBrackets.OrderBy(b => b.LowerBound).ToList();
        if (brackets.Count == 0)
            return 0m;

        decimal tax = 0m;
        for (var i = 0; i < brackets.Count; i++)
        {
            var lower = brackets[i].LowerBound;
            var upper = i + 1 < brackets.Count ? brackets[i + 1].LowerBound : decimal.MaxValue;

            if (annualTaxableIncome <= lower)
                break;

            var taxableInBracket = Math.Min(annualTaxableIncome, upper) - lower;
            if (taxableInBracket <= 0)
                continue;

            tax += taxableInBracket * brackets[i].Rate / 100m;
        }

        return R(tax);
    }

    /// <summary>
    /// Déductions familiales annuelles mensualisées : chef de famille, enfants ordinaires
    /// (plafond de rang, consommé d'abord par les étudiants à déduction majorée), enfants
    /// infirmes (hors plafond), parents à charge (% du revenu net annuel, plafonné par parent).
    /// </summary>
    private static decimal ComputeMonthlyFamilyDeductions(
        PayrollComputationInput input,
        PayrollYearParameters parameters,
        decimal baseAfterCnss,
        decimal professionalExpenses)
    {
        var annual = input.IsHeadOfFamily ? parameters.HeadOfFamilyAnnualDeduction : 0m;

        var disabled = Math.Max(0, input.DisabledChildren);
        annual += disabled * parameters.DisabledChildAnnualDeduction;

        var cap = Math.Max(0, parameters.MaxDeductibleChildren);
        var students = Math.Min(Math.Max(0, input.StudentChildren), cap);
        annual += students * parameters.StudentChildAnnualDeduction;

        var ordinary = Math.Max(0, input.DependentChildren - input.StudentChildren - disabled);
        var ordinaryCounted = Math.Min(ordinary, cap - students);
        annual += ordinaryCounted * parameters.ChildAnnualDeduction;

        var parents = Math.Clamp(input.DependentParents, 0, 2);
        if (parents > 0 && parameters.ParentDeductionRatePercent > 0)
        {
            var annualNetIncome = R((baseAfterCnss - professionalExpenses) * 12m);
            if (annualNetIncome > 0)
            {
                var perParent = Math.Min(
                    R(annualNetIncome * parameters.ParentDeductionRatePercent / 100m),
                    parameters.ParentAnnualDeductionCap);
                annual += parents * perParent;
            }
        }

        return R(annual / 12m);
    }

    private static decimal ResolvePreTaxDeductions(PayrollComputationInput input)
    {
        if (input.DeductionLines.Count > 0)
            return R(input.DeductionLines.Sum(l => l.Amount));
        return R(input.OtherDeductions);
    }

    /// <summary>
    /// Écrête un rappel de régularisation au net disponible : on ne peut pas prélever plus que
    /// ce que le salarié perçoit. L'IRPP est servi en priorité, la CSS absorbe le solde, et
    /// l'excédent est reporté (<see cref="RegularizationOutcome.Deferred"/>).
    /// Les restitutions (montants négatifs) augmentent le net et ne sont jamais écrêtées ;
    /// elles élargissent au passage le montant prélevable.
    /// </summary>
    private static RegularizationOutcome ApplyRegularizationCap(
        decimal irppRegularization,
        decimal cssRegularization,
        decimal availableNet)
    {
        var irpp = R(irppRegularization);
        var css = R(cssRegularization);

        var restitutions = Math.Min(irpp, 0m) + Math.Min(css, 0m);
        var irppClaim = Math.Max(irpp, 0m);
        var cssClaim = Math.Max(css, 0m);
        var totalClaim = R(irppClaim + cssClaim);
        var budget = R(availableNet - restitutions);

        if (totalClaim <= budget)
            return new RegularizationOutcome(irpp, css, 0m, false);

        var appliedIrpp = Math.Min(irppClaim, budget);
        var appliedCss = R(Math.Min(cssClaim, budget - appliedIrpp));

        return new RegularizationOutcome(
            R(Math.Min(irpp, 0m) + appliedIrpp),
            R(Math.Min(css, 0m) + appliedCss),
            R(totalClaim - appliedIrpp - appliedCss),
            true);
    }

    /// <summary>Régularisation effectivement appliquée au net, après écrêtage éventuel.</summary>
    private readonly record struct RegularizationOutcome(
        decimal Irpp,
        decimal Css,
        decimal Deferred,
        bool IsCapped);

    private static List<PayrollComputationLine> BuildLines(
        PayrollComputationInput input,
        PayrollYearParameters parameters,
        decimal cnssEmployeeRate,
        decimal cnssEmployerRate,
        decimal tfpRate,
        decimal cnssableGross,
        decimal cnssContributionBase,
        decimal accidentBase,
        decimal cnssEmployee,
        decimal baseAfterCnss,
        decimal professionalExpenses,
        bool professionalExpensesCapped,
        decimal familyDeductions,
        decimal monthlyNetTaxable,
        decimal irpp,
        decimal css,
        decimal smigExemption,
        decimal irppRegularization,
        decimal cssRegularization,
        decimal preTaxDeductions,
        decimal postTaxDeductions,
        decimal cnssEmployer,
        decimal workAccident,
        decimal tfp,
        decimal foprolos,
        decimal cssEmployer)
    {
        var lines = new List<PayrollComputationLine>();
        var order = 0;

        void Add(string label, PayslipLineKind kind, decimal amount, decimal? baseAmount = null, decimal? rate = null, DeductionKind? deductionKind = null)
            => lines.Add(new PayrollComputationLine
            {
                Order = order++,
                Label = label,
                Kind = kind,
                Base = baseAmount,
                Rate = rate,
                Amount = R(amount),
                DeductionKind = deductionKind
            });

        // Gains
        Add("Salaire de base", PayslipLineKind.Earning, input.BaseSalary);
        if (input.AllowanceLines.Count > 0)
        {
            foreach (var allowance in input.AllowanceLines)
                Add(allowance.Label, PayslipLineKind.Earning, allowance.Amount);
        }
        else
        {
            if (input.TaxableCnssableAllowances > 0)
                Add("Primes et indemnités imposables", PayslipLineKind.Earning, input.TaxableCnssableAllowances);
            if (input.TaxableOnlyAllowances > 0)
                Add("Primes imposables (hors CNSS)", PayslipLineKind.Earning, input.TaxableOnlyAllowances);
            if (input.CnssOnlyAllowances > 0)
                Add("Indemnités soumises CNSS (non imposables)", PayslipLineKind.Earning, input.CnssOnlyAllowances);
            if (input.NonTaxableAllowances > 0)
                Add("Indemnités non imposables", PayslipLineKind.Earning, input.NonTaxableAllowances);
        }

        if (input.InKindTaxableCnssableBenefits > 0)
            Add("Avantage en nature (imposable)", PayslipLineKind.Earning, input.InKindTaxableCnssableBenefits);

        if (input.OvertimeAmount > 0)
            Add("Heures supplémentaires", PayslipLineKind.Earning, input.OvertimeAmount);
        if (input.UnpaidAbsenceAmount > 0)
            Add("Absences non rémunérées", PayslipLineKind.Deduction, input.UnpaidAbsenceAmount);
        if (input.ProrataDeductionAmount > 0)
            Add("Prorata embauche / départ / suspension", PayslipLineKind.Deduction, input.ProrataDeductionAmount);

        // Retenues salariales
        if (cnssEmployee > 0)
            Add("Retenue CNSS", PayslipLineKind.Deduction, cnssEmployee, cnssContributionBase, cnssEmployeeRate);
        if (professionalExpenses > 0)
        {
            // Plafonnés : le montant ne résulte plus de base × taux, on n'affiche donc pas ce couple.
            if (professionalExpensesCapped)
                Add("Frais professionnels (plafonnés)", PayslipLineKind.Info, professionalExpenses);
            else
                Add("Frais professionnels (déduction)", PayslipLineKind.Info, professionalExpenses, baseAfterCnss, parameters.ProfessionalExpensesRate);
        }
        if (familyDeductions > 0)
            Add("Déductions familiales", PayslipLineKind.Info, familyDeductions);
        if (smigExemption > 0)
            Add("Exonération IRPP SMIG (art. 21)", PayslipLineKind.Info, smigExemption, monthlyNetTaxable, parameters.ResolveSmigExemptionRate());
        if (irpp > 0)
            Add("Retenue IRPP", PayslipLineKind.Deduction, irpp, monthlyNetTaxable);
        if (css > 0)
            Add("Contribution Sociale de Solidarité (CSS)", PayslipLineKind.Deduction, css, monthlyNetTaxable, parameters.CssRate);

        // Régularisation annuelle : les montants restent positifs et c'est le sens de la ligne
        // (retenue ou gain) qui porte le signe — le PDF et l'affichage n'ont ainsi rien à
        // connaître des montants négatifs. Pas de DeductionKind : l'impôt va à l'État (432),
        // pas à un tiers, et ne doit donc pas entrer dans la ventilation des retenues.
        if (irppRegularization > 0)
            Add("Régularisation IRPP (rappel)", PayslipLineKind.Deduction, irppRegularization);
        else if (irppRegularization < 0)
            Add("Régularisation IRPP (restitution)", PayslipLineKind.Earning, -irppRegularization);

        if (cssRegularization > 0)
            Add("Régularisation CSS (rappel)", PayslipLineKind.Deduction, cssRegularization);
        else if (cssRegularization < 0)
            Add("Régularisation CSS (restitution)", PayslipLineKind.Earning, -cssRegularization);

        if (input.DeductionLines.Count > 0)
        {
            foreach (var deduction in input.DeductionLines.Where(d => d.Amount > 0))
                Add(deduction.Label, PayslipLineKind.Deduction, deduction.Amount, deductionKind: deduction.Kind);
        }
        else if (preTaxDeductions > 0)
        {
            Add("Autres retenues (avances, oppositions)", PayslipLineKind.Deduction, preTaxDeductions, deductionKind: DeductionKind.Other);
        }

        foreach (var postTax in input.PostTaxDeductionLines.Where(d => d.Amount > 0))
            Add(postTax.Label, PayslipLineKind.Deduction, postTax.Amount, deductionKind: postTax.Kind);

        // Charges patronales
        if (cnssEmployer > 0)
            Add("CNSS patronale", PayslipLineKind.EmployerContribution, cnssEmployer, cnssContributionBase, cnssEmployerRate);
        if (workAccident > 0)
            Add("Accident de travail", PayslipLineKind.EmployerContribution, workAccident, accidentBase, input.WorkAccidentRate);
        if (tfp > 0)
            Add("TFP", PayslipLineKind.EmployerContribution, tfp, cnssContributionBase, tfpRate);
        if (foprolos > 0)
            Add("FOPROLOS", PayslipLineKind.EmployerContribution, foprolos, cnssContributionBase, parameters.FoprolosRate);
        if (cssEmployer > 0)
            Add("CSS patronale", PayslipLineKind.EmployerContribution, cssEmployer, cnssContributionBase, parameters.CssEmployerRate);

        foreach (var employerCharge in input.EmployerChargeLines.Where(c => c.Amount > 0))
            Add(employerCharge.Label, PayslipLineKind.EmployerContribution, employerCharge.Amount);

        return lines;
    }

    /// <summary>Résout les taux CNSS salariale et patronale selon le régime social.</summary>
    public static (decimal EmployeeRate, decimal EmployerRate) ResolveCnssRates(
        SocialRegime regime,
        PayrollYearParameters parameters)
    {
        if (!regime.IsSubjectToCnss())
            return (0m, 0m);

        return regime switch
        {
            SocialRegime.Rsa => (parameters.CnssEmployeeRateRsa, parameters.CnssEmployerRateRsa),
            _ => (parameters.CnssEmployeeRate, parameters.CnssEmployerRate)
        };
    }

    /// <summary>Applique le plafond mensuel CNSS si paramétré (null = comportement historique).</summary>
    public static decimal ApplyCnssCeiling(decimal cnssableGross, PayrollYearParameters parameters)
    {
        if (!parameters.CnssMonthlyCeiling.HasValue)
            return cnssableGross;
        return Math.Min(cnssableGross, parameters.CnssMonthlyCeiling.Value);
    }

    /// <summary>Applique le plafond mensuel accident du travail si paramétré.</summary>
    public static decimal ApplyAccidentCeiling(decimal cnssContributionBase, PayrollYearParameters parameters)
    {
        if (!parameters.AccidentWorkMonthlyCeiling.HasValue)
            return cnssContributionBase;
        return Math.Min(cnssContributionBase, parameters.AccidentWorkMonthlyCeiling.Value);
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
