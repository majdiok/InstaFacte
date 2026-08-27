using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Tests du projecteur d'échéancier partagé (T1/D1) — source unique de vérité entre
/// le moteur de facturation et les endpoints de prévision.
/// </summary>
public sealed class RecurringContractScheduleProjectorTests
{
    [Fact]
    public void Project_Monthly_DayOfMonthClampedInFebruary()
    {
        var occurrences = RecurringContractScheduleProjector.Project(
            nextBillingDate: null,
            startDate: new DateTime(2026, 1, 15),
            endDate: null,
            autoRenew: false,
            BillingFrequency.Monthly,
            billingDayOfMonth: 31,
            count: 3);

        Assert.Equal(3, occurrences.Count);
        Assert.Equal(new DateTime(2026, 1, 31), occurrences[0].Date);
        Assert.Equal(new DateTime(2026, 2, 28), occurrences[1].Date); // clampé (2026 non bissextile)
        Assert.Equal(new DateTime(2026, 3, 31), occurrences[2].Date);
        Assert.Equal(new DateTime(2026, 2, 28), occurrences[1].PeriodFrom);
        // PeriodTo = PeriodFrom + 1 mois − 1 jour (convention ResolveBillingPeriod) → 27/03.
        Assert.Equal(new DateTime(2026, 3, 27), occurrences[1].PeriodTo);
    }

    [Fact]
    public void Project_Quarterly_AdvancesByThreeMonths()
    {
        var occurrences = RecurringContractScheduleProjector.Project(
            null, new DateTime(2026, 1, 1), null, false, BillingFrequency.Quarterly, 1, 3);

        Assert.Equal(3, occurrences.Count);
        Assert.Equal(new DateTime(2026, 1, 1), occurrences[0].Date);
        Assert.Equal(new DateTime(2026, 4, 1), occurrences[1].Date);
        Assert.Equal(new DateTime(2026, 7, 1), occurrences[2].Date);
        Assert.Equal(new DateTime(2026, 1, 1), occurrences[0].PeriodFrom);
        Assert.Equal(new DateTime(2026, 3, 31), occurrences[0].PeriodTo);
    }

    [Fact]
    public void Project_Annual_AdvancesByTwelveMonths()
    {
        var occurrences = RecurringContractScheduleProjector.Project(
            null, new DateTime(2026, 1, 1), null, false, BillingFrequency.Annual, 1, 2);

        Assert.Equal(2, occurrences.Count);
        Assert.Equal(new DateTime(2026, 1, 1), occurrences[0].Date);
        Assert.Equal(new DateTime(2027, 1, 1), occurrences[1].Date);
        Assert.Equal(new DateTime(2026, 12, 31), occurrences[0].PeriodTo);
    }

    [Fact]
    public void Project_StopsAtEndDate_WhenAutoRenewFalse()
    {
        var occurrences = RecurringContractScheduleProjector.Project(
            null, new DateTime(2026, 1, 1), new DateTime(2026, 3, 31),
            autoRenew: false, BillingFrequency.Monthly, 1, count: 12);

        Assert.Equal(3, occurrences.Count);
        Assert.Equal(new DateTime(2026, 3, 1), occurrences[^1].Date);
    }

    [Fact]
    public void Project_ContinuesBeyondEndDate_WhenAutoRenewTrue()
    {
        var occurrences = RecurringContractScheduleProjector.Project(
            null, new DateTime(2026, 1, 1), new DateTime(2026, 3, 31),
            autoRenew: true, BillingFrequency.Monthly, 1, count: 5);

        Assert.Equal(5, occurrences.Count);
        Assert.Equal(new DateTime(2026, 5, 1), occurrences[^1].Date);
    }

    [Fact]
    public void Project_NullNextBillingDate_StartsFromInitialDate()
    {
        // Contrat brouillon : NextBillingDate null → départ de la date initiale calculée.
        var occurrences = RecurringContractScheduleProjector.Project(
            null, new DateTime(2026, 6, 10), null, false, BillingFrequency.Monthly, 15, 2);

        Assert.Equal(2, occurrences.Count);
        Assert.Equal(new DateTime(2026, 6, 15), occurrences[0].Date);
        Assert.Equal(new DateTime(2026, 7, 15), occurrences[1].Date);
    }

    [Fact]
    public void ComputeInitialBillingDate_BeforeBillingDay_UsesSameMonth()
    {
        var date = RecurringContractScheduleProjector.ComputeInitialBillingDate(
            new DateTime(2026, 3, 10), 15);

        Assert.Equal(new DateTime(2026, 3, 15), date);
    }

    [Fact]
    public void ComputeInitialBillingDate_AfterBillingDay_UsesNextMonth()
    {
        var date = RecurringContractScheduleProjector.ComputeInitialBillingDate(
            new DateTime(2026, 3, 20), 15);

        Assert.Equal(new DateTime(2026, 4, 15), date);
    }

    [Fact]
    public void ComputeInitialBillingDate_OnBillingDay_UsesSameMonth()
    {
        var date = RecurringContractScheduleProjector.ComputeInitialBillingDate(
            new DateTime(2026, 3, 15), 15);

        Assert.Equal(new DateTime(2026, 3, 15), date);
    }
}
