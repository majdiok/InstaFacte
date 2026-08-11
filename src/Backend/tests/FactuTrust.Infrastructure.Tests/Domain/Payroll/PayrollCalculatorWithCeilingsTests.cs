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

  [Fact]
  public void WithCeiling_PayrollTaxesUseTheUncappedBase()
  {
    // La TFP, le FOPROLOS et la CSS patronale n'ont pas de plafond légal : le plafond CNSS ne
    // doit pas réduire leur assiette. Les présets légaux livrent ce comportement.
    var parameters = ParametersWithCeiling(3000m);
    var input = SampleInput(baseSalary: 5000m);

    var result = PayrollCalculator.Compute(input, parameters);

    Assert.Equal(5000m, result.PayrollTaxBase);
    Assert.Equal(100.000m, result.Tfp);       // 5 000 × 2 % (secteur non industriel)
    Assert.Equal(50.000m, result.Foprolos);   // 5 000 × 1 %
    // La CNSS, elle, reste bien plafonnée.
    Assert.Equal(275.400m, result.CnssEmployee);
  }

  [Fact]
  public void WithCeiling_LegacyParameters_KeepTheCappedBase()
  {
    // Non-régression : un exercice déjà en base conserve l'assiette plafonnée, donc ses montants.
    var parameters = ParametersWithCeiling(3000m);
    parameters.SetApplyCnssCeilingToPayrollTaxes(true);
    var input = SampleInput(baseSalary: 5000m);

    var result = PayrollCalculator.Compute(input, parameters);

    Assert.Equal(3000m, result.PayrollTaxBase);
    Assert.Equal(60.000m, result.Tfp);        // 3 000 × 2 %
    Assert.Equal(30.000m, result.Foprolos);   // 3 000 × 1 %
  }

  [Fact]
  public void Compute_FreezesTheAppliedTfpRate()
  {
    // Le taux est figé sur le bulletin : le formulaire officiel choisit la ligne 1 % / 2 % avec,
    // sans dépendre de paramètres modifiables après coup.
    var parameters = PayrollParameterDefaults.CreateDefaults(2026).Value;

    var result = PayrollCalculator.Compute(SampleInput(baseSalary: 5000m), parameters);

    Assert.Equal(parameters.TfpRateOther, result.AppliedTfpRate);
  }

  private static PayrollComputationInput SampleInput(decimal baseSalary) => new()
  {
    BaseSalary = baseSalary,
    Regime = SocialRegime.Rsna,
    WorkAccidentRate = 0.4m
  };
}
