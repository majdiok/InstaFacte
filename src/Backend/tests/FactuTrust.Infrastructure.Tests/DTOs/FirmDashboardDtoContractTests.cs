using System.Text.Json;
using FactuTrust.Application.DTOs;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.DTOs;

/// <summary>
/// Regression guard: FirmDashboardDto must expose all fiscal KPI fields expected by the firm dashboard UI.
/// </summary>
public sealed class FirmDashboardDtoContractTests
{
    [Fact]
    public void FirmDashboardDto_SerializesAllFiscalKpiFields()
    {
        var dto = new FirmDashboardDto
        {
            ActiveClientsCount = 3,
            PendingInvitationsCount = 1,
            InactiveDossiersCount = 0,
            VatDraftsCount = 2,
            OverdueSchedulesCount = 4,
            UpcomingWithin7DaysCount = 5,
            TejPendingCount = 1,
            LiasseDraftsCount = 2,
            DtsPendingCount = 3,
            OverdueEstimatedAmount = 1500.500m,
            Upcoming7DaysEstimatedAmount = 800.250m,
            Clients = [],
            PendingInvitations = []
        };

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(3, root.GetProperty("activeClientsCount").GetInt32());
        Assert.Equal(4, root.GetProperty("overdueSchedulesCount").GetInt32());
        Assert.Equal(5, root.GetProperty("upcomingWithin7DaysCount").GetInt32());
        Assert.Equal(1, root.GetProperty("tejPendingCount").GetInt32());
        Assert.Equal(2, root.GetProperty("liasseDraftsCount").GetInt32());
        Assert.Equal(3, root.GetProperty("dtsPendingCount").GetInt32());
        Assert.Equal(1500.500m, root.GetProperty("overdueEstimatedAmount").GetDecimal());
        Assert.Equal(800.250m, root.GetProperty("upcoming7DaysEstimatedAmount").GetDecimal());
    }
}
