using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

public sealed class PayrollCalculatorTests
{
    private static PayrollYearParameters Params()
    {
        var result = PayrollParameterDefaults.CreateDefaults(2026);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    [Fact]
    public void Compute_SimpleSalary_Rsna_MatchesHandComputedValues()
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m,
            IsHeadOfFamily = false,
            DependentChildren = 0
        };

        var c = PayrollCalculator.Compute(input, Params());

        Assert.Equal(2000m, c.GrossSalary);
        Assert.Equal(193.600m, c.CnssEmployee);          // 2000 * 9.68% (RSNA depuis le 01/01/2025, LF 2025)
        Assert.Equal(1806.400m, c.TaxableBaseAfterCnss);
        Assert.Equal(166.667m, c.ProfessionalExpenses);  // capped at 2000/12
        Assert.Equal(1639.733m, c.MonthlyNetTaxable);
        Assert.Equal(264.100m, c.Irpp);                  // (750 + 9676,796 × 25 %) / 12
        Assert.Equal(8.199m, c.Css);
        Assert.Equal(1534.101m, c.NetSalary);
        Assert.Equal(341.400m, c.CnssEmployer);          // 2000 * 17.07%
        Assert.Equal(8.000m, c.WorkAccidentContribution);// 2000 * 0.4%
        Assert.Equal(40.000m, c.Tfp);                    // 2000 * 2% (non-industrial)
        Assert.Equal(20.000m, c.Foprolos);               // 2000 * 1%
    }

    [Fact]
    public void Compute_SivpExonere_HasNoCnss()
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = 1500m,
            Regime = SocialRegime.SivpExonere,
            WorkAccidentRate = 1m
        };

        var c = PayrollCalculator.Compute(input, Params());

        Assert.Equal(0m, c.CnssEmployee);
        Assert.Equal(0m, c.CnssEmployer);
        Assert.Equal(0m, c.WorkAccidentContribution);
        // Base imposable == brut puisque pas de CNSS.
        Assert.Equal(1500m, c.TaxableBaseAfterCnss);
    }

    [Fact]
    public void Compute_LowSalary_FullyExempt_NoIrppNoCss()
    {
        // Un salaire faible dont le net imposable annuel reste sous 5000 DT.
        var input = new PayrollComputationInput
        {
            BaseSalary = 400m,
            Regime = SocialRegime.Rsna
        };

        var c = PayrollCalculator.Compute(input, Params());

        Assert.True(c.AnnualNetTaxable < 5000m);
        Assert.Equal(0m, c.Irpp);
        Assert.Equal(0m, c.Css);
    }

    [Fact]
    public void Compute_HighSalary_ProfessionalExpensesCapped()
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = 8000m,
            Regime = SocialRegime.Rsna
        };

        var c = PayrollCalculator.Compute(input, Params());

        // Plafond mensuel = 2000/12 = 166.667.
        Assert.Equal(166.667m, c.ProfessionalExpenses);
    }

    [Fact]
    public void Compute_HeadOfFamilyWithChildren_ReducesIrpp()
    {
        var baseInput = new PayrollComputationInput { BaseSalary = 3000m, Regime = SocialRegime.Rsna };
        var withFamily = new PayrollComputationInput
        {
            BaseSalary = 3000m,
            Regime = SocialRegime.Rsna,
            IsHeadOfFamily = true,
            DependentChildren = 3
        };

        var pars = Params();
        var single = PayrollCalculator.Compute(baseInput, pars);
        var family = PayrollCalculator.Compute(withFamily, pars);

        Assert.True(family.Irpp < single.Irpp);
        Assert.True(family.NetSalary > single.NetSalary);
    }

    [Fact]
    public void Compute_ProrataDeduction_ReducesGross()
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            ProrataDeductionAmount = 300m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m
        };

        var c = PayrollCalculator.Compute(input, Params());

        Assert.Equal(1700m, c.GrossSalary);
    }

    [Fact]
    public void Compute_UnpaidAbsence_ReducesGross()
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            UnpaidAbsenceAmount = 200m,
            Regime = SocialRegime.Rsna
        };

        var c = PayrollCalculator.Compute(input, Params());

        Assert.Equal(1800m, c.CnssableGross);
    }

    [Fact]
    public void Compute_NonTaxableAllowance_AddedToNetButNotTaxed()
    {
        var without = new PayrollComputationInput { BaseSalary = 2000m, Regime = SocialRegime.Rsna };
        var with = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            NonTaxableAllowances = 100m,
            Regime = SocialRegime.Rsna
        };

        var pars = Params();
        var a = PayrollCalculator.Compute(without, pars);
        var b = PayrollCalculator.Compute(with, pars);

        // L'IRPP ne change pas (non imposable), mais le net augmente de 100.
        Assert.Equal(a.Irpp, b.Irpp);
        Assert.Equal(a.NetSalary + 100m, b.NetSalary);
    }

    [Theory]
    [InlineData(5000, 0)]        // Entièrement dans la tranche 0 %.
    [InlineData(10000, 750)]     // 5000 * 15%.
    [InlineData(20000, 3250)]    // 750 + 10000*25%.
    [InlineData(80000, 24750)]   // Toutes tranches + 10000*40%.
    public void ComputeProgressiveTax_MatchesBareme(decimal annualIncome, decimal expectedTax)
    {
        var tax = PayrollCalculator.ComputeProgressiveTax(annualIncome, Params());
        Assert.Equal(expectedTax, tax);
    }

    [Fact]
    public void Compute_ZeroSalary_ProducesZeros()
    {
        var c = PayrollCalculator.Compute(new PayrollComputationInput { BaseSalary = 0m }, Params());

        Assert.Equal(0m, c.GrossSalary);
        Assert.Equal(0m, c.CnssEmployee);
        Assert.Equal(0m, c.Irpp);
        Assert.Equal(0m, c.Css);
        Assert.Equal(0m, c.NetSalary);
    }

    [Fact]
    public void Compute_OtherDeductions_ReduceNet()
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            OtherDeductions = 150m,
            Regime = SocialRegime.Rsna
        };

        var reference = PayrollCalculator.Compute(new PayrollComputationInput { BaseSalary = 2000m, Regime = SocialRegime.Rsna }, Params());
        var c = PayrollCalculator.Compute(input, Params());

        Assert.Equal(reference.NetSalary - 150m, c.NetSalary);
    }

    [Fact]
    public void Compute_OvertimeAmount_IncreasesCnssableGrossAndNet()
    {
        var without = new PayrollComputationInput { BaseSalary = 2000m, Regime = SocialRegime.Rsna };
        var withOvertime = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            OvertimeAmount = 100m,
            Regime = SocialRegime.Rsna
        };

        var pars = Params();
        var a = PayrollCalculator.Compute(without, pars);
        var b = PayrollCalculator.Compute(withOvertime, pars);

        Assert.Equal(2100m, b.CnssableGross);
        Assert.True(b.NetSalary > a.NetSalary);
        Assert.True(b.CnssEmployee > a.CnssEmployee);
    }

    [Fact]
    public void Compute_RsaRegime_UsesRsaCnssRates()
    {
        var pars = Params();
        var updateResult = pars.UpdateRates(
            pars.CnssEmployeeRate,
            pars.CnssEmployerRate,
            pars.CssRate,
            pars.CssAnnualExemptionThreshold,
            pars.ProfessionalExpensesRate,
            pars.ProfessionalExpensesAnnualCap,
            pars.HeadOfFamilyAnnualDeduction,
            pars.ChildAnnualDeduction,
            pars.MaxDeductibleChildren,
            pars.TfpRateIndustry,
            pars.TfpRateOther,
            pars.FoprolosRate,
            pars.MonthlySmig,
            cnssEmployeeRateRsa: 5m,
            cnssEmployerRateRsa: 10m,
            enforceSmigOnContracts: false,
            enableExtendedOvertimeRates: false,
            enableAllowanceQuadrantMatrix: false);
        Assert.True(updateResult.IsSuccess);

        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsa
        };

        var c = PayrollCalculator.Compute(input, pars);

        Assert.Equal(100m, c.CnssEmployee);
        Assert.Equal(200m, c.CnssEmployer);
    }

    [Fact]
    public void Compute_QuadrantMatrix_TaxableOnly_IsTaxedButNotCnssSubject()
    {
        var pars = Params();
        var updateResult = pars.UpdateRates(
            pars.CnssEmployeeRate,
            pars.CnssEmployerRate,
            pars.CssRate,
            pars.CssAnnualExemptionThreshold,
            pars.ProfessionalExpensesRate,
            pars.ProfessionalExpensesAnnualCap,
            pars.HeadOfFamilyAnnualDeduction,
            pars.ChildAnnualDeduction,
            pars.MaxDeductibleChildren,
            pars.TfpRateIndustry,
            pars.TfpRateOther,
            pars.FoprolosRate,
            pars.MonthlySmig,
            pars.CnssEmployeeRateRsa,
            pars.CnssEmployerRateRsa,
            enforceSmigOnContracts: false,
            enableExtendedOvertimeRates: false,
            enableAllowanceQuadrantMatrix: true);
        Assert.True(updateResult.IsSuccess);

        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            TaxableOnlyAllowances = 100m,
            Regime = SocialRegime.Rsna
        };

        var without = PayrollCalculator.Compute(new PayrollComputationInput { BaseSalary = 2000m, Regime = SocialRegime.Rsna }, pars);
        var with = PayrollCalculator.Compute(input, pars);

        Assert.Equal(2000m, with.CnssableGross);
        Assert.Equal(2100m, with.GrossSalary);
        Assert.Equal(without.CnssEmployee, with.CnssEmployee);
        Assert.True(with.Irpp > without.Irpp);
    }

    [Fact]
    public void Compute_MonthlySmig_MatchesHandComputedValues()
    {
        // SMIG 2026 = 554,736 (décret n° 2026-67) ; CNSS RSNA 9,68 % depuis le 01/01/2025.
        // Le net imposable annuel (5411,208) dépasse la tranche à 0 %.
        var input = new PayrollComputationInput
        {
            BaseSalary = 554.736m,
            Regime = SocialRegime.Rsna
        };

        var c = PayrollCalculator.Compute(input, Params());

        Assert.Equal(53.698m, c.CnssEmployee);         // 554,736 × 9,68 %
        Assert.Equal(501.038m, c.TaxableBaseAfterCnss);
        Assert.Equal(50.104m, c.ProfessionalExpenses); // 10 %, sous le plafond
        Assert.Equal(450.934m, c.MonthlyNetTaxable);
        Assert.Equal(5411.208m, c.AnnualNetTaxable);
        Assert.Equal(5.140m, c.Irpp);                  // (5411,208 − 5000) × 15 % ÷ 12
        Assert.Equal(2.255m, c.Css);                   // 5411,208 × 0,5 % ÷ 12
        Assert.Equal(493.643m, c.NetSalary);
    }

    [Fact]
    public void Compute_CssExemptionThreshold_EdgeCases()
    {
        var pars = Params();

        // 509 TND/mois : net imposable annuel 4965,072 ≤ 5000 → IRPP et CSS exonérés
        // (CNSS RSNA 9,68 % depuis 2025, d'où le seuil de franchissement ~512,5 TND/mois).
        var below = PayrollCalculator.Compute(new PayrollComputationInput { BaseSalary = 509m, Regime = SocialRegime.Rsna }, pars);
        Assert.True(below.AnnualNetTaxable <= 5000m);
        Assert.Equal(0m, below.Irpp);
        Assert.Equal(0m, below.Css);

        // 513 TND/mois : net imposable annuel 5004,096 > 5000 → CSS due sur la totalité.
        var above = PayrollCalculator.Compute(new PayrollComputationInput { BaseSalary = 513m, Regime = SocialRegime.Rsna }, pars);
        Assert.True(above.AnnualNetTaxable > 5000m);
        Assert.True(above.Css > 0m);
    }

    // ── Déductions familiales étendues (art. 40 code IRPP) ──

    [Fact]
    public void Compute_ExtendedFamilyDeductions_MatchesHandComputedValues()
    {
        // Chef de famille, 4 enfants dont 1 étudiant (1000) et 1 infirme (2000, hors plafond),
        // 1 parent à charge (5 % du revenu net annuel plafonné à 450).
        var input = new PayrollComputationInput
        {
            BaseSalary = 3000m,
            Regime = SocialRegime.Rsna,
            IsHeadOfFamily = true,
            DependentChildren = 4,
            StudentChildren = 1,
            DisabledChildren = 1,
            DependentParents = 1
        };

        var c = PayrollCalculator.Compute(input, Params());

        // 300 (chef) + 2000 (infirme) + 1000 (étudiant) + 2 × 100 (ordinaires) + 450 (parent plafonné) = 3950/an
        Assert.Equal(329.167m, c.FamilyDeductions);
    }

    [Fact]
    public void Compute_ParentDeduction_BelowCap_UsesFivePercentOfNetIncome()
    {
        // Salaire faible : 5 % du revenu net annuel reste sous le plafond de 450.
        // CNSS RSNA 9,68 % (LF 2025) : (800 − 77,440) × 0,9 × 12 = 7803,648 ; × 5 % = 390,182 ; ÷ 12 = 32,515
        var input = new PayrollComputationInput
        {
            BaseSalary = 800m,
            Regime = SocialRegime.Rsna,
            DependentParents = 1
        };

        var c = PayrollCalculator.Compute(input, Params());

        Assert.Equal(32.515m, c.FamilyDeductions);
    }

    [Fact]
    public void Compute_StudentChildren_ConsumeCapBeforeOrdinaryChildren()
    {
        // 5 enfants dont 4 étudiants : le plafond de 4 est entièrement consommé par les
        // étudiants (déduction la plus favorable), l'enfant ordinaire n'ouvre plus droit.
        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsna,
            DependentChildren = 5,
            StudentChildren = 4
        };

        var c = PayrollCalculator.Compute(input, Params());

        Assert.Equal(333.333m, c.FamilyDeductions); // 4 × 1000 / 12
    }

    [Fact]
    public void Compute_DefaultFamilyCounts_MatchLegacyDeduction()
    {
        // Non-régression : sans étudiants/infirmes/parents, la déduction reste chef + enfants × 100.
        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsna,
            IsHeadOfFamily = true,
            DependentChildren = 2
        };

        var c = PayrollCalculator.Compute(input, Params());

        Assert.Equal(41.667m, c.FamilyDeductions); // (300 + 200) / 12
    }

    // ── TFP par secteur ──

    [Fact]
    public void Compute_IndustrialSector_UsesReducedTfpRate()
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsna,
            IsIndustrialSector = true
        };

        var c = PayrollCalculator.Compute(input, Params());

        Assert.Equal(20.000m, c.Tfp); // 2000 × 1 % (industrie) au lieu de 2 %
    }

    // ── Base / Taux des lignes de bulletin ──

    [Fact]
    public void Compute_Lines_CarryBaseAndRateForStatutoryDeductions()
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m
        };

        var c = PayrollCalculator.Compute(input, Params());

        // Frais pro plafonnés (181,640 > 166,667) : libellé dédié, pas de couple base × taux.
        var fraisPro = Assert.Single(c.Lines, l => l.Label.StartsWith("Frais professionnels"));
        Assert.Equal("Frais professionnels (plafonnés)", fraisPro.Label);
        Assert.Null(fraisPro.Base);
        Assert.Null(fraisPro.Rate);

        var irpp = Assert.Single(c.Lines, l => l.Label == "Retenue IRPP");
        Assert.Equal(1639.733m, irpp.Base);
        Assert.Null(irpp.Rate);

        var css = Assert.Single(c.Lines, l => l.Label.Contains("CSS"));
        Assert.Equal(1639.733m, css.Base);
        Assert.Equal(0.5m, css.Rate);

        var foprolos = Assert.Single(c.Lines, l => l.Label == "FOPROLOS");
        Assert.Equal(2000m, foprolos.Base);
        Assert.Equal(1m, foprolos.Rate);
    }

    [Fact]
    public void Compute_Lines_UncappedProfessionalExpenses_ShowBaseAndRate()
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = 1000m,
            Regime = SocialRegime.Rsna
        };

        var c = PayrollCalculator.Compute(input, Params());

        var fraisPro = Assert.Single(c.Lines, l => l.Label.StartsWith("Frais professionnels"));
        Assert.Equal("Frais professionnels (déduction)", fraisPro.Label);
        Assert.Equal(903.200m, fraisPro.Base); // 1000 − 96,800 (CNSS 9,68 %)
        Assert.Equal(10m, fraisPro.Rate);
    }

    [Fact]
    public void Compute_OvertimeAmount_AddsEarningLine()
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            OvertimeAmount = 50m,
            Regime = SocialRegime.Rsna
        };

        var c = PayrollCalculator.Compute(input, Params());

        Assert.Contains(c.Lines, l => l.Label == "Heures supplémentaires" && l.Amount == 50m);
    }

    [Fact]
    public void Compute_AllowanceLines_ShowsIndividualLabelsWithoutChangingTotals()
    {
        var legacy = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            TaxableCnssableAllowances = 250m,
            Regime = SocialRegime.Rsna
        };

        var detailed = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            TaxableCnssableAllowances = 250m,
            AllowanceLines =
            [
                new AllowanceLineInput("Prime contrat", 100m, true, true),
                new AllowanceLineInput("Prime rendement", 150m, true, true)
            ],
            Regime = SocialRegime.Rsna
        };

        var pars = Params();
        var a = PayrollCalculator.Compute(legacy, pars);
        var b = PayrollCalculator.Compute(detailed, pars);

        Assert.Equal(a.GrossSalary, b.GrossSalary);
        Assert.Equal(a.NetSalary, b.NetSalary);
        Assert.Equal(a.CnssEmployee, b.CnssEmployee);
        Assert.Contains(b.Lines, l => l.Label == "Prime contrat" && l.Amount == 100m);
        Assert.Contains(b.Lines, l => l.Label == "Prime rendement" && l.Amount == 150m);
        Assert.DoesNotContain(b.Lines, l => l.Label == "Primes et indemnités imposables");
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
    public void Compute_WithNoneMode_IsBackwardCompatible()
    {
        // Base 528,320 (ancien SMIG 2025) avec le preset 2026 corrigé (CNSS 9,68 %, SMIG 554,736).
        var input = new PayrollComputationInput { BaseSalary = 528.320m, Regime = SocialRegime.Rsna };
        var c = PayrollCalculator.Compute(input, ParamsWithSmigMode(SmigIrppExemptionMode.None));

        Assert.Equal(1.919m, c.Irpp);
        Assert.Equal(2.147m, c.Css);
        Assert.Equal(473.113m, c.NetSalary);
        Assert.Equal(0m, c.IrppSmigExemption);
    }

    [Fact]
    public void Compute_SmigWorker_WithSmigPortionExemption_YieldsZeroIrpp()
    {
        var input = new PayrollComputationInput { BaseSalary = 528.320m, Regime = SocialRegime.Rsna };
        var c = PayrollCalculator.Compute(input, ParamsWithSmigMode(SmigIrppExemptionMode.SmigPortion));

        Assert.Equal(0m, c.Irpp);
        Assert.Equal(1.919m, c.IrppBeforeSmigExemption);
        Assert.Equal(1.919m, c.IrppSmigExemption);
        Assert.Equal(2.147m, c.Css);
        Assert.Equal(475.032m, c.NetSalary);
        Assert.Contains(c.Lines, l => l.Label == "Exonération IRPP SMIG");
    }

    [Fact]
    public void Compute_AboveSmig_WithSmigPortionExemption_ReducesIrpp()
    {
        var without = PayrollCalculator.Compute(
            new PayrollComputationInput { BaseSalary = 1000m, Regime = SocialRegime.Rsna },
            ParamsWithSmigMode(SmigIrppExemptionMode.None));
        var with = PayrollCalculator.Compute(
            new PayrollComputationInput { BaseSalary = 1000m, Regime = SocialRegime.Rsna },
            ParamsWithSmigMode(SmigIrppExemptionMode.SmigPortion));

        Assert.True(with.IrppSmigExemption > 0m);
        Assert.True(with.Irpp < without.Irpp);
        Assert.Equal(without.Irpp - with.Irpp, with.IrppSmigExemption);
        Assert.True(with.NetSalary > without.NetSalary);
    }

    [Fact]
    public void Compute_SmigWorker_WithFullIfBelow_YieldsZeroIrpp()
    {
        // R-12 : FullIfBelow se juge désormais sur le net imposable mensuel (429,461 ≤ SMIG 554,736),
        // pas sur le seul salaire de base — ici les deux critères concordent (salaire de base 528,320 < SMIG).
        var input = new PayrollComputationInput { BaseSalary = 528.320m, Regime = SocialRegime.Rsna };
        var c = PayrollCalculator.Compute(input, ParamsWithSmigMode(SmigIrppExemptionMode.FullIfBelow));

        Assert.Equal(0m, c.Irpp);
        Assert.Equal(1.919m, c.IrppSmigExemption);
        Assert.Equal(475.032m, c.NetSalary);
    }

    [Fact]
    public void Compute_AboveSmig_WithFullIfBelow_NoExemption()
    {
        var without = PayrollCalculator.Compute(
            new PayrollComputationInput { BaseSalary = 1000m, Regime = SocialRegime.Rsna },
            ParamsWithSmigMode(SmigIrppExemptionMode.None));
        var with = PayrollCalculator.Compute(
            new PayrollComputationInput { BaseSalary = 1000m, Regime = SocialRegime.Rsna },
            ParamsWithSmigMode(SmigIrppExemptionMode.FullIfBelow));

        Assert.Equal(without.Irpp, with.Irpp);
        Assert.Equal(0m, with.IrppSmigExemption);
        Assert.Equal(without.NetSalary, with.NetSalary);
    }

    private static PayrollYearParameters ParamsWithCssEmployerRate(decimal cssEmployerRate)
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
            cssEmployerRate: cssEmployerRate);
        Assert.True(result.IsSuccess);
        return p;
    }

    [Fact]
    public void Compute_WithCssEmployerRate_AddsPatronalCharge()
    {
        var baseline = PayrollCalculator.Compute(new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m
        }, Params());

        var withCssEmployer = PayrollCalculator.Compute(new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m
        }, ParamsWithCssEmployerRate(0.5m));

        Assert.Equal(10.000m, withCssEmployer.CssEmployer);
        Assert.Equal(baseline.NetSalary, withCssEmployer.NetSalary);
        Assert.Equal(baseline.TotalEmployerCharges + 10.000m, withCssEmployer.TotalEmployerCharges);
        Assert.Contains(withCssEmployer.Lines, l =>
            l.Kind == PayslipLineKind.EmployerContribution && l.Label == "CSS patronale" && l.Amount == 10.000m);
    }

    [Fact]
    public void Compute_WithZeroCssEmployerRate_OmitsPatronalLine()
    {
        var c = PayrollCalculator.Compute(new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsna
        }, Params());

        Assert.Equal(0m, c.CssEmployer);
        Assert.DoesNotContain(c.Lines, l => l.Label == "CSS patronale");
    }
}
