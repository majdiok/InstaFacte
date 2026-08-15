using FactuTrust.Application.Features.Accounting.Audit;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AccountingAudit;

/// <summary>
/// Évaluateur cron des planifications de contrôle. Il décide si un contrôle doit partir : une
/// erreur de lecture se traduit soit par un contrôle jamais lancé, soit par un contrôle rejoué en
/// boucle. D'où la couverture serrée ci-dessous.
/// </summary>
public sealed class CronOccurrenceEvaluatorTests
{
    private static readonly DateTime Monday0800 = new(2026, 8, 10, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Never_run_schedule_is_due_immediately()
    {
        Assert.True(CronOccurrenceEvaluator.HasOccurrenceBetween("0 6 * * *", lastRunUtc: null, Monday0800));
    }

    [Fact]
    public void Daily_schedule_fires_once_the_hour_is_passed()
    {
        // Dernière exécution hier 6h05, il est 8h : l'occurrence de 6h ce matin a été franchie.
        var lastRun = Monday0800.AddDays(-1).AddHours(-1).AddMinutes(5);
        Assert.True(CronOccurrenceEvaluator.HasOccurrenceBetween("0 6 * * *", lastRun, Monday0800));
    }

    [Fact]
    public void Daily_schedule_does_not_fire_twice_in_the_same_day()
    {
        // Déjà exécutée à 6h30 ce matin : plus aucune occurrence de 6h avant demain.
        var lastRun = new DateTime(2026, 8, 10, 6, 30, 0, DateTimeKind.Utc);
        Assert.False(CronOccurrenceEvaluator.HasOccurrenceBetween("0 6 * * *", lastRun, Monday0800));
    }

    [Fact]
    public void Weekly_schedule_respects_day_of_week()
    {
        var lastRun = new DateTime(2026, 8, 9, 23, 0, 0, DateTimeKind.Utc); // dimanche soir

        // 2026-08-10 est un lundi : « tous les lundis à 6h » est dû.
        Assert.True(CronOccurrenceEvaluator.HasOccurrenceBetween("0 6 * * 1", lastRun, Monday0800));
        // « tous les vendredis à 6h » ne l'est pas.
        Assert.False(CronOccurrenceEvaluator.HasOccurrenceBetween("0 6 * * 5", lastRun, Monday0800));
    }

    [Fact]
    public void Sunday_accepts_both_zero_and_seven()
    {
        var sunday = new DateTime(2026, 8, 9, 8, 0, 0, DateTimeKind.Utc);
        var lastRun = sunday.AddDays(-1);

        Assert.True(CronOccurrenceEvaluator.HasOccurrenceBetween("0 6 * * 0", lastRun, sunday));
        Assert.True(CronOccurrenceEvaluator.HasOccurrenceBetween("0 6 * * 7", lastRun, sunday));
    }

    [Fact]
    public void Day_of_month_and_day_of_week_combine_with_OR()
    {
        // Règle cron historique : les deux champs de jour restreints se lisent en OU.
        // 2026-08-10 est un lundi et n'est pas le 1er du mois : la clause « lundi » suffit.
        var lastRun = Monday0800.AddDays(-1);
        Assert.True(CronOccurrenceEvaluator.HasOccurrenceBetween("0 6 1 * 1", lastRun, Monday0800));
    }

    [Fact]
    public void Monthly_schedule_skips_months_without_the_day()
    {
        var lastRun = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc);
        var laterInAugust = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc);

        // Le 1er août est déjà passé au moment de lastRun : plus d'occurrence avant septembre.
        Assert.False(CronOccurrenceEvaluator.HasOccurrenceBetween("0 0 1 * *", lastRun, laterInAugust));
        Assert.True(CronOccurrenceEvaluator.HasOccurrenceBetween(
            "0 0 1 * *", lastRun, new DateTime(2026, 9, 1, 1, 0, 0, DateTimeKind.Utc)));
    }

    [Theory]
    [InlineData("@daily")]
    [InlineData("@midnight")]
    [InlineData("@weekly")]
    [InlineData("@monthly")]
    [InlineData("@yearly")]
    [InlineData("@annually")]
    [InlineData("@hourly")]
    public void Macros_are_supported(string macro)
    {
        Assert.True(CronOccurrenceEvaluator.IsValid(macro));
    }

    [Fact]
    public void Step_and_range_syntax_is_supported()
    {
        Assert.True(CronOccurrenceEvaluator.IsValid("*/15 * * * *"));
        Assert.True(CronOccurrenceEvaluator.IsValid("0 8-18/2 * * 1-5"));
        Assert.True(CronOccurrenceEvaluator.IsValid("0,30 6,18 * * *"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("pas du tout un cron")]
    [InlineData("0 6 * *")]        // 4 champs
    [InlineData("0 6 * * * *")]    // 6 champs
    [InlineData("99 6 * * *")]     // minute hors bornes
    [InlineData("0 25 * * *")]     // heure hors bornes
    [InlineData("0 6 32 * *")]     // jour hors bornes
    [InlineData("0 6 * 13 *")]     // mois hors bornes
    [InlineData("0 6 * * 8")]      // jour de semaine hors bornes
    [InlineData("0 6 * * */0")]    // pas nul
    [InlineData("0 10-5 * * *")]   // intervalle inversé
    public void Invalid_expressions_are_rejected(string? expression)
    {
        Assert.False(CronOccurrenceEvaluator.IsValid(expression));
        // Une expression illisible ne déclenche jamais : c'est le comportement sûr.
        Assert.False(CronOccurrenceEvaluator.HasOccurrenceBetween(expression, null, Monday0800));
    }

    [Fact]
    public void Very_old_last_run_is_due_without_scanning_the_whole_interval()
    {
        var lastRun = Monday0800.AddYears(-3);
        Assert.True(CronOccurrenceEvaluator.HasOccurrenceBetween("0 6 * * *", lastRun, Monday0800));
    }

    [Fact]
    public void Last_run_in_the_future_is_not_due()
    {
        // Horloge décalée ou reprise de sauvegarde : on ne rejoue pas.
        Assert.False(CronOccurrenceEvaluator.HasOccurrenceBetween("0 6 * * *", Monday0800.AddHours(1), Monday0800));
    }
}
