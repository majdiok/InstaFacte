using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Valeurs par défaut des paramètres de paie tunisiens (barème IRPP progressif issu de la
/// loi de finances, taux CNSS/CSS/TFP/FOPROLOS). Utilisées pour seeder l'exercice courant ;
/// restent modifiables par l'utilisateur. Aucune valeur légale n'est codée ailleurs.
/// </summary>
public static class PayrollParameterDefaults
{
    /// <summary>Barème IRPP 2025/2026 (8 tranches, 0 % à 40 %).</summary>
    public static IReadOnlyList<(decimal LowerBound, decimal Rate)> Irpp2026Brackets => new[]
    {
        (0m, 0m),
        (5000m, 15m),
        (10000m, 25m),
        (20000m, 30m),
        (30000m, 33m),
        (40000m, 36m),
        (50000m, 38m),
        (70000m, 40m)
    };

    /// <summary>Tranches de saisie sur salaire (convention tunisienne simplifiée).</summary>
    public static IReadOnlyList<(decimal LowerBoundMonthlyNet, decimal SeizableFraction)> Garnishment2026Brackets => new[]
    {
        (0m, 0m),
        (528.320m, 0.333m),
        (1056.640m, 0.666m)
    };

    /// <summary>
    /// Construit les paramètres par défaut d'un exercice donné (valeurs 2026).
    /// </summary>
    public static Result<PayrollYearParameters> CreateDefaults(int fiscalYear)
    {
        var brackets = Irpp2026Brackets
            .Select(b => PayrollIrppBracket.Create(b.LowerBound, b.Rate))
            .ToList();

        var garnishmentBrackets = Garnishment2026Brackets
            .Select(b => PayrollGarnishmentBracket.Create(b.LowerBoundMonthlyNet, b.SeizableFraction))
            .ToList();

        return PayrollYearParameters.Create(
            fiscalYear: fiscalYear,
            cnssEmployeeRate: 9.18m,
            cnssEmployerRate: 16.57m,
            cssRate: 0.5m,
            cssAnnualExemptionThreshold: 5000m,
            professionalExpensesRate: 10m,
            professionalExpensesAnnualCap: 2000m,
            headOfFamilyAnnualDeduction: 300m,
            childAnnualDeduction: 100m,
            maxDeductibleChildren: 4,
            tfpRateIndustry: 1m,
            tfpRateOther: 2m,
            foprolosRate: 1m,
            monthlySmig: 528.320m,
            irppBrackets: brackets,
            studentChildAnnualDeduction: 1000m,
            disabledChildAnnualDeduction: 2000m,
            parentDeductionRatePercent: 5m,
            parentAnnualDeductionCap: 450m,
            isIndustrialSector: false,
            mealVoucherDailyExemptionCap: 3.000m,
            garnishmentBrackets: garnishmentBrackets,
            cssEmployerRate: 0m);
    }
}
