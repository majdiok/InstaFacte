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

        // 6b. Déduction annuelle SMIG (LF 2019 interprétée) : forfait mensuel (500 TND/an ÷ 12)
        //     appliqué sur la base imposable des salariés éligibles (rémunération ≤ SMIG du mois).
        //     L'annualisation ×12 qui suit reprend cette base réduite ; la régularisation de fin
        //     d'exercice applique ensuite le forfait annuel exact proratisé aux mois éligibles
        //     (voir IrppRegularizationCalculator). Mode activé via SmigIrppExemptionMode.
        var smigAnnualDeductionAmount = 0m;
        if (parameters.SmigIrppExemptionMode == SmigIrppExemptionMode.SmigAnnualDeduction)
        {
            smigAnnualDeductionAmount = SmigIrppExemptionCalculator.ComputeMonthlySmigAnnualDeductionEffect(
                monthlyNetTaxable, parameters.MonthlySmig);
            monthlyNetTaxable = R(Math.Max(0m, monthlyNetTaxable - smigAnnualDeductionAmount));
        }

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
        // R-22 : allocation budgétaire des retenues. Quand le net disponible est insuffisant, les
        // retenues volontaires sont réparties par ordre de priorité (avantage en nature, mutuelle,
        // prêt, avance…) et le reliquat est reporté au mois suivant via RequestedAmount/
        // CarriedOverAmount — comme pour les oppositions — au lieu d'écrêter le net à zéro tout en
        // gardant le montant intégral des lignes sur le bulletin.
        var grossNetComponents = R(
            cnssableGross
            + input.TaxableOnlyAllowances
            + input.NonTaxableAllowances
            + statutoryNonTaxable
            - cnssEmployee
            - irpp
            - css);
        var availablePreTaxBudget = grossNetComponents < 0 ? 0m : grossNetComponents;

        decimal preTaxDeductions;
        decimal preTaxCarriedOver;
        IReadOnlyList<DeductionLineInput> adjustedPreTaxLines;
        var hasPartialDeductions = false;

        if (input.DeductionLines.Count > 0)
        {
            var (adjusted, applied, carried) = AllocateDeductionBudget(input.DeductionLines, availablePreTaxBudget);
            preTaxDeductions = applied;
            preTaxCarriedOver = carried;
            adjustedPreTaxLines = adjusted;
            if (carried > 0m) hasPartialDeductions = true;
        }
        else
        {
            adjustedPreTaxLines = Array.Empty<DeductionLineInput>();
            preTaxDeductions = R(input.OtherDeductions);
            preTaxCarriedOver = 0m;
            if (preTaxDeductions > availablePreTaxBudget)
            {
                preTaxCarriedOver = R(preTaxDeductions - availablePreTaxBudget);
                preTaxDeductions = availablePreTaxBudget;
                hasPartialDeductions = true;
            }
        }

        var netBeforeRegularization = R(availablePreTaxBudget - preTaxDeductions);
        if (netBeforeRegularization < 0) netBeforeRegularization = 0m;

        // Régularisation IRPP/CSS annuelle (décembre ou solde de tout compte) : le rappel est
        // écrêté au net disponible, la restitution augmente le net et n'est jamais écrêtée.
        // Hors mois de régularisation les deux montants valent zéro et le net reste identique.
        var regularization = ApplyRegularizationCap(
            input.IrppRegularization, input.CssRegularization, netBeforeRegularization);

        var netBeforePostTax = R(netBeforeRegularization - regularization.Irpp - regularization.Css);
        if (netBeforePostTax < 0) netBeforePostTax = 0m;

        decimal postTaxDeductions;
        decimal postTaxCarriedOver;
        IReadOnlyList<DeductionLineInput> adjustedPostTaxLines;
        if (input.PostTaxDeductionLines.Count > 0)
        {
            var (adjusted, applied, carried) = AllocateDeductionBudget(input.PostTaxDeductionLines, netBeforePostTax);
            postTaxDeductions = applied;
            postTaxCarriedOver = carried;
            adjustedPostTaxLines = adjusted;
            if (carried > 0m) hasPartialDeductions = true;
        }
        else
        {
            adjustedPostTaxLines = Array.Empty<DeductionLineInput>();
            postTaxDeductions = 0m;
            postTaxCarriedOver = 0m;
        }

        var netSalary = R(netBeforePostTax - postTaxDeductions);
        if (netSalary < 0) netSalary = 0m;

        // 9. Charges patronales (hors net à payer).
        var cnssEmployer = R(cnssContributionBase * cnssEmployerRate / 100m);
        // L'assurance accident de travail est une branche CNSS : exonérée si le régime l'est.
        var workAccidentRate = input.Regime.IsSubjectToCnss() ? input.WorkAccidentRate : 0m;
        var accidentBase = ApplyAccidentCeiling(cnssContributionBase, parameters);
        var workAccident = R(accidentBase * workAccidentRate / 100m);
        // Assiette des taxes sur salaires (TFP, FOPROLOS, CSS patronale). Le plafond CNSS est une
        // règle propre à la CNSS : ces trois taxes n'en ont pas. Le paramètre reste par défaut
        // aligné sur la CNSS pour ne pas modifier les cycles des exercices déjà paramétrés ; les
        // exercices créés à partir des présets légaux l'ont désactivé.
        // R-24 : PayrollTaxBaseMode.TotalGross (assiette légale = brut total de la rémunération)
        // prime sur ApplyCnssCeilingToPayrollTaxes ; le mode Legacy conserve le comportement
        // historique (assiette CNSS plafonnée ou non selon l'option).
        var payrollTaxBase = parameters.PayrollTaxBaseMode == PayrollTaxBaseMode.TotalGross
            ? totalGross
            : (parameters.ApplyCnssCeilingToPayrollTaxes ? cnssContributionBase : cnssableGross);
        var tfpRate = input.IsIndustrialSector ? parameters.TfpRateIndustry : parameters.TfpRateOther;
        var tfp = R(payrollTaxBase * tfpRate / 100m);
        var foprolos = R(payrollTaxBase * parameters.FoprolosRate / 100m);
        var cssEmployer = R(payrollTaxBase * parameters.CssEmployerRate / 100m);

        var lines = BuildLines(
            input, parameters, cnssEmployeeRate, cnssEmployerRate, tfpRate,
            cnssableGross, cnssContributionBase, payrollTaxBase, accidentBase, cnssEmployee, baseAfterCnss,
            professionalExpenses, professionalExpensesCapped, familyDeductions,
            monthlyNetTaxable, irpp, css, smigExemption, smigAnnualDeductionAmount,
            regularization.Irpp, regularization.Css,
            preTaxDeductions, postTaxDeductions, adjustedPreTaxLines, adjustedPostTaxLines,
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
            SmigAnnualDeductionAmount = smigAnnualDeductionAmount,
            OtherDeductions = R(preTaxDeductions + postTaxDeductions),
            NonTaxableAllowances = R(input.NonTaxableAllowances + statutoryNonTaxable),
            IrppRegularization = regularization.Irpp,
            CssRegularization = regularization.Css,
            RegularizationDeferred = regularization.Deferred,
            IsRegularizationCapped = regularization.IsCapped,
            HasPartialDeductions = hasPartialDeductions,
            PartialDeductionCarryOver = R(preTaxCarriedOver + postTaxCarriedOver),
            NetSalary = netSalary,
            CnssEmployer = cnssEmployer,
            WorkAccidentContribution = workAccident,
            Tfp = tfp,
            Foprolos = foprolos,
            CssEmployer = cssEmployer,
            PayrollTaxBase = payrollTaxBase,
            AppliedTfpRate = tfpRate,
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

    /// <summary>
    /// R-22 : répartit un budget disponible sur des lignes de retenue par ordre de priorité
    /// (avantage en nature &gt; mutuelle &gt; prêt &gt; avance &gt; titres-restaurant &gt; opposition/pension
    /// &gt; autres). Les lignes excédant le budget sont réduites ou abandonnées et leur reliquat est
    /// reporté au mois suivant via <see cref="DeductionLineInput.RequestedAmount"/> /
    /// <see cref="DeductionLineInput.CarriedOverAmount"/> — comme pour les oppositions — au lieu
    /// d'écrêter le net à zéro tout en conservant le montant intégral des lignes. L'ordre des
    /// lignes en entrée est préservé en sortie (l'allocation par priorité est interne).
    /// </summary>
    /// <returns>Les lignes ajustées, le total appliqué et le total reporté au mois suivant.</returns>
    private static (IReadOnlyList<DeductionLineInput> Adjusted, decimal AppliedTotal, decimal CarriedOver) AllocateDeductionBudget(
        IReadOnlyCollection<DeductionLineInput> lines, decimal availableBudget)
    {
        if (lines.Count == 0)
            return (Array.Empty<DeductionLineInput>(), 0m, 0m);

        var lineList = lines.ToList();
        var totalRequested = R(lineList.Sum(l => l.Amount));
        if (totalRequested <= 0m)
            return (lineList, 0m, 0m);

        // Budget suffisant : aucune ligne n'est réduite.
        if (totalRequested <= availableBudget)
            return (lineList, totalRequested, 0m);

        // Allocation par priorité en préservant l'ordre d'origine.
        var indexed = lineList.Select((line, index) => (Line: line, Index: index)).ToList();
        var appliedByIndex = new decimal[lineList.Count];
        var remaining = availableBudget < 0m ? 0m : availableBudget;
        foreach (var (line, index) in indexed.OrderBy(x => DeductionPriority(x.Line)))
        {
            decimal applied;
            if (remaining <= 0m)
            {
                applied = 0m;
            }
            else
            {
                applied = Math.Min(line.Amount, remaining);
                remaining = R(remaining - applied);
            }
            appliedByIndex[index] = R(applied);
        }

        var adjusted = indexed
            .Select(x => appliedByIndex[x.Index] == x.Line.Amount
                ? x.Line
                : x.Line with
                {
                    Amount = appliedByIndex[x.Index],
                    RequestedAmount = x.Line.RequestedAmount ?? x.Line.Amount,
                    // L2 : le report cumule la part saisissable non servie (cap saisisseur, portée par
                    // CarriedOverAmount en entrée pour les saisies/pensions) ET la réduction budgétaire
                    // du cycle. On dérive donc du montant demandé, non du montant post-cap — sinon la
                    // retenue d'une saisie réduite par le budget écraserait le report de cap et le
                    // reliquat saisissable serait perdu (sous-estimé sur le bulletin et l'échéance).
                    CarriedOverAmount = R((x.Line.RequestedAmount ?? x.Line.Amount) - appliedByIndex[x.Index])
                })
            .ToList();

        var appliedTotal = R(appliedByIndex.Sum());
        return (adjusted, appliedTotal, R(totalRequested - appliedTotal));
    }

    /// <summary>Priorité d'allocation d'une retenue (plus bas = servi en premier quand le budget est rare).</summary>
    private static int DeductionPriority(DeductionLineInput line) => line.Kind switch
    {
        DeductionKind.InKindBenefitOffset => 0,
        DeductionKind.MutuelleEmployee => 1,
        DeductionKind.Loan => 2,
        DeductionKind.Advance => 3,
        DeductionKind.MealVoucherEmployeeShare => 4,
        DeductionKind.Alimony => 5,
        DeductionKind.Garnishment => 6,
        _ => 7
    };

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
        decimal payrollTaxBase,
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
        decimal smigAnnualDeductionAmount,
        decimal irppRegularization,
        decimal cssRegularization,
        decimal preTaxDeductions,
        decimal postTaxDeductions,
        IReadOnlyList<DeductionLineInput> adjustedPreTaxLines,
        IReadOnlyList<DeductionLineInput> adjustedPostTaxLines,
        decimal cnssEmployer,
        decimal workAccident,
        decimal tfp,
        decimal foprolos,
        decimal cssEmployer)
    {
        var lines = new List<PayrollComputationLine>();
        var order = 0;

        void Add(
            string label, PayslipLineKind kind, decimal amount, decimal? baseAmount = null, decimal? rate = null,
            DeductionKind? deductionKind = null, EarningKind? earningKind = null, string? accountSce = null,
            Guid? sourceEntityId = null, decimal? requestedAmount = null, decimal? carriedOverAmount = null)
            => lines.Add(new PayrollComputationLine
            {
                Order = order++,
                Label = label,
                Kind = kind,
                Base = baseAmount,
                Rate = rate,
                Amount = R(amount),
                DeductionKind = deductionKind,
                EarningKind = earningKind,
                AccountSce = accountSce,
                SourceEntityId = sourceEntityId,
                RequestedAmount = requestedAmount,
                CarriedOverAmount = carriedOverAmount
            });

        // Gains
        Add("Salaire de base", PayslipLineKind.Earning, input.BaseSalary, earningKind: EarningKind.Salary);
        if (input.AllowanceLines.Count > 0)
        {
            foreach (var allowance in input.AllowanceLines)
                Add(allowance.Label, PayslipLineKind.Earning, allowance.Amount, earningKind: allowance.Kind);
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
            Add("Avantage en nature (imposable)", PayslipLineKind.Earning, input.InKindTaxableCnssableBenefits, earningKind: EarningKind.InKindBenefit);

        if (input.OvertimeAmount > 0)
            Add("Heures supplémentaires", PayslipLineKind.Earning, input.OvertimeAmount, earningKind: EarningKind.Overtime);
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
            Add("Exonération IRPP SMIG", PayslipLineKind.Info, smigExemption, monthlyNetTaxable, parameters.ResolveSmigExemptionRate());
        if (smigAnnualDeductionAmount > 0)
            Add("Déduction SMIG annuelle (forfait mensuel)", PayslipLineKind.Info, smigAnnualDeductionAmount, monthlyNetTaxable);
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

        if (adjustedPreTaxLines.Count > 0)
        {
            foreach (var deduction in adjustedPreTaxLines.Where(d => d.Amount > 0))
                Add(deduction.Label, PayslipLineKind.Deduction, deduction.Amount,
                    deductionKind: deduction.Kind,
                    sourceEntityId: deduction.SourceEntityId,
                    requestedAmount: deduction.RequestedAmount,
                    carriedOverAmount: deduction.CarriedOverAmount,
                    accountSce: deduction.AccountSce);
        }
        else if (preTaxDeductions > 0)
        {
            Add("Autres retenues (avances, oppositions)", PayslipLineKind.Deduction, preTaxDeductions, deductionKind: DeductionKind.Other);
        }

        foreach (var postTax in adjustedPostTaxLines.Where(d => d.Amount > 0))
            Add(postTax.Label, PayslipLineKind.Deduction, postTax.Amount,
                deductionKind: postTax.Kind,
                sourceEntityId: postTax.SourceEntityId,
                requestedAmount: postTax.RequestedAmount,
                carriedOverAmount: postTax.CarriedOverAmount,
                accountSce: postTax.AccountSce);

        // Charges patronales
        if (cnssEmployer > 0)
            Add("CNSS patronale", PayslipLineKind.EmployerContribution, cnssEmployer, cnssContributionBase, cnssEmployerRate);
        if (workAccident > 0)
            Add("Accident de travail", PayslipLineKind.EmployerContribution, workAccident, accidentBase, input.WorkAccidentRate);
        if (tfp > 0)
            Add("TFP", PayslipLineKind.EmployerContribution, tfp, payrollTaxBase, tfpRate);
        if (foprolos > 0)
            Add("FOPROLOS", PayslipLineKind.EmployerContribution, foprolos, payrollTaxBase, parameters.FoprolosRate);
        if (cssEmployer > 0)
            Add("CSS patronale", PayslipLineKind.EmployerContribution, cssEmployer, payrollTaxBase, parameters.CssEmployerRate);

        foreach (var employerCharge in input.EmployerChargeLines.Where(c => c.Amount > 0))
            Add(employerCharge.Label, PayslipLineKind.EmployerContribution, employerCharge.Amount, accountSce: employerCharge.AccountSce);

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
