using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.Commands;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

public sealed class PayrollParametersValidationTests
{
    private static UpdatePayrollParametersDto ValidDto(
        IReadOnlyList<IrppBracketDto>? brackets = null,
        decimal cnssEmployeeRate = 9.18m,
        decimal parentDeductionRatePercent = 5m,
        int maxDeductibleChildren = 4)
        => new()
        {
            CnssEmployeeRate = cnssEmployeeRate,
            CnssEmployerRate = 16.57m,
            CnssEmployeeRateRsa = 9.18m,
            CnssEmployerRateRsa = 16.57m,
            CssRate = 0.5m,
            CssAnnualExemptionThreshold = 5000m,
            ProfessionalExpensesRate = 10m,
            ProfessionalExpensesAnnualCap = 2000m,
            HeadOfFamilyAnnualDeduction = 300m,
            ChildAnnualDeduction = 100m,
            MaxDeductibleChildren = maxDeductibleChildren,
            StudentChildAnnualDeduction = 1000m,
            DisabledChildAnnualDeduction = 2000m,
            ParentDeductionRatePercent = parentDeductionRatePercent,
            ParentAnnualDeductionCap = 450m,
            TfpRateIndustry = 1m,
            TfpRateOther = 2m,
            FoprolosRate = 1m,
            MonthlySmig = 528.320m,
            IrppBrackets = brackets ?? new[]
            {
                new IrppBracketDto { LowerBound = 0m, Rate = 0m },
                new IrppBracketDto { LowerBound = 5000m, Rate = 15m }
            }
        };

    private static readonly UpdatePayrollParametersCommandValidator Validator = new();

    [Fact]
    public void Validator_accepts_valid_parameters()
    {
        var result = Validator.Validate(new UpdatePayrollParametersCommand(2026, ValidDto()));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validator_rejects_rate_above_100()
    {
        var result = Validator.Validate(new UpdatePayrollParametersCommand(2026, ValidDto(cnssEmployeeRate: 101m)));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validator_rejects_negative_parent_rate()
    {
        var result = Validator.Validate(new UpdatePayrollParametersCommand(2026, ValidDto(parentDeductionRatePercent: -1m)));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validator_rejects_max_children_above_10()
    {
        var result = Validator.Validate(new UpdatePayrollParametersCommand(2026, ValidDto(maxDeductibleChildren: 11)));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validator_rejects_duplicate_bracket_bounds()
    {
        var brackets = new[]
        {
            new IrppBracketDto { LowerBound = 0m, Rate = 0m },
            new IrppBracketDto { LowerBound = 5000m, Rate = 15m },
            new IrppBracketDto { LowerBound = 5000m, Rate = 25m }
        };
        var result = Validator.Validate(new UpdatePayrollParametersCommand(2026, ValidDto(brackets)));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validator_rejects_bareme_not_starting_at_zero()
    {
        var brackets = new[] { new IrppBracketDto { LowerBound = 1000m, Rate = 15m } };
        var result = Validator.Validate(new UpdatePayrollParametersCommand(2026, ValidDto(brackets)));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validator_rejects_bracket_rate_above_100()
    {
        var brackets = new[]
        {
            new IrppBracketDto { LowerBound = 0m, Rate = 0m },
            new IrppBracketDto { LowerBound = 5000m, Rate = 150m }
        };
        var result = Validator.Validate(new UpdatePayrollParametersCommand(2026, ValidDto(brackets)));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ReplaceIrppBrackets_rejects_duplicate_bounds()
    {
        var parameters = PayrollParameterDefaults.CreateDefaults(2026).Value;
        var duplicated = new[]
        {
            PayrollIrppBracket.Create(0m, 0m),
            PayrollIrppBracket.Create(5000m, 15m),
            PayrollIrppBracket.Create(5000m, 25m)
        };

        var result = parameters.ReplaceIrppBrackets(duplicated);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.IrppBrackets", result.Error.Code);
    }

    [Fact]
    public void ReplaceIrppBrackets_rejects_rate_out_of_range()
    {
        var parameters = PayrollParameterDefaults.CreateDefaults(2026).Value;
        var invalid = new[]
        {
            PayrollIrppBracket.Create(0m, 0m),
            PayrollIrppBracket.Create(5000m, 120m)
        };

        var result = parameters.ReplaceIrppBrackets(invalid);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ReplaceIrppBrackets_assigns_parent_id_on_persisted_aggregate()
    {
        var parameters = PayrollParameterDefaults.CreateDefaults(2026).Value;
        var brackets = new[]
        {
            PayrollIrppBracket.Create(0m, 0m),
            PayrollIrppBracket.Create(5000m, 15m)
        };

        var result = parameters.ReplaceIrppBrackets(brackets);

        Assert.True(result.IsSuccess);
        Assert.All(parameters.IrppBrackets, b => Assert.Equal(parameters.Id, b.PayrollYearParametersId));
    }
}
