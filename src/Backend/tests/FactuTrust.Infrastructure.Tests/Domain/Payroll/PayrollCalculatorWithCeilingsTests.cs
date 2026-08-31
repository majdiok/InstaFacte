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
    Assert.Equal(484.000m, result.CnssEmployee); // 5 000 × 9,68 % (RSNA depuis le 01/01/2025)
  }

  [Fact]
  public void WithCeiling_CapsCnssContributionBase()
  {
    var parameters = ParametersWithCeiling(3000m);
    var input = SampleInput(baseSalary: 5000m);
    var result = PayrollCalculator.Compute(input, parameters);
    Assert.Equal(290.400m, result.CnssEmployee); // 3 000 × 9,68 %
    Assert.Equal(512.100m, result.CnssEmployer); // 3 000 × 17,07 %
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
    Assert.Equal(290.400m, result.CnssEmployee); // 3 000 × 9,68 %
  }

  [Fact]
  public void WithCeiling_LegacyParameters_KeepTheCappedBase()
  {
    // Non-régression : un exercice déjà en base conserve l'assiette plafonnée, donc ses montants.
    // Un exercice legacy a PayrollTaxBaseMode=Legacy (défaut de colonne) + ApplyCnssCeilingToPayrollTaxes=true.
    var parameters = ParametersWithCeiling(3000m);
    parameters.SetPayrollTaxBaseMode(PayrollTaxBaseMode.Legacy);
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
