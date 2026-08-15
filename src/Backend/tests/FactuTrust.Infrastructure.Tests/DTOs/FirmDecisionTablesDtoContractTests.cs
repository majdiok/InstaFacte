using System.Text.Json;
using FactuTrust.Application.DTOs;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.DTOs;

public sealed class FirmDecisionTablesDtoContractTests
{
    [Fact]
    public void FirmDecisionTablesDto_SerializesAllSections()
    {
        var dto = new FirmDecisionTablesDto
        {
            CriticalFiscalSchedules = [
                new FirmCriticalFiscalRowDto
                {
                    EntryId = Guid.NewGuid(),
                    CompanyTenantId = Guid.NewGuid(),
                    CompanyName = "Ste Test",
                    ObligationType = 1,
                    ObligationTypeDisplay = "TVA",
                    ObligationLabel = "TVA mensuelle",
                    DueDate = new DateTime(2026, 3, 15),
                    DaysUntilDue = -2,
                    IsOverdue = true,
                    EstimatedAmount = 1200.500m,
                    Currency = "TND"
                }
            ],
            AtRiskDossiers = [
                new FirmAtRiskDossierRowDto
                {
                    AssignmentId = Guid.NewGuid(),
                    CompanyTenantId = Guid.NewGuid(),
                    CompanyName = "At Risk",
                    Signals = ["Inactif 30j"],
                    PriorityScore = 3,
                    HasPermanentFile = false
                }
            ],
            NegativeMargins = [],
            PendingTimeSheets = [],
            SocialAlerts = [],
            HonorairesAlerts = [],
            Meta = new FirmDecisionTablesMetaDto
            {
                GeneratedAt = new DateTime(2026, 3, 12, 10, 0, 0, DateTimeKind.Utc),
                PartialFailures = [
                    new FirmDecisionTablesPartialFailureDto { Section = "socialAlerts", Message = "timeout" }
                ]
            }
        };

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("criticalFiscalSchedules").GetArrayLength());
        Assert.Equal(1, root.GetProperty("atRiskDossiers").GetArrayLength());
        Assert.True(root.GetProperty("meta").GetProperty("generatedAt").ValueKind == JsonValueKind.String);
        Assert.Equal(1, root.GetProperty("meta").GetProperty("partialFailures").GetArrayLength());
        Assert.Equal(1200.500m, root.GetProperty("criticalFiscalSchedules")[0].GetProperty("estimatedAmount").GetDecimal());
    }
}
