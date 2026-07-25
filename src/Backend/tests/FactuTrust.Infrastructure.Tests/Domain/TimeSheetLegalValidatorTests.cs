using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.FirmGovernance;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class TimeSheetLegalValidatorTests
{
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>Lundi 20 juillet 2026, utilisé comme « aujourd'hui » dans tous les scénarios.</summary>
    private static readonly DateTime Today = new(2026, 7, 20);

    private static FirmTimeSheetYearSettings Settings(
        WeeklyWorkRegime regime = WeeklyWorkRegime.FortyEightHours,
        bool enforce = true)
        => FirmTimeSheetYearSettings.Create(FirmId, 2026, enforce, regime).Value;

    // ============================================
    // PLAFOND JOURNALIER
    // ============================================

    [Fact]
    public void Daily_limit_counts_the_other_lines_of_the_same_day()
    {
        // 8 h déjà saisies + 3 h = 11 h > plafond de 10 h : c'est le cumul qui compte, pas la ligne.
        var anomalies = TimeSheetLegalValidator.Validate(
            Today, hours: 3m, otherHoursSameDay: 8m, otherHoursSameWeek: 8m, Today, Settings());

        Assert.Contains(anomalies, a => a.Kind == TimeSheetAnomalyKind.DailyLimitExceeded);
    }

    [Fact]
    public void Daily_limit_exactly_reached_is_accepted()
    {
        var anomalies = TimeSheetLegalValidator.Validate(
            Today, hours: 2m, otherHoursSameDay: 8m, otherHoursSameWeek: 8m, Today, Settings());

        Assert.DoesNotContain(anomalies, a => a.Kind == TimeSheetAnomalyKind.DailyLimitExceeded);
    }

    [Fact]
    public void A_single_line_under_the_cap_passes()
    {
        var anomalies = TimeSheetLegalValidator.Validate(
            Today, hours: 7.5m, otherHoursSameDay: 0m, otherHoursSameWeek: 0m, Today, Settings());

        Assert.Empty(anomalies);
    }

    // ============================================
    // PLAFOND HEBDOMADAIRE
    // ============================================

    [Fact]
    public void Weekly_limit_uses_the_regime_duration()
    {
        // Régime 48 h : 46 h déjà saisies + 4 h = 50 h.
        var anomalies = TimeSheetLegalValidator.Validate(
            Today, hours: 4m, otherHoursSameDay: 0m, otherHoursSameWeek: 46m, Today, Settings());

        Assert.Contains(anomalies, a => a.Kind == TimeSheetAnomalyKind.WeeklyLimitExceeded);
    }

    [Fact]
    public void Forty_hour_regime_caps_earlier_than_forty_eight()
    {
        var fortyHours = Settings(WeeklyWorkRegime.FortyHours);
        var fortyEightHours = Settings(WeeklyWorkRegime.FortyEightHours);

        // 42 h sur la semaine : refusé en régime 40 h, accepté en régime 48 h.
        var underForty = TimeSheetLegalValidator.Validate(
            Today, hours: 2m, otherHoursSameDay: 0m, otherHoursSameWeek: 40m, Today, fortyHours);
        var underFortyEight = TimeSheetLegalValidator.Validate(
            Today, hours: 2m, otherHoursSameDay: 0m, otherHoursSameWeek: 40m, Today, fortyEightHours);

        Assert.Contains(underForty, a => a.Kind == TimeSheetAnomalyKind.WeeklyLimitExceeded);
        Assert.DoesNotContain(underFortyEight, a => a.Kind == TimeSheetAnomalyKind.WeeklyLimitExceeded);
    }

    // ============================================
    // SEMAINE ISO
    // ============================================

    [Fact]
    public void Iso_week_starts_on_monday_and_ends_on_sunday()
    {
        // Le 22/07/2026 est un mercredi.
        var (start, end) = TimeSheetLegalValidator.IsoWeekBounds(new DateTime(2026, 7, 22));

        Assert.Equal(new DateTime(2026, 7, 20), start);
        Assert.Equal(DayOfWeek.Monday, start.DayOfWeek);
        Assert.Equal(new DateTime(2026, 7, 26), end);
        Assert.Equal(DayOfWeek.Sunday, end.DayOfWeek);
    }

    [Fact]
    public void Sunday_belongs_to_the_week_that_started_the_previous_monday()
    {
        // Piège classique : DayOfWeek place dimanche à 0, ce qui le rattacherait à la semaine suivante.
        var (start, end) = TimeSheetLegalValidator.IsoWeekBounds(new DateTime(2026, 7, 26));

        Assert.Equal(new DateTime(2026, 7, 20), start);
        Assert.Equal(new DateTime(2026, 7, 26), end);
    }

    [Fact]
    public void Iso_week_spans_two_months()
    {
        // La semaine du 29/06/2026 déborde sur juillet : le cumul hebdomadaire ne peut pas se
        // calculer sur le mois affiché.
        var (start, end) = TimeSheetLegalValidator.IsoWeekBounds(new DateTime(2026, 7, 1));

        Assert.Equal(new DateTime(2026, 6, 29), start);
        Assert.Equal(new DateTime(2026, 7, 5), end);
        Assert.NotEqual(start.Month, end.Month);
    }

    [Fact]
    public void Iso_week_spans_two_years()
    {
        // 31/12/2025 est un mercredi : sa semaine se termine en 2026.
        var (start, end) = TimeSheetLegalValidator.IsoWeekBounds(new DateTime(2025, 12, 31));

        Assert.Equal(new DateTime(2025, 12, 29), start);
        Assert.Equal(new DateTime(2026, 1, 4), end);
        Assert.NotEqual(start.Year, end.Year);
    }

    // ============================================
    // DATATION
    // ============================================

    [Fact]
    public void Future_date_is_flagged()
    {
        var anomalies = TimeSheetLegalValidator.ValidateWorkDate(
            Today.AddDays(1), Today, Settings());

        Assert.Contains(anomalies, a => a.Kind == TimeSheetAnomalyKind.FutureDate);
    }

    [Fact]
    public void Today_is_accepted()
    {
        Assert.Empty(TimeSheetLegalValidator.ValidateWorkDate(Today, Today, Settings()));
    }

    [Fact]
    public void Date_beyond_the_backdating_window_is_flagged()
    {
        // Fenêtre par défaut : 45 jours.
        var anomalies = TimeSheetLegalValidator.ValidateWorkDate(
            Today.AddDays(-46), Today, Settings());

        Assert.Contains(anomalies, a => a.Kind == TimeSheetAnomalyKind.TooOld);
    }

    [Fact]
    public void Date_at_the_edge_of_the_backdating_window_is_accepted()
    {
        Assert.Empty(TimeSheetLegalValidator.ValidateWorkDate(
            Today.AddDays(-45), Today, Settings()));
    }

    // ============================================
    // INTERRUPTEUR DE NON-RÉGRESSION
    // ============================================

    [Fact]
    public void Anomalies_are_reported_regardless_of_the_enforcement_switch()
    {
        // Le validateur constate toujours ; c'est la couche service qui décide de bloquer ou non
        // selon EnforceHardLimits. Les deux modes doivent donc produire le même diagnostic.
        var enforced = TimeSheetLegalValidator.Validate(
            Today, 5m, 8m, 8m, Today, Settings(enforce: true));
        var lenient = TimeSheetLegalValidator.Validate(
            Today, 5m, 8m, 8m, Today, Settings(enforce: false));

        Assert.Equal(
            enforced.Select(a => a.Kind).ToArray(),
            lenient.Select(a => a.Kind).ToArray());
    }

    [Fact]
    public void Several_anomalies_are_all_reported()
    {
        // Date future et dépassement journalier simultanés : l'utilisateur doit voir les deux.
        var anomalies = TimeSheetLegalValidator.Validate(
            Today.AddDays(3), hours: 5m, otherHoursSameDay: 8m, otherHoursSameWeek: 8m, Today, Settings());

        Assert.Contains(anomalies, a => a.Kind == TimeSheetAnomalyKind.FutureDate);
        Assert.Contains(anomalies, a => a.Kind == TimeSheetAnomalyKind.DailyLimitExceeded);
    }

    // ============================================
    // HEURES PRODUCTIVES (dénominateur du taux horaire)
    // ============================================

    [Fact]
    public void Annual_productive_hours_deduct_leave_holidays_and_productivity_rate()
    {
        var settings = Settings();

        // Régime 48 h : 208 × 12 = 2 496 h ; (12 congés + 12 fériés) × 8 h = 192 h ;
        // (2 496 − 192) × 80 % = 1 843,2 h.
        Assert.Equal(2496m, settings.AnnualBaseHours);
        Assert.Equal(8m, settings.DailyHours);
        Assert.Equal(1843.2m, settings.AnnualProductiveHours);
    }

    [Fact]
    public void Forty_hour_regime_yields_fewer_productive_hours()
    {
        var fortyHours = Settings(WeeklyWorkRegime.FortyHours);

        Assert.True(fortyHours.AnnualProductiveHours < Settings().AnnualProductiveHours);
        Assert.True(fortyHours.AnnualProductiveHours > 0m);
    }

    [Fact]
    public void Total_employer_charge_rate_sums_the_tunisian_contributions()
    {
        // CNSS patronale 16,57 % + TFP 2 % + FOPROLOS 1 % + accident du travail 0 % par défaut.
        Assert.Equal(19.57m, Settings().TotalEmployerChargeRate);
    }
}
