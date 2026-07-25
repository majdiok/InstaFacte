using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces.Forecasting;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class TunisianFiscalDeadlineServiceTests
{
    private static MasterDbContext BuildMaster()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MasterDbContext(options);
    }

    [Fact]
    public void ComputeVatFilingDeadline_UsesSeededRule_WhenPresent()
    {
        using var db = BuildMaster();
        db.FiscalCalendarRules.Add(FiscalCalendarRule.CreateDefault(
            FiscalObligationType.MonthlyDeclaration, 22, 1, label: "TVA"));
        db.SaveChanges();

        var calendar = new Mock<ITunisianCalendarService>();
        calendar.Setup(c => c.IsHoliday(It.IsAny<DateTime>())).Returns(false);
        var service = new TunisianFiscalDeadlineService(db, calendar.Object);

        var due = service.ComputeVatFilingDeadline(2026, 7);

        // 22 août 2026 tombe un samedi → report au lundi 24
        Assert.Equal(new DateTime(2026, 8, 24), due);
    }

    [Fact]
    public void ComputeVatFilingDeadline_FallsBackToVatFilingDeadline_WhenNoRule()
    {
        using var db = BuildMaster();
        var calendar = new Mock<ITunisianCalendarService>();
        var service = new TunisianFiscalDeadlineService(db, calendar.Object);

        var due = service.ComputeVatFilingDeadline(2026, 7);

        Assert.Equal(VatFilingDeadline.ForPeriod(2026, 7), due);
    }

    [Fact]
    public void AdjustForWeekendsAndHolidays_ShiftsSaturdayToMonday()
    {
        using var db = BuildMaster();
        var calendar = new Mock<ITunisianCalendarService>();
        calendar.Setup(c => c.IsHoliday(It.IsAny<DateTime>())).Returns(false);
        var service = new TunisianFiscalDeadlineService(db, calendar.Object);

        // 2026-08-22 is a Saturday
        var adjusted = service.AdjustForWeekendsAndHolidays(new DateTime(2026, 8, 22));

        Assert.Equal(DayOfWeek.Monday, adjusted.DayOfWeek);
        Assert.Equal(new DateTime(2026, 8, 24), adjusted);
    }
}
