using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Snapshot immuable des paramètres légaux de paie pour un exercice donné (loi de finances).
/// </summary>
public sealed record PayrollLegalPreset
{
    public required int FiscalYear { get; init; }
    public required string Label { get; init; }
    public required decimal CnssEmployeeRate { get; init; }
    public required decimal CnssEmployerRate { get; init; }
    public required decimal CnssEmployeeRateRsa { get; init; }
    public required decimal CnssEmployerRateRsa { get; init; }
    public required decimal CssRate { get; init; }
    public required decimal CssAnnualExemptionThreshold { get; init; }
    public required decimal CssEmployerRate { get; init; }
    public required decimal ProfessionalExpensesRate { get; init; }
    public required decimal ProfessionalExpensesAnnualCap { get; init; }
    public required decimal HeadOfFamilyAnnualDeduction { get; init; }
    public required decimal ChildAnnualDeduction { get; init; }
    public required int MaxDeductibleChildren { get; init; }
    public required decimal StudentChildAnnualDeduction { get; init; }
    public required decimal DisabledChildAnnualDeduction { get; init; }
    public required decimal ParentDeductionRatePercent { get; init; }
    public required decimal ParentAnnualDeductionCap { get; init; }
    public required decimal TfpRateIndustry { get; init; }
    public required decimal TfpRateOther { get; init; }
    public required decimal FoprolosRate { get; init; }
    public required decimal MonthlySmig { get; init; }
    public required IReadOnlyList<(decimal LowerBound, decimal Rate)> IrppBrackets { get; init; }
    public required IReadOnlyList<(decimal LowerBoundMonthlyNet, decimal SeizableFraction)> GarnishmentBrackets { get; init; }
    public decimal? CnssMonthlyCeiling { get; init; }
    public int SickLeaveWaitingDays { get; init; } = 5;
    public decimal SickLeaveIjRatePercent { get; init; } = 66.67m;
    public int MaternityLeaveDurationDays { get; init; } = 60;
    public int PaternityLeaveDurationDays { get; init; } = 2;
    public decimal MaternityEmployerTopUpDefault { get; init; } = 100m;

    public Result<PayrollYearParameters> Materialize(int fiscalYear)
    {
        var brackets = IrppBrackets
            .Select(b => PayrollIrppBracket.Create(b.LowerBound, b.Rate))
            .ToList();
        var garnishment = GarnishmentBrackets
            .Select(b => PayrollGarnishmentBracket.Create(b.LowerBoundMonthlyNet, b.SeizableFraction))
            .ToList();

        var result = PayrollYearParameters.Create(
            fiscalYear: fiscalYear,
            cnssEmployeeRate: CnssEmployeeRate,
            cnssEmployerRate: CnssEmployerRate,
            cssRate: CssRate,
            cssAnnualExemptionThreshold: CssAnnualExemptionThreshold,
            professionalExpensesRate: ProfessionalExpensesRate,
            professionalExpensesAnnualCap: ProfessionalExpensesAnnualCap,
            headOfFamilyAnnualDeduction: HeadOfFamilyAnnualDeduction,
            childAnnualDeduction: ChildAnnualDeduction,
            maxDeductibleChildren: MaxDeductibleChildren,
            tfpRateIndustry: TfpRateIndustry,
            tfpRateOther: TfpRateOther,
            foprolosRate: FoprolosRate,
            monthlySmig: MonthlySmig,
            irppBrackets: brackets,
            studentChildAnnualDeduction: StudentChildAnnualDeduction,
            disabledChildAnnualDeduction: DisabledChildAnnualDeduction,
            parentDeductionRatePercent: ParentDeductionRatePercent,
            parentAnnualDeductionCap: ParentAnnualDeductionCap,
            cnssEmployeeRateRsa: CnssEmployeeRateRsa,
            cnssEmployerRateRsa: CnssEmployerRateRsa,
            garnishmentBrackets: garnishment,
            cssEmployerRate: CssEmployerRate);

        if (result.IsFailure)
            return result;

        if (CnssMonthlyCeiling.HasValue)
            result.Value.SetCnssMonthlyCeiling(CnssMonthlyCeiling);

        result.Value.SetStatutoryLeaveDefaults(
            SickLeaveWaitingDays,
            SickLeaveIjRatePercent,
            MaternityLeaveDurationDays,
            PaternityLeaveDurationDays,
            MaternityEmployerTopUpDefault);

        return result;
    }
}
