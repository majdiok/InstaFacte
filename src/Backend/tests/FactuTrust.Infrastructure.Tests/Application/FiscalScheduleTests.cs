using FactuTrust.Application.Features.Accounting.FiscalSchedule;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class FiscalScheduleTests
{
    [Fact]
    public void ResolveStatus_UsesDueDateDepositAndPayment()
    {
        var create = FiscalScheduleEntry.Create(
            FiscalObligationType.MonthlyDeclaration,
            "Declaration mensuelle",
            2026,
            new DateTime(2026, 7, 10),
            1250m);

        Assert.True(create.IsSuccess);
        var entry = create.Value;

        Assert.Equal(FiscalScheduleStatus.Overdue, entry.ResolveStatus(new DateTime(2026, 7, 11)));
        Assert.Equal(FiscalScheduleStatus.UpcomingWithin7Days, entry.ResolveStatus(new DateTime(2026, 7, 5)));
        Assert.Equal(FiscalScheduleStatus.UpcomingAfter7Days, entry.ResolveStatus(new DateTime(2026, 7, 1)));

        Assert.True(entry.MarkDeposited(new DateTime(2026, 7, 8)).IsSuccess);
        Assert.Equal(FiscalScheduleStatus.Deposited, entry.ResolveStatus(new DateTime(2026, 7, 11)));

        Assert.True(entry.MarkValidated(new DateTime(2026, 7, 9), "comptable@cabinet.tn").IsSuccess);
        Assert.Equal(FiscalScheduleStatus.Validated, entry.ResolveStatus(new DateTime(2026, 7, 11)));

        Assert.True(entry.CapturePayment(new DateTime(2026, 7, 10)).IsSuccess);
        Assert.Equal(FiscalScheduleStatus.Paid, entry.ResolveStatus(new DateTime(2026, 7, 11)));
    }

    [Fact]
    public void CapturePayment_RejectsDateBeforeDeposit()
    {
        var entry = FiscalScheduleEntry.Create(
            FiscalObligationType.Fodec,
            "FODEC",
            2026,
            new DateTime(2026, 8, 22),
            450m).Value;

        Assert.True(entry.MarkDeposited(new DateTime(2026, 8, 20)).IsSuccess);

        var result = entry.CapturePayment(new DateTime(2026, 8, 19));

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.PaymentDate", result.Error.Code);
    }

    [Fact]
    public void BuildSummary_ComputesSageDashboardCounters()
    {
        var today = new DateTime(2026, 7, 10);
        var soon = FiscalScheduleEntry.Create(FiscalObligationType.MonthlyDeclaration, "Declaration mensuelle", 2026, today.AddDays(3), 100m).Value;
        var later = FiscalScheduleEntry.Create(FiscalObligationType.WithholdingTax, "Retenue a la source", 2026, today.AddDays(15), 200m).Value;
        var overdue = FiscalScheduleEntry.Create(FiscalObligationType.Fodec, "FODEC", 2026, today.AddDays(-1), 300m).Value;
        var deposited = FiscalScheduleEntry.Create(FiscalObligationType.QuarterlyVat, "TVA trimestrielle", 2026, today.AddDays(8), 400m).Value;
        Assert.True(deposited.MarkDeposited(today).IsSuccess);

        var rows = new[] { soon, later, overdue, deposited }
            .Select(entry => FiscalScheduleMappings.ToDto(entry, today))
            .ToList();

        var summary = FiscalScheduleMappings.BuildSummary(rows, today);

        Assert.Equal(1, summary.UpcomingWithin7DaysCount);
        Assert.Equal(100m, summary.UpcomingWithin7DaysAmount);
        Assert.Equal(1, summary.UpcomingAfter7DaysCount);
        Assert.Equal(200m, summary.UpcomingAfter7DaysAmount);
        Assert.Equal(1, summary.OverdueCount);
        Assert.Equal(300m, summary.OverdueAmount);
        Assert.Equal(1, summary.DepositedThisMonthCount);
        Assert.Equal(400m, summary.DepositedThisMonthAmount);
        Assert.Equal(4, summary.TotalCount);
        Assert.Equal(1000m, summary.TotalAmount);
    }

    [Fact]
    public void ValidatePeriodConsistency_RequiresQuarterForQuarterlyVat()
    {
        var result = FiscalScheduleMappings.ValidatePeriodConsistency(
            FiscalObligationType.QuarterlyVat,
            null,
            null);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.PeriodQuarter", result.Error.Code);
    }

    [Fact]
    public void ValidatePeriodConsistency_RejectsMonthAndQuarterTogether()
    {
        var result = FiscalScheduleMappings.ValidatePeriodConsistency(
            FiscalObligationType.MonthlyDeclaration,
            3,
            1);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.PeriodMonth", result.Error.Code);
    }

    [Fact]
    public void MarkValidated_RequiresDepositAndBlocksWhenPaid()
    {
        var entry = FiscalScheduleEntry.Create(
            FiscalObligationType.Fodec,
            "FODEC",
            2026,
            new DateTime(2026, 8, 22),
            450m).Value;

        var withoutDeposit = entry.MarkValidated(new DateTime(2026, 8, 20), "user@test.tn");
        Assert.True(withoutDeposit.IsFailure);
        Assert.Equal("Validation.DepositDate", withoutDeposit.Error.Code);

        Assert.True(entry.MarkDeposited(new DateTime(2026, 8, 20)).IsSuccess);
        Assert.True(entry.MarkValidated(new DateTime(2026, 8, 21), "user@test.tn").IsSuccess);
        Assert.Equal(FiscalScheduleStatus.Validated, entry.ResolveStatus(new DateTime(2026, 8, 22)));

        Assert.True(entry.CapturePayment(new DateTime(2026, 8, 22)).IsSuccess);
        var afterPaid = entry.MarkValidated(new DateTime(2026, 8, 23), "user@test.tn");
        Assert.True(afterPaid.IsFailure);
    }

    [Fact]
    public void Create_RejectsNegativeAmount()
    {
        var result = FiscalScheduleEntry.Create(
            FiscalObligationType.Fodec,
            "FODEC",
            2026,
            new DateTime(2026, 8, 22),
            -1m);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.EstimatedAmount", result.Error.Code);
    }
}
