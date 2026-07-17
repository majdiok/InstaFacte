using FactuTrust.Domain.Common;

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

    /// <summary>Taux de la Contribution Sociale de Solidarité (CSS), en %. Ex. 0.5.</summary>
    public decimal CssRate { get; private set; }
    /// <summary>Seuil annuel (TND) en dessous duquel la CSS ne s'applique pas (tranche exonérée IRPP). Ex. 5000.</summary>
    public decimal CssAnnualExemptionThreshold { get; private set; }

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

    /// <summary>SMIG mensuel indicatif (TND), pour contrôle de cohérence. Optionnel.</summary>
    public decimal MonthlySmig { get; private set; }

    private readonly List<PayrollIrppBracket> _irppBrackets = new();
    /// <summary>Tranches du barème IRPP progressif, triées par borne inférieure croissante.</summary>
    public IReadOnlyCollection<PayrollIrppBracket> IrppBrackets => _irppBrackets.AsReadOnly();

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
        bool isIndustrialSector = false)
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

        var rsaEmployeeRate = cnssEmployeeRateRsa ?? cnssEmployeeRate;
        var rsaEmployerRate = cnssEmployerRateRsa ?? cnssEmployerRate;

        var negativeRates = new[]
        {
            cnssEmployeeRate, cnssEmployerRate, rsaEmployeeRate, rsaEmployerRate,
            cssRate, professionalExpensesRate, tfpRateIndustry, tfpRateOther, foprolosRate,
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
            CssRate = Round(cssRate),
            CssAnnualExemptionThreshold = Round(cssAnnualExemptionThreshold),
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
            MonthlySmig = Round(monthlySmig)
        };
        entity._irppBrackets.AddRange(brackets);
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
        bool isIndustrialSector = false)
    {
        var rates = new[]
        {
            cnssEmployeeRate, cnssEmployerRate, cnssEmployeeRateRsa, cnssEmployerRateRsa,
            cssRate, professionalExpensesRate, tfpRateIndustry, tfpRateOther, foprolosRate,
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
        CssRate = Round(cssRate);
        CssAnnualExemptionThreshold = Round(cssAnnualExemptionThreshold);
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
        IncrementVersion();
        return Result.Success();
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
