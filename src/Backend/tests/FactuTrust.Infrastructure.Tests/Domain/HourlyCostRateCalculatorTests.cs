using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.FirmGovernance;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class HourlyCostRateCalculatorTests
{
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private const decimal FirmDefaultRate = 50m;

    private static FirmTimeSheetYearSettings Settings(
        WeeklyWorkRegime regime = WeeklyWorkRegime.FortyEightHours)
        => FirmTimeSheetYearSettings.Create(FirmId, 2026, true, regime).Value;

    private static FirmCollaboratorYearCost Cost(
        decimal gross = 0m,
        decimal contributions = 0m,
        decimal extras = 0m)
    {
        var cost = FirmCollaboratorYearCost.Create(FirmId, UserId, 2026).Value;
        cost.SetManualCost(gross, contributions, extras);
        return cost;
    }

    // ============================================
    // NIVEAU 1 — TAUX DÉRIVÉ
    // ============================================

    [Fact]
    public void Derived_rate_is_employer_cost_over_productive_hours()
    {
        // 36 864 TND ÷ 1 843,2 h productives = 20,000 TND/h.
        var resolution = HourlyCostRateCalculator.Resolve(
            Cost(gross: 30_000m, contributions: 6_864m), Settings(), null, FirmDefaultRate);

        Assert.Equal(FirmHourlyRateSource.Derived, resolution.Source);
        Assert.Equal(20m, resolution.Rate);
    }

    [Fact]
    public void Derived_rate_explains_its_own_computation()
    {
        var resolution = HourlyCostRateCalculator.Resolve(
            Cost(gross: 30_000m, contributions: 6_864m), Settings(), null, FirmDefaultRate);

        // La colonne « Taux horaire » doit être vérifiable sans ouvrir le code.
        // Le séparateur de milliers fr-FR est une espace insécable étroite : on n'assère que sur
        // des fragments qui ne le traversent pas.
        Assert.Contains("843,2", resolution.Basis);
        Assert.Contains("÷", resolution.Basis);
        Assert.Contains("productives", resolution.Basis);
    }

    [Fact]
    public void Employer_contributions_and_extras_all_weigh_on_the_rate()
    {
        var withoutExtras = HourlyCostRateCalculator.Resolve(
            Cost(gross: 30_000m, contributions: 6_000m), Settings(), null, FirmDefaultRate);
        var withExtras = HourlyCostRateCalculator.Resolve(
            Cost(gross: 30_000m, contributions: 6_000m, extras: 3_000m), Settings(), null, FirmDefaultRate);

        Assert.True(withExtras.Rate > withoutExtras.Rate);
    }

    [Fact]
    public void Forty_hour_regime_raises_the_rate_for_an_identical_cost()
    {
        // Moins d'heures productives pour un même coût : le taux horaire monte mécaniquement.
        var fortyEight = HourlyCostRateCalculator.Resolve(
            Cost(gross: 30_000m), Settings(WeeklyWorkRegime.FortyEightHours), null, FirmDefaultRate);
        var forty = HourlyCostRateCalculator.Resolve(
            Cost(gross: 30_000m), Settings(WeeklyWorkRegime.FortyHours), null, FirmDefaultRate);

        Assert.True(forty.Rate > fortyEight.Rate);
    }

    // ============================================
    // NIVEAU 2 — TAUX IMPOSÉ
    // ============================================

    [Fact]
    public void Override_wins_over_the_derived_rate()
    {
        // Un taux imposé est un acte explicite et justifié du cabinet : le laisser perdre face au
        // calcul le rendrait inopérant dès qu'une paie existe, soit le défaut que ce module corrige.
        var cost = Cost(gross: 30_000m, contributions: 6_864m);
        cost.SetHourlyRateOverride(75m, "Associé — barème interne");

        var resolution = HourlyCostRateCalculator.Resolve(cost, Settings(), null, FirmDefaultRate);

        Assert.Equal(FirmHourlyRateSource.Override, resolution.Source);
        Assert.Equal(75m, resolution.Rate);
        Assert.Contains("Associé — barème interne", resolution.Basis);
    }

    [Fact]
    public void Override_without_justification_is_refused_by_the_entity()
    {
        var cost = Cost(gross: 30_000m);

        Assert.True(cost.SetHourlyRateOverride(75m, "   ").IsFailure);
        Assert.True(cost.SetHourlyRateOverride(0m, "motif").IsFailure);
        Assert.Null(cost.HourlyRateOverride);
    }

    [Fact]
    public void Clearing_the_override_restores_the_derived_rate()
    {
        var cost = Cost(gross: 30_000m, contributions: 6_864m);
        cost.SetHourlyRateOverride(75m, "Provisoire");
        cost.SetHourlyRateOverride(null, null);

        var resolution = HourlyCostRateCalculator.Resolve(cost, Settings(), null, FirmDefaultRate);

        Assert.Equal(FirmHourlyRateSource.Derived, resolution.Source);
        Assert.Equal(20m, resolution.Rate);
    }

    // ============================================
    // NIVEAUX 3 ET 4 — REPLIS
    // ============================================

    [Fact]
    public void Legacy_profile_rate_is_used_when_no_cost_is_known()
    {
        var resolution = HourlyCostRateCalculator.Resolve(null, Settings(), 42m, FirmDefaultRate);

        Assert.Equal(FirmHourlyRateSource.LegacyProfile, resolution.Source);
        Assert.Equal(42m, resolution.Rate);
    }

    [Fact]
    public void Firm_default_is_the_last_resort_and_says_so()
    {
        var resolution = HourlyCostRateCalculator.Resolve(null, Settings(), null, FirmDefaultRate);

        Assert.Equal(FirmHourlyRateSource.FirmDefault, resolution.Source);
        Assert.Equal(FirmDefaultRate, resolution.Rate);
        Assert.Contains("aucun coût employeur", resolution.Basis);
    }

    [Fact]
    public void A_cost_row_with_no_amount_falls_through_to_the_default()
    {
        // Une ligne créée mais jamais renseignée ne doit pas produire un taux de zéro.
        var resolution = HourlyCostRateCalculator.Resolve(Cost(), Settings(), null, FirmDefaultRate);

        Assert.Equal(FirmHourlyRateSource.FirmDefault, resolution.Source);
        Assert.Equal(FirmDefaultRate, resolution.Rate);
    }

    // ============================================
    // CALCUL DES CHARGES PATRONALES
    // ============================================

    [Fact]
    public void Employer_contributions_follow_the_year_rates()
    {
        // 16,57 % CNSS + 2 % TFP + 1 % FOPROLOS = 19,57 % ; 30 000 × 19,57 % = 5 871,000.
        var settings = Settings();
        var contributions = FirmCollaboratorYearCost.ComputeEmployerContributions(
            30_000m, settings.TotalEmployerChargeRate);

        Assert.Equal(5_871m, contributions);
    }

    [Fact]
    public void Employer_contributions_are_zero_without_a_gross_salary()
    {
        Assert.Equal(0m, FirmCollaboratorYearCost.ComputeEmployerContributions(0m, 19.57m));
    }

    [Fact]
    public void Negative_payroll_amounts_are_refused()
    {
        var cost = FirmCollaboratorYearCost.Create(FirmId, UserId, 2026).Value;
        Assert.True(cost.SetManualCost(-1m, 0m, 0m).IsFailure);
    }

    [Fact]
    public void Imported_cost_is_tagged_with_its_origin()
    {
        var cost = FirmCollaboratorYearCost.Create(FirmId, UserId, 2026).Value;
        cost.SetImportedCost(30_000m, 5_871m, 0m);

        Assert.Equal(FirmPayrollCostSource.ImportedFromPayroll, cost.Source);
        Assert.NotNull(cost.ImportedAt);
        Assert.Equal(35_871m, cost.TotalEmployerCost);
    }

    [Fact]
    public void A_manual_correction_clears_the_import_stamp()
    {
        var cost = FirmCollaboratorYearCost.Create(FirmId, UserId, 2026).Value;
        cost.SetImportedCost(30_000m, 5_871m, 0m);
        cost.SetManualCost(31_000m, 6_066m, 0m);

        Assert.Equal(FirmPayrollCostSource.Manual, cost.Source);
        Assert.Null(cost.ImportedAt);
    }
}
