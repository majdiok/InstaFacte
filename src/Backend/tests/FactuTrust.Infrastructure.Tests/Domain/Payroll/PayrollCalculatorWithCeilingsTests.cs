using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

public sealed class PayrollCalculatorWithCeilingsTests
{
  private static PayrollYearParameters ParametersWithCeiling(decimal ceiling)
  {
    var p = PayrollParameterDefaults.CreateDefaults(2026).Value;
    p.UpdateCnssCeilings(ceiling, null, null, null);
    return p;
  }

  [Fact]
  public void WithoutCeiling_MatchesHistoricalCnss()
  {
    var parameters = PayrollParameterDefaults.CreateDefaults(2026).Value;
    var input = SampleInput(baseSalary: 5000m);
    var result = PayrollCalculator.Compute(input, parameters);
    Assert.Equal(459.000m, result.CnssEmployee);
  }

  [Fact]
  public void WithCeiling_CapsCnssContributionBase()
  {
    var parameters = ParametersWithCeiling(3000m);
    var input = SampleInput(baseSalary: 5000m);
    var result = PayrollCalculator.Compute(input, parameters);
    Assert.Equal(275.400m, result.CnssEmployee);
    Assert.Equal(497.100m, result.CnssEmployer);
  }

  [Fact]
  public void ApplyCnssCeiling_NullCeiling_ReturnsFullGross()
  {
    var parameters = PayrollParameterDefaults.CreateDefaults(2026).Value;
    Assert.Equal(5000m, PayrollCalculator.ApplyCnssCeiling(5000m, parameters));
  }

  private static PayrollComputationInput SampleInput(decimal baseSalary) => new()
  {
    BaseSalary = baseSalary,
    Regime = SocialRegime.Rsna,
    WorkAccidentRate = 0.4m
  };
}
