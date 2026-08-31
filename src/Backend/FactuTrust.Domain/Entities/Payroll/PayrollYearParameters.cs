using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>
/// Paramètres légaux de paie versionnés par exercice (Tunisie) : barème IRPP, taux CNSS,
/// CSS, TFP, FOPROLOS, frais professionnels et déductions familiales.
/// Une ligne par exercice. Les valeurs par défaut sont seedées mais restent modifiables
/// (la loi de finances peut imposer des ajustements sans modification du code).
/// </summary>
public sealed class PayrollYearParameters : AggregateRoot
{
    public int FiscalYear { get; private set; }

    /// <summary>Taux de cotisation CNSS salariale (RSNA), en % du brut. Ex. 9.18.</summary>
    public decimal CnssEmployeeRate { get; private set; }
    /// <summary>Taux de cotisation CNSS patronale (RSNA), en % du brut. Ex. 16.57.</summary>
    public decimal CnssEmployerRate { get; private set; }

    /// <summary>Taux de cotisation CNSS salariale (RSA), en % du brut.</summary>
    public decimal CnssEmployeeRateRsa { get; private set; }
    /// <summary>Taux de cotisation CNSS patronale (RSA), en % du brut.</summary>
    public decimal CnssEmployerRateRsa { get; private set; }

    /// <summary>Rejeter les contrats dont le salaire de base est inférieur au SMIG mensuel.</summary>
    public bool EnforceSmigOnContracts { get; private set; }
    /// <summary>Autoriser les taux de majoration HS 175 % et 200 %.</summary>
    public bool EnableExtendedOvertimeRates { get; private set; }
    /// <summary>Matrice à 4 quadrants pour les indemnités (imposable × CNSS).</summary>
    public bool EnableAllowanceQuadrantMatrix { get; private set; }
    /// <summary>
    /// Activer la régularisation IRPP/CSS annuelle (décembre et soldes de tout compte).
    /// Désactivée par défaut : les exercices existants conservent exactement leur calcul.
    /// </summary>
    public bool EnableIrppRegularization { get; private set; }
    /// <summary>
    /// Prorata automatique embauche / départ / suspension sur le salaire de base.
    /// Désactivé par défaut : les exercices existants conservent exactement leur calcul.
    /// </summary>
    public bool EnableAutomaticProrata { get; private set; }

    /// <summary>Taux de la Contribution Sociale de Solidarité (CSS), en %. Ex. 0.5.</summary>
    public decimal CssRate { get; private set; }
    /// <summary>Seuil annuel (TND) en dessous duquel la CSS ne s'applique pas (tranche exonérée IRPP). Ex. 5000.</summary>
    public decimal CssAnnualExemptionThreshold { get; private set; }
    /// <summary>Taux de la Contribution Sociale de Solidarité patronale, en % du brut soumis à CNSS. Ex. 0.5. Aucun seuil d'exonération côté employeur.</summary>
    public decimal CssEmployerRate { get; private set; }

    /// <summary>Taux des frais professionnels, en % de la base après CNSS. Ex. 10.</summary>
    public decimal ProfessionalExpensesRate { get; private set; }
    /// <summary>Plafond annuel (TND) des frais professionnels. Ex. 2000.</summary>
    public decimal ProfessionalExpensesAnnualCap { get; private set; }

    /// <summary>Déduction annuelle (TND) pour chef de famille. Ex. 300.</summary>
    public decimal HeadOfFamilyAnnualDeduction { get; private set; }
    /// <summary>Déduction annuelle (TND) par enfant à charge. Ex. 100.</summary>
    public decimal ChildAnnualDeduction { get; private set; }
    /// <summary>Nombre maximum d'enfants à charge pris en compte pour la déduction. Ex. 4.</summary>
    public int MaxDeductibleChildren { get; private set; }
    /// <summary>Déduction annuelle (TND) par enfant étudiant non boursier de moins de 25 ans. Ex. 1000.</summary>
    public decimal StudentChildAnnualDeduction { get; private set; }
    /// <summary>Déduction annuelle (TND) par enfant infirme (sans limite de rang). Ex. 2000.</summary>
    public decimal DisabledChildAnnualDeduction { get; private set; }
    /// <summary>Taux de la déduction pour parent à charge, en % du revenu net imposable. Ex. 5.</summary>
    public decimal ParentDeductionRatePercent { get; private set; }
    /// <summary>Plafond annuel (TND) de la déduction par parent à charge. Ex. 450.</summary>
    public decimal ParentAnnualDeductionCap { get; private set; }

    /// <summary>L'entreprise relève-t-elle du secteur industriel (TFP à taux réduit) ?</summary>
    public bool IsIndustrialSector { get; private set; }

    /// <summary>Taux de la Taxe de Formation Professionnelle (TFP) — secteur industriel, en %. Ex. 1.</summary>
    public decimal TfpRateIndustry { get; private set; }
    /// <summary>Taux de la Taxe de Formation Professionnelle (TFP) — autres secteurs, en %. Ex. 2.</summary>
    public decimal TfpRateOther { get; private set; }
    /// <summary>Taux de la contribution FOPROLOS (part patronale), en %. Ex. 1.</summary>
    public decimal FoprolosRate { get; private set; }

    /// <summary>
    /// Le plafond CNSS s'applique-t-il aussi à l'assiette de la TFP, du FOPROLOS et de la CSS
    /// patronale ? Légalement <b>non</b> : ces taxes sont assises sur la totalité du brut soumis.
    ///
    /// <para>
    /// Reste à <c>true</c> par défaut pour ne pas déplacer les montants des exercices existants ;
    /// les exercices matérialisés depuis les présets légaux le positionnent à <c>false</c>. Sans
    /// plafond CNSS paramétré — le cas courant — ce réglage n'a aucun effet.
    /// </para>
    /// </summary>
    public bool ApplyCnssCeilingToPayrollTaxes { get; private set; } = true;

    /// <summary>
    /// Assiette des taxes sur salaires (TFP/FOPROLOS/CSS patronale) — R-24.
    /// <c>Legacy</c> reproduit le comportement historique (assiette CNSS plafonnée ou non selon
    /// <see cref="ApplyCnssCeilingToPayrollTaxes"/>) ; <c>TotalGross</c> applique l'assiette légale
    /// = brut total de la rémunération. Les exercices existants conservent <c>Legacy</c> ; les
    /// présets légaux matérialisent <c>TotalGross</c>. Sans indemnités hors CNSS, les deux modes
    /// coïncident (le brut total égale le brut CNSS).
    /// </summary>
    public PayrollTaxBaseMode PayrollTaxBaseMode { get; private set; } = PayrollTaxBaseMode.Legacy;

    /// <summary>SMIG mensuel indicatif (TND), pour contrôle de cohérence. Optionnel.</summary>
    public decimal MonthlySmig { get; private set; }

    /// <summary>Mode d'exonération/déduction IRPP SMIG. Désactivé par défaut.</summary>
    public SmigIrppExemptionMode SmigIrppExemptionMode { get; private set; }

    /// <summary>
    /// Taux d'IRPP applicable à la portion SMIG exonérée (mode SmigPortion).
    /// Si null, utilise le premier taux non nul du barème IRPP.
    /// </summary>
    public decimal? SmigIrppExemptionRateOverride { get; private set; }

    /// <summary>Plafond journalier d'exonération tickets restaurant (TND). Défaut : 3.000.</summary>
    public decimal MealVoucherDailyExemptionCap { get; private set; }

    /// <summary>Plafond mensuel CNSS (null = pas de plafond, comportement historique).</summary>
    public decimal? CnssMonthlyCeiling { get; private set; }
    /// <summary>Plafond journalier CNSS (null = pas de plafond).</summary>
    public decimal? CnssDailyCeiling { get; private set; }
    /// <summary>Plafond mensuel CSS salariale (null = pas de plafond).</summary>
    public decimal? CssMonthlyCeiling { get; private set; }
    /// <summary>Plafond mensuel accident du travail (null = pas de plafond).</summary>
    public decimal? AccidentWorkMonthlyCeiling { get; private set; }

    /// <summary>Jours de carence maladie avant indemnisation (défaut 5).</summary>
    public int SickLeaveWaitingDays { get; private set; } = 5;
    /// <summary>Taux IJ CNSS maladie en % du salaire journalier (défaut 66,67).</summary>
    public decimal SickLeaveIjRatePercent { get; private set; } = 66.67m;
    /// <summary>Durée légale congé maternité en jours (défaut 60).</summary>
    public int MaternityLeaveDurationDays { get; private set; } = 60;
    /// <summary>Durée légale congé paternité en jours ouvrables (défaut 2).</summary>
    public int PaternityLeaveDurationDays { get; private set; } = 2;
    /// <summary>Maintien employeur maternité par défaut en % (défaut 100).</summary>
    public decimal MaternityEmployerTopUpDefault { get; private set; } = 100m;

    private readonly List<PayrollIrppBracket> _irppBrackets = new();
    /// <summary>Tranches du barème IRPP progressif, triées par borne inférieure croissante.</summary>
    public IReadOnlyCollection<PayrollIrppBracket> IrppBrackets => _irppBrackets.AsReadOnly();

    private readonly List<PayrollGarnishmentBracket> _garnishmentBrackets = new();
    /// <summary>Tranches de saisie sur salaire (net mensuel × fraction saisissable).</summary>
    public IReadOnlyCollection<PayrollGarnishmentBracket> GarnishmentBrackets => _garnishmentBrackets.AsReadOnly();

    private PayrollYearParameters() { }

    public static Result<PayrollYearParameters> Create(
        int fiscalYear,
        decimal cnssEmployeeRate,
        decimal cnssEmployerRate,
        decimal cssRate,
        decimal cssAnnualExemptionThreshold,
        decimal professionalExpensesRate,
        decimal professionalExpensesAnnualCap,
        decimal headOfFamilyAnnualDeduction,
        decimal childAnnualDeduction,
        int maxDeductibleChildren,
        decimal tfpRateIndustry,
        decimal tfpRateOther,
        decimal foprolosRate,
        decimal monthlySmig,
        IEnumerable<PayrollIrppBracket> irppBrackets,
        decimal? cnssEmployeeRateRsa = null,
        decimal? cnssEmployerRateRsa = null,
        bool enforceSmigOnContracts = false,
        bool enableExtendedOvertimeRates = false,
        bool enableAllowanceQuadrantMatrix = false,
        decimal studentChildAnnualDeduction = 0m,
        decimal disabledChildAnnualDeduction = 0m,
        decimal parentDeductionRatePercent = 0m,
        decimal parentAnnualDeductionCap = 0m,
        bool isIndustrialSector = false,
        decimal mealVoucherDailyExemptionCap = 3.000m,
        IEnumerable<PayrollGarnishmentBracket>? garnishmentBrackets = null,
        bool enableIrppRegularization = false,
        bool enableAutomaticProrata = false,
        SmigIrppExemptionMode smigIrppExemptionMode = SmigIrppExemptionMode.None,
        decimal? smigIrppExemptionRateOverride = null,
        decimal cssEmployerRate = 0m,
        PayrollTaxBaseMode payrollTaxBaseMode = PayrollTaxBaseMode.Legacy)
    {
        if (fiscalYear is < 2000 or > 2100)
            return Result.Failure<PayrollYearParameters>(Error.Validation("FiscalYear", "L'exercice doit être compris entre 2000 et 2100."));

        var brackets = (irppBrackets ?? Enumerable.Empty<PayrollIrppBracket>())
            .OrderBy(b => b.LowerBound)
            .ToList();

        if (brackets.Count == 0)
            return Result.Failure<PayrollYearParameters>(Error.Validation("IrppBrackets", "Le barème IRPP doit comporter au moins une tranche."));

        if (brackets[0].LowerBound != 0m)
            return Result.Failure<PayrollYearParameters>(Error.Validation("IrppBrackets", "La première tranche IRPP doit démarrer à 0."));

        if (mealVoucherDailyExemptionCap < 0)
            return Result.Failure<PayrollYearParameters>(Error.Validation("MealVoucherDailyExemptionCap", "Le plafond journalier tickets restaurant ne peut pas être négatif."));

        if (smigIrppExemptionMode != SmigIrppExemptionMode.None && monthlySmig <= 0)
            return Result.Failure<PayrollYearParameters>(Error.Validation("MonthlySmig", "Le SMIG mensuel doit être positif pour activer l'exonération IRPP SMIG."));

        if (smigIrppExemptionRateOverride is < 0m or > 100m)
            return Result.Failure<PayrollYearParameters>(Error.Validation("SmigIrppExemptionRateOverride", "Le taux d'exonération IRPP SMIG doit être compris entre 0 et 100 %."));

        var garnishment = (garnishmentBrackets ?? Enumerable.Empty<PayrollGarnishmentBracket>())
            .OrderBy(b => b.LowerBoundMonthlyNet)
            .ToList();

        var rsaEmployeeRate = cnssEmployeeRateRsa ?? cnssEmployeeRate;
        var rsaEmployerRate = cnssEmployerRateRsa ?? cnssEmployerRate;

        var negativeRates = new[]
        {
            cnssEmployeeRate, cnssEmployerRate, rsaEmployeeRate, rsaEmployerRate,
            cssRate, cssEmployerRate, professionalExpensesRate, tfpRateIndustry, tfpRateOther, foprolosRate,
            parentDeductionRatePercent
        };
        if (negativeRates.Any(r => r < 0))
            return Result.Failure<PayrollYearParameters>(Error.Validation("Rates", "Les taux ne peuvent pas être négatifs."));

        if (studentChildAnnualDeduction < 0 || disabledChildAnnualDeduction < 0 || parentAnnualDeductionCap < 0)
            return Result.Failure<PayrollYearParameters>(Error.Validation("Deductions", "Les déductions ne peuvent pas être négatives."));

        var entity = new PayrollYearParameters
        {
            FiscalYear = fiscalYear,
            CnssEmployeeRate = Round(cnssEmployeeRate),
            CnssEmployerRate = Round(cnssEmployerRate),
            CnssEmployeeRateRsa = Round(rsaEmployeeRate),
            CnssEmployerRateRsa = Round(rsaEmployerRate),
            EnforceSmigOnContracts = enforceSmigOnContracts,
            EnableExtendedOvertimeRates = enableExtendedOvertimeRates,
            EnableAllowanceQuadrantMatrix = enableAllowanceQuadrantMatrix,
            EnableIrppRegularization = enableIrppRegularization,
            EnableAutomaticProrata = enableAutomaticProrata,
            CssRate = Round(cssRate),
            CssAnnualExemptionThreshold = Round(cssAnnualExemptionThreshold),
            CssEmployerRate = Round(cssEmployerRate),
            ProfessionalExpensesRate = Round(professionalExpensesRate),
            ProfessionalExpensesAnnualCap = Round(professionalExpensesAnnualCap),
            HeadOfFamilyAnnualDeduction = Round(headOfFamilyAnnualDeduction),
            ChildAnnualDeduction = Round(childAnnualDeduction),
            MaxDeductibleChildren = Math.Max(0, maxDeductibleChildren),
            StudentChildAnnualDeduction = Round(studentChildAnnualDeduction),
            DisabledChildAnnualDeduction = Round(disabledChildAnnualDeduction),
            ParentDeductionRatePercent = Round(parentDeductionRatePercent),
            ParentAnnualDeductionCap = Round(parentAnnualDeductionCap),
            IsIndustrialSector = isIndustrialSector,
            TfpRateIndustry = Round(tfpRateIndustry),
            TfpRateOther = Round(tfpRateOther),
            FoprolosRate = Round(foprolosRate),
            MonthlySmig = Round(monthlySmig),
            SmigIrppExemptionMode = smigIrppExemptionMode,
            SmigIrppExemptionRateOverride = smigIrppExemptionRateOverride.HasValue
                ? Round(smigIrppExemptionRateOverride.Value)
                : null,
            MealVoucherDailyExemptionCap = Round(mealVoucherDailyExemptionCap),
            PayrollTaxBaseMode = payrollTaxBaseMode
        };
        entity._irppBrackets.AddRange(brackets);
        foreach (var gBracket in garnishment)
            gBracket.AssignParentId(entity.Id);
        entity._garnishmentBrackets.AddRange(garnishment);
        return Result.Success(entity);
    }

    /// <summary>Met à jour l'ensemble des taux et déductions (sans toucher au barème).</summary>
    public Result UpdateRates(
        decimal cnssEmployeeRate,
        decimal cnssEmployerRate,
        decimal cssRate,
        decimal cssAnnualExemptionThreshold,
        decimal professionalExpensesRate,
        decimal professionalExpensesAnnualCap,
        decimal headOfFamilyAnnualDeduction,
        decimal childAnnualDeduction,
        int maxDeductibleChildren,
        decimal tfpRateIndustry,
        decimal tfpRateOther,
        decimal foprolosRate,
        decimal monthlySmig,
        decimal cnssEmployeeRateRsa,
        decimal cnssEmployerRateRsa,
        bool enforceSmigOnContracts,
        bool enableExtendedOvertimeRates,
        bool enableAllowanceQuadrantMatrix,
        decimal studentChildAnnualDeduction = 0m,
        decimal disabledChildAnnualDeduction = 0m,
        decimal parentDeductionRatePercent = 0m,
        decimal parentAnnualDeductionCap = 0m,
        bool isIndustrialSector = false,
        decimal mealVoucherDailyExemptionCap = 3.000m,
        bool enableIrppRegularization = false,
        bool enableAutomaticProrata = false,
        SmigIrppExemptionMode smigIrppExemptionMode = SmigIrppExemptionMode.None,
        decimal? smigIrppExemptionRateOverride = null,
        decimal cssEmployerRate = 0m,
        PayrollTaxBaseMode payrollTaxBaseMode = PayrollTaxBaseMode.Legacy)
    {
        var rates = new[]
        {
            cnssEmployeeRate, cnssEmployerRate, cnssEmployeeRateRsa, cnssEmployerRateRsa,
            cssRate, cssEmployerRate, professionalExpensesRate, tfpRateIndustry, tfpRateOther, foprolosRate,
            parentDeductionRatePercent
        };
        if (rates.Any(r => r < 0))
            return Result.Failure(Error.Validation("Rates", "Les taux ne peuvent pas être négatifs."));

        if (studentChildAnnualDeduction < 0 || disabledChildAnnualDeduction < 0 || parentAnnualDeductionCap < 0)
            return Result.Failure(Error.Validation("Deductions", "Les déductions ne peuvent pas être négatives."));

        CnssEmployeeRate = Round(cnssEmployeeRate);
        CnssEmployerRate = Round(cnssEmployerRate);
        CnssEmployeeRateRsa = Round(cnssEmployeeRateRsa);
        CnssEmployerRateRsa = Round(cnssEmployerRateRsa);
        EnforceSmigOnContracts = enforceSmigOnContracts;
        EnableExtendedOvertimeRates = enableExtendedOvertimeRates;
        EnableAllowanceQuadrantMatrix = enableAllowanceQuadrantMatrix;
        EnableIrppRegularization = enableIrppRegularization;
        EnableAutomaticProrata = enableAutomaticProrata;
        CssRate = Round(cssRate);
        CssAnnualExemptionThreshold = Round(cssAnnualExemptionThreshold);
        CssEmployerRate = Round(cssEmployerRate);
        ProfessionalExpensesRate = Round(professionalExpensesRate);
        ProfessionalExpensesAnnualCap = Round(professionalExpensesAnnualCap);
        HeadOfFamilyAnnualDeduction = Round(headOfFamilyAnnualDeduction);
        ChildAnnualDeduction = Round(childAnnualDeduction);
        MaxDeductibleChildren = Math.Max(0, maxDeductibleChildren);
        StudentChildAnnualDeduction = Round(studentChildAnnualDeduction);
        DisabledChildAnnualDeduction = Round(disabledChildAnnualDeduction);
        ParentDeductionRatePercent = Round(parentDeductionRatePercent);
        ParentAnnualDeductionCap = Round(parentAnnualDeductionCap);
        IsIndustrialSector = isIndustrialSector;
        TfpRateIndustry = Round(tfpRateIndustry);
        TfpRateOther = Round(tfpRateOther);
        FoprolosRate = Round(foprolosRate);
        MonthlySmig = Round(monthlySmig);
        if (mealVoucherDailyExemptionCap < 0)
            return Result.Failure(Error.Validation("MealVoucherDailyExemptionCap", "Le plafond journalier tickets restaurant ne peut pas être négatif."));

        if (smigIrppExemptionMode != SmigIrppExemptionMode.None && monthlySmig <= 0)
            return Result.Failure(Error.Validation("MonthlySmig", "Le SMIG mensuel doit être positif pour activer l'exonération IRPP SMIG."));

        if (smigIrppExemptionRateOverride is < 0m or > 100m)
            return Result.Failure(Error.Validation("SmigIrppExemptionRateOverride", "Le taux d'exonération IRPP SMIG doit être compris entre 0 et 100 %."));

        MealVoucherDailyExemptionCap = Round(mealVoucherDailyExemptionCap);
        SmigIrppExemptionMode = smigIrppExemptionMode;
        SmigIrppExemptionRateOverride = smigIrppExemptionRateOverride.HasValue
            ? Round(smigIrppExemptionRateOverride.Value)
            : null;
        PayrollTaxBaseMode = payrollTaxBaseMode;
        IncrementVersion();
        return Result.Success();
    }

    /// <summary>
    /// Taux d'IRPP applicable à l'exonération SMIG : override paramétré ou premier taux non nul du barème.
    /// </summary>
    public decimal ResolveSmigExemptionRate()
    {
        if (SmigIrppExemptionRateOverride.HasValue)
            return SmigIrppExemptionRateOverride.Value;

        var firstNonZero = IrppBrackets
            .Where(b => b.Rate > 0)
            .OrderBy(b => b.LowerBound)
            .FirstOrDefault();

        return firstNonZero?.Rate ?? 15m;
    }

    /// <summary>Remplace intégralement le barème IRPP par un nouveau jeu de tranches.</summary>
    public Result ReplaceIrppBrackets(IEnumerable<PayrollIrppBracket> brackets)
    {
        var ordered = (brackets ?? Enumerable.Empty<PayrollIrppBracket>())
            .OrderBy(b => b.LowerBound)
            .ToList();

        if (ordered.Count == 0)
            return Result.Failure(Error.Validation("IrppBrackets", "Le barème IRPP doit comporter au moins une tranche."));
        if (ordered[0].LowerBound != 0m)
            return Result.Failure(Error.Validation("IrppBrackets", "La première tranche IRPP doit démarrer à 0."));
        if (ordered.Select(b => b.LowerBound).Distinct().Count() != ordered.Count)
            return Result.Failure(Error.Validation("IrppBrackets", "Deux tranches IRPP ne peuvent pas avoir le même seuil inférieur."));
        if (ordered.Any(b => b.Rate < 0m || b.Rate > 100m))
            return Result.Failure(Error.Validation("IrppBrackets", "Les taux IRPP doivent être compris entre 0 et 100 %."));

        foreach (var bracket in ordered)
            bracket.AssignParentId(Id);

        _irppBrackets.Clear();
        _irppBrackets.AddRange(ordered);
        IncrementVersion();
        return Result.Success();
    }

    /// <summary>Remplace intégralement les tranches de saisie sur salaire.</summary>
    public Result ReplaceGarnishmentBrackets(IEnumerable<PayrollGarnishmentBracket> brackets)
    {
        var ordered = (brackets ?? Enumerable.Empty<PayrollGarnishmentBracket>())
            .OrderBy(b => b.LowerBoundMonthlyNet)
            .ToList();

        if (ordered.Select(b => b.LowerBoundMonthlyNet).Distinct().Count() != ordered.Count)
            return Result.Failure(Error.Validation("GarnishmentBrackets", "Deux tranches de saisie ne peuvent pas avoir le même seuil inférieur."));
        if (ordered.Any(b => b.SeizableFraction < 0m || b.SeizableFraction > 1m))
            return Result.Failure(Error.Validation("GarnishmentBrackets", "La fraction saisissable doit être comprise entre 0 et 1."));

        foreach (var bracket in ordered)
            bracket.AssignParentId(Id);

        _garnishmentBrackets.Clear();
        _garnishmentBrackets.AddRange(ordered);
        IncrementVersion();
        return Result.Success();
    }

    public void SetCnssMonthlyCeiling(decimal? ceiling)
    {
        CnssMonthlyCeiling = ceiling.HasValue ? Round(ceiling.Value) : null;
        IncrementVersion();
    }

    /// <summary>
    /// Étend ou non le plafond CNSS à l'assiette des taxes sur salaires. Voir
    /// <see cref="ApplyCnssCeilingToPayrollTaxes"/> : sans plafond CNSS, sans effet.
    /// </summary>
    public void SetApplyCnssCeilingToPayrollTaxes(bool apply)
    {
        ApplyCnssCeilingToPayrollTaxes = apply;
        IncrementVersion();
    }

    /// <summary>
    /// Force le mode d'assiette des taxes sur salaires (R-24). Principalement utilisé pour
    /// simuler un exercice legacy en tests de non-régression (les exercices en base conservent
    /// <c>Legacy</c> via la valeur par défaut de la colonne).
    /// </summary>
    public void SetPayrollTaxBaseMode(PayrollTaxBaseMode mode)
    {
        PayrollTaxBaseMode = mode;
        IncrementVersion();
    }

    public Result UpdateCnssCeilings(decimal? monthlyCeiling, decimal? dailyCeiling, decimal? cssCeiling, decimal? accidentCeiling)
    {
        if (monthlyCeiling is < 0 || dailyCeiling is < 0 || cssCeiling is < 0 || accidentCeiling is < 0)
            return Result.Failure(Error.Validation("Ceilings", "Les plafonds ne peuvent pas être négatifs."));

        CnssMonthlyCeiling = monthlyCeiling.HasValue ? Round(monthlyCeiling.Value) : null;
        CnssDailyCeiling = dailyCeiling.HasValue ? Round(dailyCeiling.Value) : null;
        CssMonthlyCeiling = cssCeiling.HasValue ? Round(cssCeiling.Value) : null;
        AccidentWorkMonthlyCeiling = accidentCeiling.HasValue ? Round(accidentCeiling.Value) : null;
        IncrementVersion();
        return Result.Success();
    }

    public void SetStatutoryLeaveDefaults(
        int sickLeaveWaitingDays,
        decimal sickLeaveIjRatePercent,
        int maternityLeaveDurationDays,
        int paternityLeaveDurationDays,
        decimal maternityEmployerTopUpDefault)
    {
        SickLeaveWaitingDays = Math.Max(0, sickLeaveWaitingDays);
        SickLeaveIjRatePercent = Math.Round(sickLeaveIjRatePercent, 3);
        MaternityLeaveDurationDays = Math.Max(0, maternityLeaveDurationDays);
        PaternityLeaveDurationDays = Math.Max(0, paternityLeaveDurationDays);
        MaternityEmployerTopUpDefault = Math.Round(maternityEmployerTopUpDefault, 3);
        IncrementVersion();
    }

    private static decimal Round(decimal value) => Math.Round(value, 3);
}

/// <summary>
/// Une tranche du barème IRPP progressif : à partir de <see cref="LowerBound"/> (inclus)
/// le revenu net annuel imposable est taxé au taux <see cref="Rate"/> jusqu'à la borne
/// inférieure de la tranche suivante (dernière tranche = illimitée).
/// </summary>
public sealed class PayrollIrppBracket : Entity
{
    public Guid PayrollYearParametersId { get; private set; }
    /// <summary>Borne inférieure de la tranche (revenu net annuel, TND).</summary>
    public decimal LowerBound { get; private set; }
    /// <summary>Taux marginal de la tranche, en %.</summary>
    public decimal Rate { get; private set; }

    private PayrollIrppBracket() { }

    public static PayrollIrppBracket Create(decimal lowerBound, decimal rate)
    {
        if (lowerBound < 0)
            throw new ArgumentOutOfRangeException(nameof(lowerBound));
        if (rate < 0)
            throw new ArgumentOutOfRangeException(nameof(rate));

        return new PayrollIrppBracket
        {
            LowerBound = Math.Round(lowerBound, 3),
            Rate = Math.Round(rate, 3)
        };
    }

    internal void AssignParentId(Guid payrollYearParametersId)
    {
        if (payrollYearParametersId == Guid.Empty)
            throw new ArgumentException("L'identifiant des paramètres de paie est obligatoire.", nameof(payrollYearParametersId));

        PayrollYearParametersId = payrollYearParametersId;
    }
}

/// <summary>
/// Tranche de saisie sur salaire : à partir de <see cref="LowerBoundMonthlyNet"/> (inclus),
/// la fraction <see cref="SeizableFraction"/> du net mensuel dans la tranche est saisissable.
/// </summary>
public sealed class PayrollGarnishmentBracket : Entity
{
    public Guid PayrollYearParametersId { get; private set; }
    /// <summary>Borne inférieure de la tranche (net mensuel, TND).</summary>
    public decimal LowerBoundMonthlyNet { get; private set; }
    /// <summary>Fraction saisissable (0 à 1). Ex. 0.333 pour un tiers.</summary>
    public decimal SeizableFraction { get; private set; }

    private PayrollGarnishmentBracket() { }

    public static PayrollGarnishmentBracket Create(decimal lowerBoundMonthlyNet, decimal seizableFraction)
    {
        if (lowerBoundMonthlyNet < 0)
            throw new ArgumentOutOfRangeException(nameof(lowerBoundMonthlyNet));
        if (seizableFraction is < 0m or > 1m)
            throw new ArgumentOutOfRangeException(nameof(seizableFraction));

        return new PayrollGarnishmentBracket
        {
            LowerBoundMonthlyNet = Math.Round(lowerBoundMonthlyNet, 3),
            SeizableFraction = Math.Round(seizableFraction, 4)
        };
    }

    internal void AssignParentId(Guid payrollYearParametersId)
    {
        if (payrollYearParametersId == Guid.Empty)
            throw new ArgumentException("L'identifiant des paramètres de paie est obligatoire.", nameof(payrollYearParametersId));

        PayrollYearParametersId = payrollYearParametersId;
    }
}
