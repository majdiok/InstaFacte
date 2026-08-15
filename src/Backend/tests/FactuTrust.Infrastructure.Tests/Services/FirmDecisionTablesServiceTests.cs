using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class FirmDecisionTablesServiceTests
{
  private sealed class FakeTimeProvider : TimeProvider
  {
    private readonly DateTimeOffset _utcNow;
    public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
    public override DateTimeOffset GetUtcNow() => _utcNow;
  }
  [Fact]
  public async Task BuildCriticalFiscal_orders_overdue_before_upcoming()
  {
    var overdueId = Guid.NewGuid();
    var upcomingId = Guid.NewGuid();
    var companyId = Guid.NewGuid();
    var today = new DateTime(2026, 3, 12);

    var fiscal = new Mock<IFirmFiscalScheduleService>();
    fiscal.Setup(s => s.GetScheduleAsync(
        It.IsAny<Guid>(),
        It.IsAny<FiscalScheduleFiltersDto>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(new FiscalScheduleListDto
      {
        Items = [
          new FiscalScheduleEntryDto
          {
            Id = upcomingId,
            CompanyTenantId = companyId,
            CompanyName = "Upcoming Co",
            ObligationType = 1,
            ObligationTypeDisplay = "TVA",
            ObligationLabel = "TVA",
            DueDate = today.AddDays(3),
            EstimatedAmount = 500m,
            Status = (int)FiscalScheduleStatus.UpcomingWithin7Days,
            StatusDisplay = "A venir"
          },
          new FiscalScheduleEntryDto
          {
            Id = overdueId,
            CompanyTenantId = companyId,
            CompanyName = "Overdue Co",
            ObligationType = 1,
            ObligationTypeDisplay = "TVA",
            ObligationLabel = "TVA",
            DueDate = today.AddDays(-5),
            EstimatedAmount = 100m,
            Status = (int)FiscalScheduleStatus.Overdue,
            StatusDisplay = "En retard"
          }
        ]
      });

    var timeProfit = new Mock<IFirmTimeProfitabilityService>();
    timeProfit.Setup(s => s.GetDossierTimeProfitabilityAsync(
        It.IsAny<Guid>(),
        It.IsAny<string?>(),
        It.IsAny<int?>(),
        It.IsAny<Guid?>(),
        It.IsAny<FirmMarginSignFilter>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(new FirmDossierTimeProfitabilityReportDto());

    var governance = new Mock<IFirmGovernanceService>();
    governance.Setup(g => g.GetSocialOverviewAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new FirmSocialOverviewDto { Clients = [] });

    var rentability = new Mock<IFirmCollaboratorRentabilityService>();
    rentability.Setup(r => r.ListAsync(It.IsAny<Guid>(), It.IsAny<int?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new FirmCollaboratorRentabilityListDto
      {
        Items = [],
        Totals = new FirmCollaboratorRentabilityListItemDto
        {
          CollaboratorName = "Total",
          Year = 2026
        }
      });

    var service = CreateService(
      fiscal: fiscal,
      timeProfit: timeProfit,
      governance: governance,
      rentability: rentability,
      today: today);
    var result = await service.GetDecisionTablesAsync(Guid.NewGuid());

    Assert.Equal(2, result.CriticalFiscalSchedules.Count);
    Assert.Equal(overdueId, result.CriticalFiscalSchedules[0].EntryId);
    Assert.True(result.CriticalFiscalSchedules[0].IsOverdue);
  }

  private static FirmDecisionTablesService CreateService(
    Mock<IFirmFiscalScheduleService>? fiscal = null,
    Mock<IFirmTimeProfitabilityService>? timeProfit = null,
    Mock<IFirmGovernanceService>? governance = null,
    Mock<IFirmCollaboratorRentabilityService>? rentability = null,
    DateTime? today = null)
  {
    if (fiscal is null)
    {
      fiscal = new Mock<IFirmFiscalScheduleService>();
      fiscal.Setup(s => s.GetScheduleAsync(
          It.IsAny<Guid>(),
          It.IsAny<FiscalScheduleFiltersDto>(),
          It.IsAny<CancellationToken>()))
        .ReturnsAsync(new FiscalScheduleListDto());
    }

    if (timeProfit is null)
    {
      timeProfit = new Mock<IFirmTimeProfitabilityService>();
      timeProfit.Setup(s => s.GetDossierTimeProfitabilityAsync(
          It.IsAny<Guid>(),
          It.IsAny<string?>(),
          It.IsAny<int?>(),
          It.IsAny<Guid?>(),
          It.IsAny<FirmMarginSignFilter>(),
          It.IsAny<CancellationToken>()))
        .ReturnsAsync(new FirmDossierTimeProfitabilityReportDto());
    }

    if (governance is null)
    {
      governance = new Mock<IFirmGovernanceService>();
      governance.Setup(g => g.GetSocialOverviewAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new FirmSocialOverviewDto { Clients = [] });
    }

    if (rentability is null)
    {
      rentability = new Mock<IFirmCollaboratorRentabilityService>();
      rentability.Setup(r => r.ListAsync(It.IsAny<Guid>(), It.IsAny<int?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new FirmCollaboratorRentabilityListDto
        {
          Items = [],
          Totals = new FirmCollaboratorRentabilityListItemDto { CollaboratorName = "Total", Year = 2026 }
        });
    }

    var time = new FakeTimeProvider(new DateTimeOffset(today ?? new DateTime(2026, 3, 12, 12, 0, 0, DateTimeKind.Local)));

    return new FirmDecisionTablesService(
      null!,
      Mock.Of<ITenantService>(),
      fiscal.Object,
      timeProfit.Object,
      governance.Object,
      rentability.Object,
      new MemoryCache(new MemoryCacheOptions()),
      time,
      NullLogger<FirmDecisionTablesService>.Instance);
  }
}
