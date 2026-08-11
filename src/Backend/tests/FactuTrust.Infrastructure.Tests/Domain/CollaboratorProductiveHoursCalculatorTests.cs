using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.FirmGovernance;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Le dénominateur du taux horaire de revient. Le mode paramétrique doit rester strictement
/// identique à l'existant : c'est lui qui protège les exercices déjà analysés.
/// </summary>
public sealed class CollaboratorProductiveHoursCalculatorTests
{
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static FirmTimeSheetYearSettings BuildSettings(
        FirmProductiveHoursMode mode = FirmProductiveHoursMode.Parametric)
    {
        var settings = FirmTimeSheetYearSettings.Create(FirmId, 2026).Value;
        if (mode == FirmProductiveHoursMode.IndividualRealLeaves)
        {
            settings.Update(
                settings.WeeklyRegime,
                settings.MaxDailyHours,
                settings.MaxWeeklyHours,
                settings.AllowFutureEntryDays,
                settings.MaxBackdatingDays,
                settings.EnforceHardLimits,
                settings.PaidLeaveDaysPerYear,
                settings.PublicHolidayDaysPerYear,
                settings.ProductivityRatePercent,
                settings.CnssEmployerRate,
                settings.TfpRate,
                settings.FoprolosRate,
                settings.WorkAccidentRate,
                settings.CssEmployerRate,
                FirmProductiveHoursMode.IndividualRealLeaves);
        }
        return settings;
    }

    [Fact]
    public void Parametric_mode_returns_the_exact_legacy_value()
    {
        // Régime 48 h : (208 × 12 − (12 congés + 12 fériés) × 8) × 80 % = 1 843,2 h.
        var settings = BuildSettings();

        var resolution = CollaboratorProductiveHoursCalculator.Resolve(settings, realAbsenceDays: 30m, presenceRatio: 0.5m);

        Assert.Equal(FirmProductiveHoursMode.Parametric, resolution.Mode);
        Assert.Equal(1_843.2m, resolution.Hours);
        // Ni les congés réels ni la présence ne doivent l'influencer dans ce mode.
        Assert.Equal(settings.AnnualProductiveHours, resolution.Hours);
    }

    [Fact]
    public void Is_the_default_mode_so_existing_exercises_never_move()
    {
        var settings = FirmTimeSheetYearSettings.Create(FirmId, 2026).Value;
        Assert.Equal(FirmProductiveHoursMode.Parametric, settings.ProductiveHoursMode);
    }

    [Fact]
    public void Individual_mode_replaces_the_leave_allowance_with_real_leaves()
    {
        var settings = BuildSettings(FirmProductiveHoursMode.IndividualRealLeaves);

        var resolution = CollaboratorProductiveHoursCalculator.Resolve(settings, realAbsenceDays: 20m, presenceRatio: 1m);

        // (2 496 − (20 + 12) × 8) × 80 % = (2 496 − 256) × 80 % = 1 792 h.
        Assert.Equal(FirmProductiveHoursMode.IndividualRealLeaves, resolution.Mode);
        Assert.Equal(1_792m, resolution.Hours);
        Assert.Contains("congés réels", resolution.Basis, StringComparison.Ordinal);
    }

    [Fact]
    public void Fewer_real_leaves_than_the_allowance_raise_the_productive_hours()
    {
        var settings = BuildSettings(FirmProductiveHoursMode.IndividualRealLeaves);

        var resolution = CollaboratorProductiveHoursCalculator.Resolve(settings, realAbsenceDays: 0m, presenceRatio: 1m);

        // (2 496 − 12 × 8) × 80 % = 1 920 h, au-dessus du forfait de 1 843,2 h.
        Assert.Equal(1_920m, resolution.Hours);
        Assert.True(resolution.Hours > settings.AnnualProductiveHours);
    }

    [Fact]
    public void A_mid_year_arrival_reduces_the_theoretical_base()
    {
        var settings = BuildSettings(FirmProductiveHoursMode.IndividualRealLeaves);

        var resolution = CollaboratorProductiveHoursCalculator.Resolve(
            settings, realAbsenceDays: 0m, presenceRatio: 0.5m);

        // Base et fériés proratisés : (1 248 − 6 × 8) × 80 % = 960 h.
        Assert.Equal(960m, resolution.Hours);
        Assert.Contains("présence", resolution.Basis, StringComparison.Ordinal);
    }

    [Fact]
    public void Absences_covering_the_whole_presence_floor_at_zero()
    {
        var settings = BuildSettings(FirmProductiveHoursMode.IndividualRealLeaves);

        var resolution = CollaboratorProductiveHoursCalculator.Resolve(
            settings, realAbsenceDays: 400m, presenceRatio: 1m);

        Assert.Equal(0m, resolution.Hours);
        Assert.False(string.IsNullOrWhiteSpace(resolution.Basis));
    }

    [Fact]
    public void Negative_absence_days_are_ignored_rather_than_inflating_the_base()
    {
        var settings = BuildSettings(FirmProductiveHoursMode.IndividualRealLeaves);

        var resolution = CollaboratorProductiveHoursCalculator.Resolve(
            settings, realAbsenceDays: -50m, presenceRatio: 1m);

        Assert.Equal(1_920m, resolution.Hours);
    }

    // ============================================
    // PRORATA DE PRÉSENCE
    // ============================================

    [Fact]
    public void No_presence_dates_means_present_all_year()
    {
        Assert.Equal(1m, CollaboratorProductiveHoursCalculator.ComputePresenceRatio(2026, null, null));
    }

    [Fact]
    public void Dates_outside_the_exercise_do_not_shrink_the_ratio()
    {
        var ratio = CollaboratorProductiveHoursCalculator.ComputePresenceRatio(
            2026, new DateTime(2020, 3, 1), new DateTime(2030, 6, 30));

        Assert.Equal(1m, ratio);
    }

    [Fact]
    public void A_july_arrival_yields_roughly_half_the_year()
    {
        var ratio = CollaboratorProductiveHoursCalculator.ComputePresenceRatio(
            2026, new DateTime(2026, 7, 1), null);

        // 184 jours du 1er juillet au 31 décembre, sur 365.
        Assert.Equal(Math.Round(184m / 365m, 3, MidpointRounding.AwayFromZero), ratio);
    }

    [Fact]
    public void A_departure_before_the_exercise_yields_no_presence()
    {
        var ratio = CollaboratorProductiveHoursCalculator.ComputePresenceRatio(
            2026, null, new DateTime(2025, 6, 30));

        Assert.Equal(0m, ratio);
    }
}
