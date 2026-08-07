using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Services.Payroll;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Queries;

public sealed record PayrollLegalPresetDto
{
    public int FiscalYear { get; init; }
    public string Label { get; init; } = null!;
    public decimal CnssEmployeeRate { get; init; }
    public decimal CnssEmployerRate { get; init; }
    public decimal CssRate { get; init; }
    public decimal CssAnnualExemptionThreshold { get; init; }
    public decimal CssEmployerRate { get; init; }
    public decimal ProfessionalExpensesRate { get; init; }
    public decimal ProfessionalExpensesAnnualCap { get; init; }
    public decimal HeadOfFamilyAnnualDeduction { get; init; }
    public decimal ChildAnnualDeduction { get; init; }
    public int MaxDeductibleChildren { get; init; }
    public decimal StudentChildAnnualDeduction { get; init; }
    public decimal DisabledChildAnnualDeduction { get; init; }
    public decimal ParentDeductionRatePercent { get; init; }
    public decimal ParentAnnualDeductionCap { get; init; }
    public decimal TfpRateIndustry { get; init; }
    public decimal TfpRateOther { get; init; }
    public decimal FoprolosRate { get; init; }
    public decimal MonthlySmig { get; init; }
    public IReadOnlyList<IrppBracketDto> IrppBrackets { get; init; } = Array.Empty<IrppBracketDto>();
    public int SickLeaveWaitingDays { get; init; }
    public decimal SickLeaveIjRatePercent { get; init; }
    public int MaternityLeaveDurationDays { get; init; }
    public int PaternityLeaveDurationDays { get; init; }
    public decimal MaternityEmployerTopUpDefault { get; init; }
    public decimal? CnssMonthlyCeiling { get; init; }
}

public sealed record GetPayrollLegalPresetQuery(int FiscalYear) : IRequest<PayrollLegalPresetDto>;

public sealed class GetPayrollLegalPresetQueryHandler : IRequestHandler<GetPayrollLegalPresetQuery, PayrollLegalPresetDto>
{
    public Task<PayrollLegalPresetDto> Handle(GetPayrollLegalPresetQuery request, CancellationToken cancellationToken)
    {
        var preset = PayrollParameterDefaults.GetPreset(request.FiscalYear);
        return Task.FromResult(Map(preset));
    }

    internal static PayrollLegalPresetDto Map(PayrollLegalPreset preset) => new()
    {
        FiscalYear = preset.FiscalYear,
        Label = preset.Label,
        CnssEmployeeRate = preset.CnssEmployeeRate,
        CnssEmployerRate = preset.CnssEmployerRate,
        CssRate = preset.CssRate,
        CssAnnualExemptionThreshold = preset.CssAnnualExemptionThreshold,
        CssEmployerRate = preset.CssEmployerRate,
        ProfessionalExpensesRate = preset.ProfessionalExpensesRate,
        ProfessionalExpensesAnnualCap = preset.ProfessionalExpensesAnnualCap,
        HeadOfFamilyAnnualDeduction = preset.HeadOfFamilyAnnualDeduction,
        ChildAnnualDeduction = preset.ChildAnnualDeduction,
        MaxDeductibleChildren = preset.MaxDeductibleChildren,
        StudentChildAnnualDeduction = preset.StudentChildAnnualDeduction,
        DisabledChildAnnualDeduction = preset.DisabledChildAnnualDeduction,
        ParentDeductionRatePercent = preset.ParentDeductionRatePercent,
        ParentAnnualDeductionCap = preset.ParentAnnualDeductionCap,
        TfpRateIndustry = preset.TfpRateIndustry,
        TfpRateOther = preset.TfpRateOther,
        FoprolosRate = preset.FoprolosRate,
        MonthlySmig = preset.MonthlySmig,
        IrppBrackets = preset.IrppBrackets
            .Select(b => new IrppBracketDto { LowerBound = b.LowerBound, Rate = b.Rate })
            .ToList(),
        SickLeaveWaitingDays = preset.SickLeaveWaitingDays,
        SickLeaveIjRatePercent = preset.SickLeaveIjRatePercent,
        MaternityLeaveDurationDays = preset.MaternityLeaveDurationDays,
        PaternityLeaveDurationDays = preset.PaternityLeaveDurationDays,
        MaternityEmployerTopUpDefault = preset.MaternityEmployerTopUpDefault,
        CnssMonthlyCeiling = preset.CnssMonthlyCeiling
    };
}
