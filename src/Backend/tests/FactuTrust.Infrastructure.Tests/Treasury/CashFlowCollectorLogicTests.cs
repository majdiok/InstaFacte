using FactuTrust.Domain.Entities.Treasury;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.Treasury.Collectors;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Treasury;

/// <summary>
/// Logique pure des collecteurs : développement des engagements récurrents, datation de la paie,
/// médiane des retards. Ces calculs se testent sans base et concentrent l'essentiel du risque
/// d'erreur de date.
/// </summary>
public sealed class CashFlowCollectorLogicTests
{
    private static RecurringCashCommitment Commitment(
        CashCommitmentFrequency frequency,
        int dayOfMonth,
        DateTime start,
        DateTime? end = null) =>
        RecurringCashCommitment.Create(
            "Loyer",
            CashFlowDirection.Outflow,
            2_500m,
            frequency,
            dayOfMonth,
            start,
            end);

    // ──────────────── Engagements récurrents ────────────────

    [Fact]
    public void Expand_Monthly_ProducesOneOccurrencePerMonth()
    {
        var commitment = Commitment(CashCommitmentFrequency.Monthly, 5, new DateTime(2026, 1, 1));

        var occurrences = RecurringCommitmentCollector
            .ExpandOccurrences(commitment, new DateTime(2026, 6, 1), new DateTime(2026, 8, 31))
            .ToList();

        Assert.Equal(
            new[] { new DateTime(2026, 6, 5), new DateTime(2026, 7, 5), new DateTime(2026, 8, 5) },
            occurrences);
    }

    [Fact]
    public void Expand_DayThirtyOne_FallsBackToLastDayOfShortMonths()
    {
        // Sans ce repli, un prélèvement au 31 disparaîtrait quatre à cinq fois par an.
        var commitment = Commitment(CashCommitmentFrequency.Monthly, 31, new DateTime(2026, 1, 1));

        var occurrences = RecurringCommitmentCollector
            .ExpandOccurrences(commitment, new DateTime(2026, 2, 1), new DateTime(2026, 4, 30))
            .ToList();

        Assert.Equal(
            new[] { new DateTime(2026, 2, 28), new DateTime(2026, 3, 31), new DateTime(2026, 4, 30) },
            occurrences);
    }

    [Fact]
    public void Expand_Quarterly_StaysAlignedOnTheStartMonth()
    {
        // Un trimestriel démarré en février tombe en février, mai, août — pas sur les trimestres
        // civils.
        var commitment = Commitment(CashCommitmentFrequency.Quarterly, 10, new DateTime(2026, 2, 1));

        var occurrences = RecurringCommitmentCollector
            .ExpandOccurrences(commitment, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31))
            .ToList();

        Assert.Equal(
            new[]
            {
                new DateTime(2026, 2, 10),
                new DateTime(2026, 5, 10),
                new DateTime(2026, 8, 10),
                new DateTime(2026, 11, 10)
            },
            occurrences);
    }

    [Fact]
    public void Expand_StopsAtEndDate()
    {
        var commitment = Commitment(
            CashCommitmentFrequency.Monthly, 5, new DateTime(2026, 1, 1), end: new DateTime(2026, 7, 20));

        var occurrences = RecurringCommitmentCollector
            .ExpandOccurrences(commitment, new DateTime(2026, 6, 1), new DateTime(2026, 12, 31))
            .ToList();

        Assert.Equal(new[] { new DateTime(2026, 6, 5), new DateTime(2026, 7, 5) }, occurrences);
    }

    [Fact]
    public void Expand_IgnoresOccurrencesBeforeTheCommitmentStarts()
    {
        var commitment = Commitment(CashCommitmentFrequency.Monthly, 5, new DateTime(2026, 7, 15));

        var occurrences = RecurringCommitmentCollector
            .ExpandOccurrences(commitment, new DateTime(2026, 6, 1), new DateTime(2026, 9, 30))
            .ToList();

        Assert.Equal(new[] { new DateTime(2026, 8, 5), new DateTime(2026, 9, 5) }, occurrences);
    }

    [Fact]
    public void Expand_Weekly_StepsBySevenDays()
    {
        var commitment = Commitment(CashCommitmentFrequency.Weekly, 1, new DateTime(2026, 6, 1));

        var occurrences = RecurringCommitmentCollector
            .ExpandOccurrences(commitment, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30))
            .ToList();

        Assert.Equal(
            new[]
            {
                new DateTime(2026, 6, 1),
                new DateTime(2026, 6, 8),
                new DateTime(2026, 6, 15),
                new DateTime(2026, 6, 22),
                new DateTime(2026, 6, 29)
            },
            occurrences);
    }

    [Fact]
    public void Expand_AnnualOutsideWindow_ProducesNothing()
    {
        var commitment = Commitment(CashCommitmentFrequency.Annual, 15, new DateTime(2026, 1, 1));

        var occurrences = RecurringCommitmentCollector
            .ExpandOccurrences(commitment, new DateTime(2026, 6, 1), new DateTime(2026, 8, 31))
            .ToList();

        Assert.Empty(occurrences);
    }

    // ──────────────── Datation de la paie ────────────────

    [Fact]
    public void PayDate_UsesConfiguredDayOfMonth()
    {
        Assert.Equal(new DateTime(2026, 6, 28), PayrollCollector.ResolvePayDate(2026, 6, 28));
    }

    [Fact]
    public void PayDate_ClampsToLastDayOfShortMonth()
    {
        Assert.Equal(new DateTime(2026, 2, 28), PayrollCollector.ResolvePayDate(2026, 2, 31));
    }

    [Fact]
    public void PayDate_ClampsOutOfRangeDayToFirst()
    {
        Assert.Equal(new DateTime(2026, 6, 1), PayrollCollector.ResolvePayDate(2026, 6, 0));
    }

    // ──────────────── Médiane des retards ────────────────

    [Fact]
    public void Median_OddCount_TakesMiddleValue()
    {
        Assert.Equal(15, PaymentDelayStatistics.Median(new[] { 3, 15, 90 }));
    }

    [Fact]
    public void Median_EvenCount_AveragesTheTwoMiddleValues()
    {
        Assert.Equal(23, PaymentDelayStatistics.Median(new[] { 10, 16, 30, 90 }));
    }

    [Fact]
    public void Median_EmptySequence_IsZero()
    {
        Assert.Equal(0, PaymentDelayStatistics.Median(Array.Empty<int>()));
    }

    [Fact]
    public void Median_ResistsASingleOutlier()
    {
        // Une facture en litige réglée à 400 jours ne doit pas déplacer la projection de tout un
        // portefeuille — c'est précisément pourquoi la médiane est préférée à la moyenne.
        var withOutlier = PaymentDelayStatistics.Median(new[] { 28, 30, 32, 400 });
        Assert.True(withOutlier < 60);
    }

    [Fact]
    public void EmptyStatistics_ApplyNoShift()
    {
        Assert.Equal(0, PaymentDelayStatistics.Empty.ResolveShiftDays(Guid.NewGuid()));
    }
}
