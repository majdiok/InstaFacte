using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

/// <summary>
/// Golden tests du calcul des congés maternité (IJ CNSS + complément employeur).
/// </summary>
public sealed class MaternityLeaveCalculatorTests
{
    private const decimal BaseSalary = 1300m;
    private const decimal DailyRate = 50m;

    [Fact]
    public void SixtyDaysAtFullMaintenance_SplitsIjAndEmployerTopUp()
    {
        var result = MaternityLeaveCalculator.Compute(new MaternityLeaveInput
        {
            MaternityDaysInMonth = 60m,
            BaseSalary = BaseSalary,
            IjRatePercent = 66.67m,
            EmployerTopUpPercent = 100m
        });

        Assert.Equal(60m, result.MaternityDays);
        Assert.Equal(DailyRate, result.DailyRate);
        Assert.Equal(2000.100m, result.CnssIjAmount);       // 60 × 50 × 66,67 %
        Assert.Equal(999.900m, result.EmployerTopUpAmount); // 60 × 50 × 33,33 %
        Assert.Equal(3000m, result.TotalMaintenanceAmount);
    }

    [Fact]
    public void PartialMonth_ProRataMaintenance()
    {
        var result = MaternityLeaveCalculator.Compute(new MaternityLeaveInput
        {
            MaternityDaysInMonth = 15m,
            BaseSalary = BaseSalary,
            IjRatePercent = 66.67m,
            EmployerTopUpPercent = 100m
        });

        Assert.Equal(500.025m, result.CnssIjAmount);
        Assert.Equal(249.975m, result.EmployerTopUpAmount);
        Assert.Equal(750m, result.TotalMaintenanceAmount);
    }

    [Fact]
    public void ZeroDays_ReturnsEmpty()
    {
        var result = MaternityLeaveCalculator.Compute(new MaternityLeaveInput
        {
            MaternityDaysInMonth = 0m,
            BaseSalary = BaseSalary,
            IjRatePercent = 66.67m,
            EmployerTopUpPercent = 100m
        });

        Assert.Equal(0m, result.TotalMaintenanceAmount);
    }
}
