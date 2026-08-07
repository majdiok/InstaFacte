using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

/// <summary>
/// Golden tests du calcul des congés maladie (carence, IJ CNSS, subrogation, top-up).
/// </summary>
public sealed class SickLeaveCalculatorTests
{
    private const decimal BaseSalary = 1300m;
    private const decimal DailyRate = 50m; // 1300 / 26

    [Fact]
    public void TenDaysFirstEpisode_CarenceFiveDays_ThenIjAtTwoThirds()
    {
        var result = SickLeaveCalculator.Compute(new SickLeaveInput
        {
            SickDaysInMonth = 10m,
            BaseSalary = BaseSalary,
            PriorSickDaysInEpisode = 0m,
            PriorIjDaysInYear = 0m,
            WaitingDaysPolicy = 5,
            IjRatePercent = 66.67m,
            SubrogationEnabled = false
        });

        Assert.Equal(5m, result.WaitingDays);
        Assert.Equal(5m, result.IjDays);
        Assert.Equal(DailyRate, result.DailyRate);
        Assert.Equal(250m, result.DeductionAmount);          // 5 × 50
        Assert.Equal(166.675m, result.CnssIjAmount);         // 5 × 50 × 66,67 %
        Assert.Equal(0m, result.EmployerTopUpAmount);
        Assert.Equal(0m, result.SubrogationAdvanceAmount);
    }

    [Fact]
    public void SubrogationEnabled_AdvancesIjAmount()
    {
        var result = SickLeaveCalculator.Compute(new SickLeaveInput
        {
            SickDaysInMonth = 10m,
            BaseSalary = BaseSalary,
            WaitingDaysPolicy = 5,
            IjRatePercent = 66.67m,
            SubrogationEnabled = true
        });

        Assert.Equal(166.675m, result.SubrogationAdvanceAmount);
        Assert.Equal(result.CnssIjAmount, result.SubrogationAdvanceAmount);
    }

    [Fact]
    public void EmployerTopUp100Percent_ComplementsIjDays()
    {
        var result = SickLeaveCalculator.Compute(new SickLeaveInput
        {
            SickDaysInMonth = 10m,
            BaseSalary = BaseSalary,
            WaitingDaysPolicy = 5,
            IjRatePercent = 66.67m,
            EmployerTopUpPercent = 100m
        });

        // Complément = 5 j × 50 × (100 % − 66,67 %) = 83,325
        Assert.Equal(83.325m, result.EmployerTopUpAmount);
    }

    [Fact]
    public void PriorEpisodeWaitingConsumed_SkipsCarenceInMonth()
    {
        var result = SickLeaveCalculator.Compute(new SickLeaveInput
        {
            SickDaysInMonth = 5m,
            BaseSalary = BaseSalary,
            PriorSickDaysInEpisode = 5m,
            WaitingDaysPolicy = 5,
            IjRatePercent = 66.67m
        });

        Assert.Equal(0m, result.WaitingDays);
        Assert.Equal(5m, result.IjDays);
        Assert.Equal(0m, result.DeductionAmount);
    }

    [Fact]
    public void AnnualIjCap_180Days_LimitsIjDays()
    {
        var result = SickLeaveCalculator.Compute(new SickLeaveInput
        {
            SickDaysInMonth = 30m,
            BaseSalary = BaseSalary,
            PriorIjDaysInYear = 175m,
            WaitingDaysPolicy = 0,
            IjRatePercent = 66.67m
        });

        Assert.Equal(5m, result.IjDays);
    }
}
