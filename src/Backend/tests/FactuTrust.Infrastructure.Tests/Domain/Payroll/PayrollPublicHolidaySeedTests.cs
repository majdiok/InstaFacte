using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

public sealed class PayrollPublicHolidaySeedTests
{
    [Fact]
    public void GetDefaultsForYear_2026_HasNoDuplicateDates()
    {
        var holidays = PayrollPublicHolidaySeed.GetDefaultsForYear(2026);
        var dates = holidays.Select(h => h.Date.Date).ToList();

        Assert.Equal(dates.Count, dates.Distinct().Count());
    }

    [Fact]
    public void GetDefaultsForYear_2026_MergesIndependenceAndAidAlFitr()
    {
        var holidays = PayrollPublicHolidaySeed.GetDefaultsForYear(2026);
        var march20 = holidays.Single(h => h.Date == new DateTime(2026, 3, 20));

        Assert.Contains("Fête de l'Indépendance", march20.Label);
        Assert.Contains("Aïd El-Fitr (1er jour)", march20.Label);
        Assert.True(march20.IsEstimated);
    }

    [Fact]
    public void GetDefaultsForYear_AllSupportedYears_HaveUniqueDates()
    {
        foreach (var year in PayrollPublicHolidaySeed.SupportedSeedYears)
        {
            var holidays = PayrollPublicHolidaySeed.GetDefaultsForYear(year);
            var dates = holidays.Select(h => h.Date.Date).ToList();

            Assert.Equal(dates.Count, dates.Distinct().Count());
        }
    }
}
