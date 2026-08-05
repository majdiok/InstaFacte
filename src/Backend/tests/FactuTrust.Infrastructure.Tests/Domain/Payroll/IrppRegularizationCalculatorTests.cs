using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

/// <summary>
/// Golden tests du calcul de régularisation IRPP/CSS annuelle.
///
/// Propriété structurante : le barème progressif étant <b>convexe</b>, la méthode mensuelle
/// par projection (net imposable × 12) prélève toujours au moins autant que l'impôt réellement
/// dû sur le cumul annuel. La régularisation produit donc une <b>restitution</b> dès que la
/// rémunération varie, et un écart nul quand elle est constante.
/// </summary>
public sealed class IrppRegularizationCalculatorTests
{
    private static PayrollYearParameters Params() => PayrollParameterDefaults.CreateDefaults(2026).Value;

    /// <summary>Calcule le bulletin d'un mois et en extrait la contribution au cumul.</summary>
    private static IrppRegularizationMonth Month(int month, decimal baseSalary)
    {
        var computation = PayrollCalculator.Compute(
            new PayrollComputationInput
            {
                BaseSalary = baseSalary,
                Regime = SocialRegime.Rsna,
                WorkAccidentRate = 0.4m
            },
            Params());

        return new IrppRegularizationMonth(month, computation.MonthlyNetTaxable, computation.Irpp, computation.Css, baseSalary);
    }

    private static List<IrppRegularizationMonth> ConstantYear(decimal baseSalary, int months = 12) =>
        Enumerable.Range(1, months).Select(m => Month(m, baseSalary)).ToList();

    [Fact]
    public void SalaireConstantSurDouzeMois_EcartQuasiNul()
    {
        var months = ConstantYear(2000m);

        // Bulletin mensuel de référence (cf. PayrollCalculatorTests) :
        // CNSS 183,600 → base 1816,400 → frais pro plafonnés 166,667 → net imposable 1649,733
        // annualisé 19796,796 → IRPP 3199,199/12 = 266,600 ; CSS 98,984/12 = 8,249.
        Assert.Equal(1649.733m, months[0].MonthlyNetTaxable);
        Assert.Equal(266.600m, months[0].Irpp);
        Assert.Equal(8.249m, months[0].Css);

        var result = IrppRegularizationCalculator.Compute(months, Params());

        Assert.Equal(12, result.MonthsCounted);
        Assert.Equal(19796.796m, result.CumulNetTaxable);   // 1649,733 × 12
        Assert.Equal(3199.200m, result.CumulIrppWithheld);  // 266,600 × 12
        Assert.Equal(3199.199m, result.IrppDue);            // 750 + 9796,796 × 25 %
        Assert.Equal(-0.001m, result.IrppDelta);            // résidu d'arrondi au millime

        Assert.Equal(98.988m, result.CumulCssWithheld);     // 8,249 × 12
        Assert.Equal(98.984m, result.CssDue);               // 19796,796 × 0,5 %
        Assert.Equal(-0.004m, result.CssDelta);

        // À rémunération constante, la retenue mensuelle est déjà juste.
        Assert.True(Math.Abs(result.TotalDelta) <= 0.012m);
    }

    [Fact]
    public void PrimeExceptionnelle_ProduitUneRestitution()
    {
        // Onze mois à 2 000 puis un mois à 5 000 (prime de 3 000). Le mois de prime est taxé
        // comme s'il se répétait douze fois : la retenue de l'année dépasse l'impôt réellement dû.
        var months = ConstantYear(2000m, 11);
        months.Add(Month(12, 5000m));

        var result = IrppRegularizationCalculator.Compute(months, Params());

        Assert.True(result.IrppDelta < 0, "Une prime ponctuelle sur-taxée doit ouvrir droit à restitution.");
        Assert.False(result.IsAdditionalWithholding);

        // L'impôt dû se calcule bien sur le cumul réel, pas sur une projection.
        var expectedDue = PayrollCalculator.ComputeProgressiveTax(result.CumulNetTaxable, Params());
        Assert.Equal(expectedDue, result.IrppDue);
        Assert.Equal(result.IrppDue - result.CumulIrppWithheld, result.IrppDelta);
    }

    [Fact]
    public void EmbaucheEnCoursDAnnee_ProduitUneRestitutionImportante()
    {
        // Salarié embauché en octobre : trois bulletins seulement. Le barème annuel s'applique
        // au cumul réel (≈ 4 949) et non à une projection sur douze mois — comportement voulu,
        // documenté dans docs/paie.md.
        var months = new List<IrppRegularizationMonth>
        {
            Month(10, 2000m),
            Month(11, 2000m),
            Month(12, 2000m)
        };

        var result = IrppRegularizationCalculator.Compute(months, Params());

        Assert.Equal(3, result.MonthsCounted);
        Assert.Equal(4949.199m, result.CumulNetTaxable);  // 1649,733 × 3

        // Le cumul reste sous la première tranche imposable (5 000) : aucun impôt dû.
        Assert.Equal(0m, result.IrppDue);
        Assert.Equal(799.800m, result.CumulIrppWithheld); // 266,600 × 3
        Assert.Equal(-799.800m, result.IrppDelta);

        // Sous le seuil, la CSS est également intégralement restituée.
        Assert.Equal(0m, result.CssDue);
        Assert.Equal(-24.747m, result.CssDelta);          // 8,249 × 3
    }

    [Fact]
    public void CumulSousLeSeuilCss_NeGenereAucuneCss()
    {
        var months = new List<IrppRegularizationMonth> { Month(11, 1500m), Month(12, 1500m) };

        var result = IrppRegularizationCalculator.Compute(months, Params());

        Assert.True(result.CumulNetTaxable <= Params().CssAnnualExemptionThreshold);
        Assert.Equal(0m, result.CssDue);
    }

    [Fact]
    public void CumulAuDessusDuSeuilCss_AppliqueLeTaux()
    {
        var months = ConstantYear(2000m);
        var parameters = Params();

        var result = IrppRegularizationCalculator.Compute(months, parameters);

        Assert.True(result.CumulNetTaxable > parameters.CssAnnualExemptionThreshold);
        Assert.Equal(
            Math.Round(result.CumulNetTaxable * parameters.CssRate / 100m, 3, MidpointRounding.AwayFromZero),
            result.CssDue);
    }

    [Fact]
    public void CumulVide_ProduitUnResultatNeutre()
    {
        var result = IrppRegularizationCalculator.Compute([], Params());

        Assert.Equal(0, result.MonthsCounted);
        Assert.Equal(0m, result.CumulNetTaxable);
        Assert.Equal(0m, result.IrppDue);
        Assert.Equal(0m, result.IrppDelta);
        Assert.True(result.IsNeutral);
    }

    [Fact]
    public void Recalculer_DonneExactementLeMemeResultat()
    {
        // Idempotence : le cumul ne lit que l'IRPP mensuel pur, jamais les régularisations
        // déjà portées. Régénérer ne peut donc pas doubler le montant.
        var months = ConstantYear(2000m, 11);
        months.Add(Month(12, 4000m));
        var parameters = Params();

        var first = IrppRegularizationCalculator.Compute(months, parameters);
        var second = IrppRegularizationCalculator.Compute(months, parameters);

        Assert.Equal(first.IrppDelta, second.IrppDelta);
        Assert.Equal(first.CssDelta, second.CssDelta);
        Assert.Equal(first.CumulNetTaxable, second.CumulNetTaxable);
    }

    [Fact]
    public void MoisNonRemuneres_NEntrentPasDansLeCumul()
    {
        // Un mois sans bulletin (congé sans solde total, absence de cycle) n'apporte rien au
        // cumul : rien n'a été versé, rien n'a été retenu.
        var withGap = new List<IrppRegularizationMonth> { Month(1, 2000m), Month(3, 2000m) };

        var result = IrppRegularizationCalculator.Compute(withGap, Params());

        Assert.Equal(2, result.MonthsCounted);
        Assert.Equal(3299.466m, result.CumulNetTaxable); // 1649,733 × 2
    }

    private static PayrollYearParameters ParamsWithSmigMode(SmigIrppExemptionMode mode)
    {
        var p = Params();
        var result = p.UpdateRates(
            p.CnssEmployeeRate, p.CnssEmployerRate, p.CssRate, p.CssAnnualExemptionThreshold,
            p.ProfessionalExpensesRate, p.ProfessionalExpensesAnnualCap,
            p.HeadOfFamilyAnnualDeduction, p.ChildAnnualDeduction, p.MaxDeductibleChildren,
            p.TfpRateIndustry, p.TfpRateOther, p.FoprolosRate, p.MonthlySmig,
            p.CnssEmployeeRateRsa, p.CnssEmployerRateRsa,
            p.EnforceSmigOnContracts, p.EnableExtendedOvertimeRates, p.EnableAllowanceQuadrantMatrix,
            p.StudentChildAnnualDeduction, p.DisabledChildAnnualDeduction,
            p.ParentDeductionRatePercent, p.ParentAnnualDeductionCap, p.IsIndustrialSector,
            p.MealVoucherDailyExemptionCap, p.EnableIrppRegularization,
            smigIrppExemptionMode: mode);
        Assert.True(result.IsSuccess);
        return p;
    }

    [Fact]
    public void SmigPortion_ConstantSmigYear_RegularizationStaysNeutral()
    {
        var parameters = ParamsWithSmigMode(SmigIrppExemptionMode.SmigPortion);
        var months = Enumerable.Range(1, 12).Select(m =>
        {
            var computation = PayrollCalculator.Compute(
                new PayrollComputationInput { BaseSalary = 528.320m, Regime = SocialRegime.Rsna },
                parameters);
            return new IrppRegularizationMonth(m, computation.MonthlyNetTaxable, computation.Irpp, computation.Css, 528.320m);
        }).ToList();

        var result = IrppRegularizationCalculator.Compute(months, parameters);

        Assert.Equal(0m, result.IrppDue);
        Assert.Equal(0m, result.IrppDelta);
    }

    [Fact]
    public void FullIfBelow_ConstantSmigYear_RegularizationStaysNeutral()
    {
        var parameters = ParamsWithSmigMode(SmigIrppExemptionMode.FullIfBelow);
        var months = Enumerable.Range(1, 12).Select(m =>
        {
            var computation = PayrollCalculator.Compute(
                new PayrollComputationInput { BaseSalary = 528.320m, Regime = SocialRegime.Rsna },
                parameters);
            return new IrppRegularizationMonth(m, computation.MonthlyNetTaxable, computation.Irpp, computation.Css, 528.320m);
        }).ToList();

        var result = IrppRegularizationCalculator.Compute(months, parameters);

        Assert.Equal(0m, result.IrppDue);
        Assert.Equal(0m, result.IrppDelta);
    }
}
