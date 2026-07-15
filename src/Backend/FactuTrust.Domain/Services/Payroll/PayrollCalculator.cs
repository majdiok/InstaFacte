using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Moteur de calcul de paie tunisien — fonction pure et déterministe (sans dépendance base
/// de données ni infrastructure). Applique, dans l'ordre : brut, CNSS salariale, frais
/// professionnels, déductions familiales, IRPP (barème progressif annualisé), CSS, puis le
/// net à payer et les charges patronales.
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
        var cnssableGross = R(
            input.BaseSalary
            + input.TaxableCnssableAllowances
            + input.CnssOnlyAllowances
            + input.OvertimeAmount
            - input.UnpaidAbsenceAmount);
        if (cnssableGross < 0) cnssableGross = 0m;

        var taxableGross = R(
            input.BaseSalary
            + input.TaxableCnssableAllowances
            + input.TaxableOnlyAllowances
            + input.OvertimeAmount
            - input.UnpaidAbsenceAmount);
        if (taxableGross < 0) taxableGross = 0m;

        // Brut total affiché (inclut les éléments non soumis, ex. transport dans les limites légales).
        var totalGross = R(cnssableGross + input.TaxableOnlyAllowances + input.NonTaxableAllowances);

        // 2. CNSS salariale.
        var (cnssEmployeeRate, cnssEmployerRate) = ResolveCnssRates(input.Regime, parameters);
        var cnssEmployee = R(cnssableGross * cnssEmployeeRate / 100m);

        // 3. Base imposable après CNSS.
        var baseAfterCnss = R(taxableGross - cnssEmployee);

        // 4. Frais professionnels (mensualisés, plafonnés).
        var monthlyCap = R(parameters.ProfessionalExpensesAnnualCap / 12m);
        var professionalExpenses = R(baseAfterCnss * parameters.ProfessionalExpensesRate / 100m);
        if (professionalExpenses > monthlyCap)
            professionalExpenses = monthlyCap;

        // 5. Déductions familiales (mensualisées).
        var childCount = Math.Min(input.DependentChildren, parameters.MaxDeductibleChildren);
        var annualFamilyDeduction =
            (input.IsHeadOfFamily ? parameters.HeadOfFamilyAnnualDeduction : 0m)
            + childCount * parameters.ChildAnnualDeduction;
        var familyDeductions = R(annualFamilyDeduction / 12m);

        // 6. Net imposable mensuel puis annualisation pour le barème progressif.
        var monthlyNetTaxable = baseAfterCnss - professionalExpenses - familyDeductions;
        if (monthlyNetTaxable < 0) monthlyNetTaxable = 0m;
        monthlyNetTaxable = R(monthlyNetTaxable);

        var annualNetTaxable = R(monthlyNetTaxable * 12m);

        var annualIrpp = ComputeProgressiveTax(annualNetTaxable, parameters);
        var irpp = R(annualIrpp / 12m);

        // 7. CSS : exonérée si le revenu annuel imposable reste dans la tranche exonérée.
        decimal css = 0m;
        if (annualNetTaxable > parameters.CssAnnualExemptionThreshold)
        {
            var annualCss = R(annualNetTaxable * parameters.CssRate / 100m);
            css = R(annualCss / 12m);
        }

        // 8. Net à payer.
        var netSalary = R(
            cnssableGross
            + input.TaxableOnlyAllowances
            + input.NonTaxableAllowances
            - cnssEmployee
            - irpp
            - css
            - input.OtherDeductions);
        if (netSalary < 0) netSalary = 0m;

        // 9. Charges patronales (hors net à payer).
        var cnssEmployer = R(cnssableGross * cnssEmployerRate / 100m);
        // L'assurance accident de travail est une branche CNSS : exonérée si le régime l'est.
        var workAccidentRate = input.Regime.IsSubjectToCnss() ? input.WorkAccidentRate : 0m;
        var workAccident = R(cnssableGross * workAccidentRate / 100m);
        var tfpRate = input.IsIndustrialSector ? parameters.TfpRateIndustry : parameters.TfpRateOther;
        var tfp = R(cnssableGross * tfpRate / 100m);
        var foprolos = R(cnssableGross * parameters.FoprolosRate / 100m);

        var lines = BuildLines(
            input, cnssEmployeeRate, cnssEmployerRate, tfpRate,
            cnssableGross, cnssEmployee, professionalExpenses, familyDeductions,
            irpp, css, cnssEmployer, workAccident, tfp, foprolos);

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
            OtherDeductions = R(input.OtherDeductions),
            NonTaxableAllowances = R(input.NonTaxableAllowances),
            NetSalary = netSalary,
            CnssEmployer = cnssEmployer,
            WorkAccidentContribution = workAccident,
            Tfp = tfp,
            Foprolos = foprolos,
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

    private static List<PayrollComputationLine> BuildLines(
        PayrollComputationInput input,
        decimal cnssEmployeeRate,
        decimal cnssEmployerRate,
        decimal tfpRate,
        decimal cnssableGross,
        decimal cnssEmployee,
        decimal professionalExpenses,
        decimal familyDeductions,
        decimal irpp,
        decimal css,
        decimal cnssEmployer,
        decimal workAccident,
        decimal tfp,
        decimal foprolos)
    {
        var lines = new List<PayrollComputationLine>();
        var order = 0;

        void Add(string label, PayslipLineKind kind, decimal amount, decimal? baseAmount = null, decimal? rate = null)
            => lines.Add(new PayrollComputationLine
            {
                Order = order++,
                Label = label,
                Kind = kind,
                Base = baseAmount,
                Rate = rate,
                Amount = R(amount)
            });

        // Gains
        Add("Salaire de base", PayslipLineKind.Earning, input.BaseSalary);
        if (input.TaxableCnssableAllowances > 0)
            Add("Primes et indemnités imposables", PayslipLineKind.Earning, input.TaxableCnssableAllowances);
        if (input.TaxableOnlyAllowances > 0)
            Add("Primes imposables (hors CNSS)", PayslipLineKind.Earning, input.TaxableOnlyAllowances);
        if (input.CnssOnlyAllowances > 0)
            Add("Indemnités soumises CNSS (non imposables)", PayslipLineKind.Earning, input.CnssOnlyAllowances);
        if (input.OvertimeAmount > 0)
            Add("Heures supplémentaires", PayslipLineKind.Earning, input.OvertimeAmount);
        if (input.NonTaxableAllowances > 0)
            Add("Indemnités non imposables", PayslipLineKind.Earning, input.NonTaxableAllowances);
        if (input.UnpaidAbsenceAmount > 0)
            Add("Absences non rémunérées", PayslipLineKind.Deduction, input.UnpaidAbsenceAmount);

        // Retenues salariales
        if (cnssEmployee > 0)
            Add("Retenue CNSS", PayslipLineKind.Deduction, cnssEmployee, cnssableGross, cnssEmployeeRate);
        if (professionalExpenses > 0)
            Add("Frais professionnels (déduction)", PayslipLineKind.Info, professionalExpenses);
        if (familyDeductions > 0)
            Add("Déductions familiales", PayslipLineKind.Info, familyDeductions);
        if (irpp > 0)
            Add("Retenue IRPP", PayslipLineKind.Deduction, irpp);
        if (css > 0)
            Add("Contribution Sociale de Solidarité (CSS)", PayslipLineKind.Deduction, css);
        if (input.OtherDeductions > 0)
            Add("Autres retenues (avances, oppositions)", PayslipLineKind.Deduction, input.OtherDeductions);

        // Charges patronales
        if (cnssEmployer > 0)
            Add("CNSS patronale", PayslipLineKind.EmployerContribution, cnssEmployer, cnssableGross, cnssEmployerRate);
        if (workAccident > 0)
            Add("Accident de travail", PayslipLineKind.EmployerContribution, workAccident, cnssableGross, input.WorkAccidentRate);
        if (tfp > 0)
            Add("TFP", PayslipLineKind.EmployerContribution, tfp, cnssableGross, tfpRate);
        if (foprolos > 0)
            Add("FOPROLOS", PayslipLineKind.EmployerContribution, foprolos, cnssableGross);

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

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
