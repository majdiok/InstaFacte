using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

/// <summary>
/// Tests de conformité des presets légaux aux chiffres OFFICIELS tunisiens (WS-3 / Phase 3).
///
/// Règle cardinale du plan §8 : les chiffres officiels sont codés EN DUR dans ces tests
/// (JORT / décrets / notes communes), et l'on compare les presets du moteur à ces valeurs
/// de référence — jamais les presets aux défauts de l'application (cette tautologie est
/// précisément ce qui a laissé passer R-09/R-10/R-11/R-21 dans l'ancienne suite de régression).
///
/// Sources vérifiées le 2026-08-31 — citations détaillées dans
/// <c>docs/payroll/legal-presets-history.md</c>.
/// </summary>
public sealed class PayrollLegalPresetsOfficialFiguresTests
{
    // ── CNSS RSNA — taux effectifs (régime des salariés non agricoles) ──
    // Hausse de 0,50 pt salarié/employeur entrée en vigueur au 1er janvier 2025 (LF 2025).
    // Avant cette date : 9,18 % / 16,57 % (taux de base pré-2019 maintenus 2020-2024).
    // Sources : smartpaie.tn « taux CNSS 2026 », paie-tunisie.com, jurisitetunisie.com,
    // efacturetn.com (vérifié 2026-08).

    // ── CNSS RSA — régime des salariés agricoles ──
    // 4,57 % salarié / 7,72 % employeur, en vigueur depuis le 01/07/2011, constant sur
    // toute la période (distinct du RSNA — CAL-002). Assiette forfaitaire SMAG simplifiée
    // (non modélisée telle quelle par le moteur). Sources : paie-tunisie.com, humanforcetunisie.com.
    private const decimal CnssRsaEmployee = 4.57m;
    private const decimal CnssRsaEmployer = 7.72m;

    // ── SMIG mensuel (régime 48 h) — série décret par décret ──
    // 2020-2021 : 429,312 (décret n° 2020-1069 du 30/12/2020, eff. 01/10/2020)
    // 2022-2023 : 459,264 (décret n° 2022-769 du 19/10/2022, eff. 01/10/2022)
    // 2024      : 491,504 (décrets n° 2024-419/420 du 09/07/2024, eff. 01/05/2024)
    // 2025      : 528,320 (mêmes décrets, eff. 01/01/2025)
    // 2026      : 554,736 (décret n° 2026-67 du 30/04/2026, JORT n° 44, eff. 01/01/2026)
    // Sources : jurisitetunisie.com/SMIG.htm, paie-tunisie.com, businessnews.com.tn (30/04/2026),
    // tunisie-tribune.com (10/07/2024), lapresse.tn (30/04/2026).
    private static readonly IReadOnlyDictionary<int, decimal> OfficialMonthlySmig = new Dictionary<int, decimal>
    {
        [2020] = 429.312m,
        [2021] = 429.312m,
        [2022] = 459.264m,
        [2023] = 459.264m,
        [2024] = 491.504m,
        [2025] = 528.320m,
        [2026] = 554.736m
    };

    // ── CSS (personnes physiques / salariés) — taux par exercice ──
    // 1 % pour 2020-2022 (taux de droit commun avant la LF 2023) ;
    // 0,5 % pour 2023-2026 (LF 2023 ramène à 0,5 %, maintenu par la note commune 01-2026).
    // Sources : africanmanager.com, standfors.com (LF2023), jibaya.tn (note commune 01-2026),
    // lapresse.tn (14/01/2026).
    private static readonly IReadOnlyDictionary<int, decimal> OfficialCssRate = new Dictionary<int, decimal>
    {
        [2020] = 1m,
        [2021] = 1m,
        [2022] = 1m,
        [2023] = 0.5m,
        [2024] = 0.5m,
        [2025] = 0.5m,
        [2026] = 0.5m
    };

    // ── IRPP 2024 — barème légal 5 tranches (en vigueur jusqu'au 31/12/2024) ──
    // 0 % ≤ 5000 ; 26 % 5000-20000 ; 28 % 20000-30000 ; 32 % 30000-50000 ; 35 % > 50000.
    // Le barème 8 tranches (LF 2025, note commune 03-2025) ne s'applique qu'à compter du
    // 1er janvier 2025. Sources : finances.gov.tn, jurisitetunisie.com, paie-tunisie.com.
    private static readonly (decimal LowerBound, decimal Rate)[] OfficialIrpp2024Brackets =
    [
        (0m, 0m),
        (5000m, 26m),
        (20000m, 28m),
        (30000m, 32m),
        (50000m, 35m)
    ];

    // ── IRPP 2025/2026 — barème 8 tranches (LF 2025, note commune 03-2025) ──
    // Vérifié correct par l'audit (findings §2 point 1) — à préserver.
    private static readonly (decimal LowerBound, decimal Rate)[] OfficialIrpp2025Brackets =
    [
        (0m, 0m),
        (5000m, 15m),
        (10000m, 25m),
        (20000m, 30m),
        (30000m, 33m),
        (40000m, 36m),
        (50000m, 38m),
        (70000m, 40m)
    ];

    [Theory]
    [InlineData(2020, 9.18, 16.57)]
    [InlineData(2021, 9.18, 16.57)]
    [InlineData(2022, 9.18, 16.57)]
    [InlineData(2023, 9.18, 16.57)]
    [InlineData(2024, 9.18, 16.57)]
    [InlineData(2025, 9.68, 17.07)]
    [InlineData(2026, 9.68, 17.07)]
    public void Preset_CnssRsnaRates_MatchOfficialPerYear(int year, decimal expectedEmployee, decimal expectedEmployer)
    {
        // 2020-2024 : 9,18 %/16,57 % (taux pré-2019) ; 2025-2026 : 9,68 %/17,07 % (LF 2025).
        var preset = PayrollLegalPresets.Presets[year];

        Assert.Equal(expectedEmployee, preset.CnssEmployeeRate);
        Assert.Equal(expectedEmployer, preset.CnssEmployerRate);
    }

    [Theory]
    [InlineData(2020)]
    [InlineData(2023)]
    [InlineData(2026)]
    public void Preset_CnssRsaRates_DistinctFromRsna_MatchOfficial(int year)
    {
        // R-20 / CAL-002 : le régime agricole a ses propres taux, distincts du RSNA.
        var preset = PayrollLegalPresets.Presets[year];

        Assert.Equal(CnssRsaEmployee, preset.CnssEmployeeRateRsa);
        Assert.Equal(CnssRsaEmployer, preset.CnssEmployerRateRsa);
        Assert.NotEqual(preset.CnssEmployeeRate, preset.CnssEmployeeRateRsa);
        Assert.NotEqual(preset.CnssEmployerRate, preset.CnssEmployerRateRsa);
    }

    [Theory]
    [InlineData(2020)]
    [InlineData(2021)]
    [InlineData(2022)]
    [InlineData(2023)]
    [InlineData(2024)]
    [InlineData(2025)]
    [InlineData(2026)]
    public void Preset_MonthlySmig_MatchesOfficialDecreeSeries(int year)
    {
        var preset = PayrollLegalPresets.Presets[year];

        Assert.Equal(OfficialMonthlySmig[year], preset.MonthlySmig);
    }

    /// <summary>
    /// R-11 : la valeur SMIG 2026 doit être 554,736 (décret n° 2026-67) et non l'ancienne
    /// valeur 528,320 (2024/2025) qui était codée par erreur pour 2026.
    /// </summary>
    [Fact]
    public void Preset_Smig2026_Is554_736_NotStale528_320()
    {
        var preset = PayrollLegalPresets.Presets[2026];

        Assert.Equal(554.736m, preset.MonthlySmig);
        Assert.NotEqual(528.320m, preset.MonthlySmig);
    }

    [Theory]
    [InlineData(2020)]
    [InlineData(2021)]
    [InlineData(2022)]
    [InlineData(2023)]
    [InlineData(2024)]
    [InlineData(2025)]
    [InlineData(2026)]
    public void Preset_CssRate_MatchesOfficialPerYear(int year)
    {
        // R-21 / CAL-005 : la CSS est annuelle-variable (1 % 2020-2022, 0,5 % 2023-2026),
        // non un 0,5 % uniforme comme c'était codé.
        var preset = PayrollLegalPresets.Presets[year];

        Assert.Equal(OfficialCssRate[year], preset.CssRate);
    }

    [Fact]
    public void Preset_CssRate_2020To2022_IsOnePercent_NotHalfPercent()
    {
        Assert.Equal(1m, PayrollLegalPresets.Presets[2020].CssRate);
        Assert.Equal(1m, PayrollLegalPresets.Presets[2021].CssRate);
        Assert.Equal(1m, PayrollLegalPresets.Presets[2022].CssRate);
    }

    // ── IRPP 2024 : barème légal 5 tranches (R-10 / CAL-003) ──

    [Fact]
    public void Preset_Irpp2024_UsesLegalLegacy5Brackets_NotPhantomHybrid()
    {
        // L'ancien Irpp2024Brackets (0/15/25/30/33/36/38) ne correspondait à aucun texte
        // et a été supprimé. 2024 doit pointer sur le barème 5 tranches 0/26/28/32/35.
        var preset = PayrollLegalPresets.Presets[2024];
        var brackets = preset.IrppBrackets.ToList();

        Assert.Equal(OfficialIrpp2024Brackets.Length, brackets.Count);
        for (var i = 0; i < OfficialIrpp2024Brackets.Length; i++)
        {
            Assert.Equal(OfficialIrpp2024Brackets[i].LowerBound, brackets[i].LowerBound);
            Assert.Equal(OfficialIrpp2024Brackets[i].Rate, brackets[i].Rate);
        }

        // Garde-fou : le premier taux non nul doit être 26 % (legacy), pas 15 % (LF 2025).
        Assert.Equal(26m, brackets.First(b => b.Rate > 0).Rate);
    }

    [Fact]
    public void Preset_Irpp2025And2026_UseLegal8Brackets_PreservedCorrect()
    {
        // L'audit a vérifié le barème 2025/2026 (findings §2 point 1) — à préserver.
        foreach (var year in new[] { 2025, 2026 })
        {
            var preset = PayrollLegalPresets.Presets[year];
            var brackets = preset.IrppBrackets.ToList();

            Assert.Equal(OfficialIrpp2025Brackets.Length, brackets.Count);
            for (var i = 0; i < OfficialIrpp2025Brackets.Length; i++)
            {
                Assert.Equal(OfficialIrpp2025Brackets[i].LowerBound, brackets[i].LowerBound);
                Assert.Equal(OfficialIrpp2025Brackets[i].Rate, brackets[i].Rate);
            }
        }
    }

    /// <summary>
    /// Plan §8 : exemple IRPP pointé « annual net 19 796 → IRPP 3 847 » (CAL-003).
    /// Avec le barème légal 2024 (5 tranches), un revenu net annuel imposable de 19 796 TND
    /// produit 3 846,960 TND d'IRPP (≈ 3 847 en prose). L'ancien barème fantôme produisait
    /// 2 219,400 TND — sous-déclaration massive.
    /// </summary>
    [Fact]
    public void Irpp2024_PinnedExample_AnnualNet19796_Yields3847Not2199()
    {
        var parameters = PayrollLegalPresets.Presets[2024].Materialize(2024);
        Assert.True(parameters.IsSuccess);

        // 19 796 TND de revenu net annuel imposable.
        var irpp = PayrollCalculator.ComputeProgressiveTax(19796m, parameters.Value);

        // Barème légal : 0 % ≤ 5000, 26 % 5000-20000 → (19796 - 5000) × 26 % = 3846,960.
        Assert.Equal(3846.960m, irpp);
        Assert.True(irpp > 3800m, "L'IRPP légal 2024 sur 19 796 TND doit être ≈ 3 847 TND.");
        Assert.False(irpp < 2500m, "Le barème fantôme (2 219 TND) ne doit plus être produit.");
    }

    // ── Tranches de saisie sur salaire indexées sur le SMIG de l'exercice (R-11) ──

    [Theory]
    [InlineData(2024, 491.504)]
    [InlineData(2025, 528.320)]
    [InlineData(2026, 554.736)]
    public void Preset_GarnishmentBrackets_LowerBoundsIndexedOnThatYearSmig(int year, decimal expectedSmig)
    {
        // R-11 : les bornes 1×SMIG / 2×SMIG sont dérivées du SMIG propre à l'exercice,
        // et non d'une liste statique partagée par toutes les années.
        var preset = PayrollLegalPresets.Presets[year];
        var ordered = preset.GarnishmentBrackets.OrderBy(b => b.LowerBoundMonthlyNet).ToList();

        Assert.Equal(3, ordered.Count);
        Assert.Equal(0m, ordered[0].LowerBoundMonthlyNet);
        Assert.Equal(0m, ordered[0].SeizableFraction);
        Assert.Equal(expectedSmig, ordered[1].LowerBoundMonthlyNet);          // 1 × SMIG
        Assert.Equal(2m * expectedSmig, ordered[2].LowerBoundMonthlyNet);     // 2 × SMIG
        Assert.Equal(0.333m, ordered[1].SeizableFraction);
        Assert.Equal(0.666m, ordered[2].SeizableFraction);
    }

    [Fact]
    public void Preset_GarnishmentBrackets_NotSharedStaticList_Anymore()
    {
        // Garde-fou : avant la correction, une liste statique (528,320 / 1056,640) était
        // partagée par tous les exercices. Désormais chaque exercice a ses propres bornes.
        var p2024 = PayrollLegalPresets.Presets[2024].GarnishmentBrackets
            .OrderBy(b => b.LowerBoundMonthlyNet).ToList();
        var p2026 = PayrollLegalPresets.Presets[2026].GarnishmentBrackets
            .OrderBy(b => b.LowerBoundMonthlyNet).ToList();

        Assert.NotEqual(p2024[1].LowerBoundMonthlyNet, p2026[1].LowerBoundMonthlyNet);
        // 2024 = 491,504 ; 2026 = 554,736 — plus jamais 528,320 pour les deux.
        Assert.Equal(491.504m, p2024[1].LowerBoundMonthlyNet);
        Assert.Equal(554.736m, p2026[1].LowerBoundMonthlyNet);
    }

    /// <summary>
    /// R-26 : pour un exercice non explicitement préseté (futur), Resolve() bascule sur le
    /// barème légal art. 354 CPCC (bornes mensuelles = bornes annuelles ÷ 12) plutôt que sur
    /// la convention SMIG simplifiée héritée. Les tranches déjà paramétrées par un tenant
    /// (ReplaceGarnishmentBrackets) restent inchangées — testé séparément au niveau repository.
    /// </summary>
    [Fact]
    public void Resolve_UnknownYear_UsesArt354CpccLegalScale()
    {
        // 2027 n'est pas préseté : on hérite des taux du dernier exercice connu (2026) mais
        // les tranches de saisie basculent sur le barème légal art. 354 CPCC.
        var preset = PayrollLegalPresets.Resolve(2027);
        var ordered = preset.GarnishmentBrackets.OrderBy(b => b.LowerBoundMonthlyNet).ToList();

        // art. 354 CPCC : 1/20 ≤ 300 DT/an (25 DT/mois), 1/10 ≤ 600 (50), 1/5 ≤ 900 (75),
        // 1/4 ≤ 1200 (100), 1/3 ≤ 1500 (125), 2/3 ≤ 3000 (250), puis illimité au-delà.
        Assert.Equal(7, ordered.Count);
        Assert.Equal(0m, ordered[0].LowerBoundMonthlyNet);
        Assert.Equal(1m / 20m, ordered[0].SeizableFraction);
        Assert.Equal(25m, ordered[1].LowerBoundMonthlyNet);
        Assert.Equal(1m / 10m, ordered[1].SeizableFraction);
        Assert.Equal(50m, ordered[2].LowerBoundMonthlyNet);
        Assert.Equal(1m / 5m, ordered[2].SeizableFraction);
        Assert.Equal(75m, ordered[3].LowerBoundMonthlyNet);
        Assert.Equal(1m / 4m, ordered[3].SeizableFraction);
        Assert.Equal(100m, ordered[4].LowerBoundMonthlyNet);
        Assert.Equal(1m / 3m, ordered[4].SeizableFraction);
        Assert.Equal(125m, ordered[5].LowerBoundMonthlyNet);
        Assert.Equal(2m / 3m, ordered[5].SeizableFraction);
        Assert.Equal(250m, ordered[6].LowerBoundMonthlyNet);
        Assert.Equal(1m, ordered[6].SeizableFraction); // tranche supérieure illimitée
    }

    // ── Déduction SMIG annuelle (R-12 / CAL-006) — règle vérifiable 500 TND/an ──

    [Fact]
    public void SmigAnnualDeduction_Amount_Is500Tnd()
    {
        Assert.Equal(500m, SmigIrppExemptionCalculator.SmigAnnualDeductionAmount);
    }

    [Fact]
    public void SmigAnnualDeduction_MonthlyEffect_Is500DividedBy12()
    {
        // Effet mensuel sur la base imposable : 500 / 12 = 41,667 TND (3 décimales).
        Assert.Equal(41.667m, SmigIrppExemptionCalculator.ComputeMonthlySmigAnnualDeductionEffect(400m, 554.736m));
    }

    [Fact]
    public void SmigAnnualDeduction_CumulEffect_OverTwelveMonths_Is500()
    {
        // L'effet mensuel (41,667) est une approximation de retenue à la source : 500/12 arrondi
        // à 3 décimales (AwayFromZero) donne 41,667, dont la somme sur 12 mois atteint 500,004.
        // La régularisation annuelle applique donc le forfait exact de 500 TND
        // (SmigAnnualDeductionAmount), et non la somme des arrondis mensuels — d'où un écart de
        // retenue de 0,004 TND corrigé lors de l'annualisation.
        var monthly = SmigIrppExemptionCalculator.ComputeMonthlySmigAnnualDeductionEffect(450m, 554.736m);
        Assert.Equal(41.667m, monthly);

        // Somme des arrondis mensuels : 41,667 × 12 = 500,004 (dérive d'arrondi connue).
        var monthlyCumul = Math.Round(monthly * 12m, 3, MidpointRounding.AwayFromZero);
        Assert.Equal(500.004m, monthlyCumul);

        // Forfait annuel exact appliqué à la régularisation (règle légale vérifiable).
        Assert.Equal(500m, SmigIrppExemptionCalculator.SmigAnnualDeductionAmount);
    }

    [Theory]
    [InlineData(400.000, 554.736, true)]   // rémunération < SMIG → éligible
    [InlineData(554.736, 554.736, true)]   // rémunération = SMIG → éligible (limite inclusive)
    [InlineData(600.000, 554.736, false)]  // rémunération > SMIG → non éligible
    public void SmigAnnualDeduction_Eligibility_OnTotalMonthlyTaxableRemuneration(
        decimal monthlyTaxable, decimal smig, bool expectedEligible)
    {
        // R-12 : l'éligibilité se juge sur la rémunération mensuelle totale imposable,
        // non sur le seul salaire de base contractuel.
        Assert.Equal(expectedEligible, SmigIrppExemptionCalculator.IsEligibleForSmigAnnualDeduction(monthlyTaxable, smig));

        var effect = SmigIrppExemptionCalculator.ComputeMonthlySmigAnnualDeductionEffect(monthlyTaxable, smig);
        Assert.Equal(expectedEligible ? 41.667m : 0m, effect);
    }

    [Fact]
    public void SmigAnnualDeduction_NoEffect_WhenSmigNotPositive()
    {
        // Garde-fou : sans SMIG paramétré, aucune déduction (évite une éligibilité systématique).
        Assert.False(SmigIrppExemptionCalculator.IsEligibleForSmigAnnualDeduction(100m, 0m));
        Assert.Equal(0m, SmigIrppExemptionCalculator.ComputeMonthlySmigAnnualDeductionEffect(100m, 0m));
    }

    // ── FullIfBelow : éligibilité sur le net imposable mensuel total (R-12 point 3) ──

    [Fact]
    public void FullIfBelow_ExemptionApplies_WhenMonthlyNetTaxableBelowSmig()
    {
        // Un salarié au salaire de base 528,320 (sous le SMIG 2026 de 554,736) : son net
        // imposable mensuel est aussi sous le SMIG → exonération totale de l'IRPP.
        var parameters = BuildParametersWithSmigMode(SmigIrppExemptionMode.FullIfBelow, 554.736m);

        var result = SmigIrppExemptionCalculator.ApplyMonthly(
            irppBeforeExemption: 50m,
            monthlyNetTaxable: 429.461m,
            baseSalary: 528.320m,
            parameters);

        Assert.Equal(0m, result.IrppFinal);
        Assert.Equal(50m, result.ExemptionAmount);
    }

    [Fact]
    public void FullIfBelow_NoExemption_WhenMonthlyNetTaxableAboveSmig_EvenIfBaseSalaryBelow()
    {
        // R-12 / CAL-006 : autrefois jugé sur le seul salaire de base, un salarié au SMIG
        // avec de fortes primes était exonéré à tort. Désormais le net imposable mensuel
        // (qui intègre les primes) dépasse le SMIG → pas d'exonération.
        var parameters = BuildParametersWithSmigMode(SmigIrppExemptionMode.FullIfBelow, 554.736m);

        var result = SmigIrppExemptionCalculator.ApplyMonthly(
            irppBeforeExemption: 200m,
            monthlyNetTaxable: 800m,   // primes font dépasser le SMIG
            baseSalary: 528.320m,      // salaire de base seul sous le SMIG
            parameters);

        Assert.Equal(200m, result.IrppFinal);
        Assert.Equal(0m, result.ExemptionAmount);
    }

    // ── R-23 : prorata suspension dans un mois à 22 jours ouvrables (CAL-010) ──

    [Fact]
    public void Prorata_SuspensionIn22WeekdayMonth_ScalesTo26DayConvention()
    {
        // Mars 2026 = 22 jours ouvrables. 5 jours de suspension non payée doivent être ramenés
        // à la convention 26 jours (5 × 26 / 22 = 5,909 → 5,91) avant soustraction, sinon la
        // retenue est sous-évaluée (5 bruts au lieu de 5,91).
        var fullMonth = PayrollWorkingDaysCounter.CountWeekdaysInMonth(2026, 3);
        Assert.Equal(22, fullMonth); // garde-fou

        var input = new PayrollProrataMonthInput
        {
            Year = 2026,
            Month = 3,
            BaseSalary = 2600m, // 100 TND/jour à 26 jours
            EffectiveStart = new DateTime(2026, 3, 1),
            EffectiveEnd = new DateTime(2026, 3, 31),
            IsEnabled = true,
            Suspensions =
            [
                new PayrollProrataSuspensionPeriod(
                    new DateTime(2026, 3, 2),
                    new DateTime(2026, 3, 6),
                    IsPaid: false,
                    IsApproved: true)
            ]
        };

        var result = PayrollProrataCalculator.Compute(input);

        Assert.Equal(20.09m, result.WorkedDays);     // 26 - 5,91
        Assert.Equal(5.91m, result.NonWorkedDays);
        Assert.Equal(591.000m, result.DeductionAmount); // 2600 / 26 × 5,91
        Assert.Equal(PayrollProrataReason.Suspension, result.Reason);
    }

    // ── R-37 : arrondi des heures supplémentaires au montant final (CAL-018) ──

    [Fact]
    public void Overtime_DivisorH40_IsExact_520Over3()
    {
        // 520/3 = 173,333... (exact) et non 173,33 (tronqué, biais ~2/100 000).
        Assert.Equal(520m / 3m, OvertimeAmountCalculator.DivisorH40);
        Assert.True(OvertimeAmountCalculator.DivisorH40 > 173.33m);
    }

    [Fact]
    public void Overtime_RoundsOnlyFinalAmount_NotIntermediateHourlyRate()
    {
        // base 1000, 10 h à 125 %, régime 40 h : 1000/(520/3) × 10 × 1,25 = 72,115384... → 72,115.
        // Avec l'ancien diviseur tronqué 173,33 + taux horaire pré-arrondi (5,769), on obtenait
        // 72,113 — la correction R-37 supprime cette dérive.
        var amount = OvertimeAmountCalculator.ComputeAmount(1000m, 10m, 125m, regime: WeeklyWorkRegime.FortyHours);

        Assert.Equal(72.115m, amount);

        // Le taux horaire affiché reste arrondi à 3 décimales (5,769) pour le bulletin,
        // mais ce n'est plus lui qui entre dans le calcul du montant.
        Assert.Equal(5.769m, OvertimeAmountCalculator.ComputeHourlyRate(1000m, WeeklyWorkRegime.FortyHours));
    }

    private static PayrollYearParameters BuildParametersWithSmigMode(SmigIrppExemptionMode mode, decimal smig)
    {
        var p = PayrollParameterDefaults.CreateDefaults(2026).Value;
        var result = p.UpdateRates(
            p.CnssEmployeeRate, p.CnssEmployerRate, p.CssRate, p.CssAnnualExemptionThreshold,
            p.ProfessionalExpensesRate, p.ProfessionalExpensesAnnualCap,
            p.HeadOfFamilyAnnualDeduction, p.ChildAnnualDeduction, p.MaxDeductibleChildren,
            p.TfpRateIndustry, p.TfpRateOther, p.FoprolosRate, smig,
            p.CnssEmployeeRateRsa, p.CnssEmployerRateRsa,
            p.EnforceSmigOnContracts, p.EnableExtendedOvertimeRates, p.EnableAllowanceQuadrantMatrix,
            p.StudentChildAnnualDeduction, p.DisabledChildAnnualDeduction,
            p.ParentDeductionRatePercent, p.ParentAnnualDeductionCap, p.IsIndustrialSector,
            p.MealVoucherDailyExemptionCap, p.EnableIrppRegularization,
            smigIrppExemptionMode: mode);
        Assert.True(result.IsSuccess);
        return p;
    }
}
