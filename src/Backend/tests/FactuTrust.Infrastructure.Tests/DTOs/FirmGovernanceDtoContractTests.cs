using System.Text.Json;
using FactuTrust.Application.DTOs;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.DTOs;

public sealed class FirmGovernanceDtoContractTests
{
    [Fact]
    public void FirmTimeSheetEntryDto_SerializesCamelCase()
    {
        var dto = new FirmTimeSheetEntryDto
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            UserDisplayName = "Jean Test",
            FirmClientAssignmentId = Guid.Parse("cceccccc-cccc-cccc-cccc-cccccccccccc"),
            ClientCompanyName = "Ste X",
            WorkDate = new DateTime(2026, 7, 15),
            Hours = 4.5m,
            ActivityCode = "COMPTA",
            Notes = "Revue",
            IsBillable = true,
            IsValidated = false
        };

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(4.5m, root.GetProperty("hours").GetDecimal());
        Assert.True(root.GetProperty("isBillable").GetBoolean());
        Assert.False(root.GetProperty("isValidated").GetBoolean());
        Assert.Equal("COMPTA", root.GetProperty("activityCode").GetString());
    }

    [Fact]
    public void CreateTimeSheetEntryDto_SerializesTargetUserIdCamelCase()
    {
        var dto = new CreateTimeSheetEntryDto
        {
            WorkDate = new DateTime(2026, 7, 15),
            Hours = 2m,
            TargetUserId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            IsBillable = true
        };

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("targetUserId", out _));
    }

    [Fact]
    public void FirmGovernanceDashboardDto_SerializesAllKpiFields()
    {
        var dto = new FirmGovernanceDashboardDto
        {
            ActiveDossiersCount = 5,
            PermanentFilesCompleteCount = 2,
            PermanentFilesInProgressCount = 1,
            TotalBillableHoursMonth = 10.5m,
            TotalBillableHoursYear = 120m,
            PendingExpenseNotesCount = 3,
            OverdueFiscalSchedulesCount = 71,
            UnreadNotificationsCount = 0
        };

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(5, root.GetProperty("activeDossiersCount").GetInt32());
        Assert.Equal(3, root.GetProperty("pendingExpenseNotesCount").GetInt32());
        Assert.Equal(71, root.GetProperty("overdueFiscalSchedulesCount").GetInt32());
    }

    [Fact]
    public void FirmClientDossierDto_SerializesPermanentFileFields()
    {
        var dto = new FirmClientDossierDto
        {
            AssignmentId = Guid.NewGuid(),
            CompanyTenantId = Guid.NewGuid(),
            CompanyName = "Ste Test",
            ActiveSince = DateTime.UtcNow,
            HasPermanentFile = true,
            PermanentFileStatus = 2,
            PermanentFileStatusDisplay = "Complet"
        };

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("hasPermanentFile").GetBoolean());
        Assert.Equal(2, root.GetProperty("permanentFileStatus").GetInt32());
        Assert.Equal("Complet", root.GetProperty("permanentFileStatusDisplay").GetString());
    }

    [Fact]
    public void PermanentFileDto_SerializesBillingFieldsCamelCase()
    {
        var dto = new PermanentFileDto
        {
            Id = Guid.NewGuid(),
            FirmClientAssignmentId = Guid.NewGuid(),
            CompanyTenantId = Guid.NewGuid(),
            Status = 1,
            StatusDisplay = "En cours",
            WizardStep = 5,
            AnnualFeeAmount = 1500m,
            BillingFrequency = 2,
            BillingFrequencyDisplay = "Annuel",
            Currency = "TND",
            BillingNotes = "Forfait"
        };

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(1500m, root.GetProperty("annualFeeAmount").GetDecimal());
        Assert.Equal(2, root.GetProperty("billingFrequency").GetInt32());
        Assert.Equal("Annuel", root.GetProperty("billingFrequencyDisplay").GetString());
        Assert.Equal("TND", root.GetProperty("currency").GetString());
        Assert.Equal("Forfait", root.GetProperty("billingNotes").GetString());
    }

    [Fact]
    public void PermanentFileDto_SerializesResignationAndQualityFieldsCamelCase()
    {
        var dto = new PermanentFileDto
        {
            Id = Guid.NewGuid(),
            FirmClientAssignmentId = Guid.NewGuid(),
            CompanyTenantId = Guid.NewGuid(),
            Status = 1,
            StatusDisplay = "En cours",
            WizardStep = 4,
            MissionResigned = true,
            ResignationFiscalYear = 2025,
            ResignationNotes = "Notes démission",
            LabCompletedAt = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc),
            MissionAcceptedAt = new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Utc),
            IsBusinessComplete = false,
            MissingItems = new[] { "Dirigeant" },
            CompletionPercent = 62,
            NextRecommendedStep = 2,
            NextActionLabel = "Dirigeant"
        };

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("missionResigned").GetBoolean());
        Assert.Equal(2025, root.GetProperty("resignationFiscalYear").GetInt32());
        Assert.Equal("Notes démission", root.GetProperty("resignationNotes").GetString());
        Assert.Equal(62, root.GetProperty("completionPercent").GetInt32());
        Assert.Equal(2, root.GetProperty("nextRecommendedStep").GetInt32());
        Assert.Equal("Dirigeant", root.GetProperty("nextActionLabel").GetString());
    }
}
