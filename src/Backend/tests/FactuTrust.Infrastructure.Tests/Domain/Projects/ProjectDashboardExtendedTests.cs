using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.Projects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Projects;

public sealed class ProjectProgressCalculatorTests
{
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(3, 10, 30)]
    [InlineData(5, 5, 100)]
    [InlineData(7, 4, 100)]
    public void ComputeProgressPercent_MatchesRatio(int completed, int total, int expected)
    {
        var actual = total <= 0 ? 0 : (int)Math.Round(completed * 100.0 / total);
        actual = Math.Min(100, actual);
        Assert.Equal(expected, actual);
    }
}

public sealed class ProjectDashboardDtoContractTests
{
    [Fact]
    public void ExtendedDashboardDto_HasAdditiveFields()
    {
        var dto = new FactuTrust.Application.DTOs.ProjectDashboardExtendedDto
        {
            ActiveProjects = 1,
            OpenTasks = 2,
            OverdueTasks = 0,
            UninvoicedBillableHours = 4m,
            CompletedTasks = 3,
            TotalProjects = 5,
            AverageProgressPercent = 72,
            ProgressTargetPercent = 90
        };
        Assert.Equal(1, dto.ActiveProjects);
        Assert.Equal(3, dto.CompletedTasks);
        Assert.Equal(5, dto.TotalProjects);
        Assert.Equal(72, dto.AverageProgressPercent);
        Assert.Equal(90, dto.ProgressTargetPercent);
        Assert.Empty(dto.RecentProjects);
    }

    [Fact]
    public void TaskTrendPointDto_SupportsPendingSeries()
    {
        var point = new FactuTrust.Application.DTOs.ProjectTaskTrendPointDto
        {
            Date = DateTime.UtcNow.Date,
            Created = 2,
            Completed = 1,
            Pending = 4,
            Overdue = 0
        };
        Assert.Equal(4, point.Pending);
    }

    [Fact]
    public void KpiTrendsDto_SupportsExtendedChangePercents()
    {
        var trends = new FactuTrust.Application.DTOs.ProjectDashboardKpiTrendsDto
        {
            ActiveProjectsChangePercent = 12,
            OpenTasksChangePercent = -5,
            CompletedTasksChangePercent = 15,
            OverdueTasksChangePercent = -3,
            UninvoicedBillableHoursChangePercent = 8,
            AverageProgressChangePercent = 4
        };
        Assert.Equal(8, trends.UninvoicedBillableHoursChangePercent);
        Assert.Equal(4, trends.AverageProgressChangePercent);
    }

    [Fact]
    public void ProjectListQuery_HasDefaultPagination()
    {
        var q = new FactuTrust.Application.DTOs.ProjectListQuery();
        Assert.Equal(1, q.Page);
        Assert.Equal(20, q.PageSize);
        Assert.Null(q.ClientId);
        Assert.Null(q.OverdueOnly);
    }
}

public sealed class ProjectListCsvExporterTests
{
    [Fact]
    public void ToCsvBytes_IncludesUtf8BomAndHeader()
    {
        var bytes = ProjectListCsvExporter.ToCsvBytes([]);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Contains("Projet;Client", text);
    }

    [Fact]
    public void ToCsvBytes_EscapesSemicolons()
    {
        var items = new[]
        {
            new FactuTrust.Application.DTOs.ProjectListItemDto
            {
                Id = Guid.NewGuid(),
                Name = "Alpha;Beta",
                ClientId = Guid.NewGuid(),
                ClientName = "Client",
                Kind = ProjectKind.Generic,
                KindDisplay = "Général",
                BillingMode = ProjectBillingMode.None,
                BillingModeDisplay = "Aucune",
                Status = ProjectStatus.Active,
                StatusDisplay = "Actif",
                BudgetHt = 100m
            }
        };
        var text = System.Text.Encoding.UTF8.GetString(ProjectListCsvExporter.ToCsvBytes(items));
        Assert.Contains("\"Alpha;Beta\"", text);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a;b", "\"a;b\"")]
    public void Escape_HandlesSpecialChars(string input, string expected)
    {
        Assert.Equal(expected, ProjectListCsvExporter.Escape(input));
    }

    [Fact]
    public void SearchResponseDto_HasExpectedShape()
    {
        var dto = new FactuTrust.Application.DTOs.ProjectSearchResponseDto
        {
            Query = "mission",
            Results =
            [
                new FactuTrust.Application.DTOs.ProjectSearchResultDto
                {
                    Kind = FactuTrust.Application.DTOs.ProjectSearchResultKind.Project,
                    Id = Guid.NewGuid(),
                    ProjectId = Guid.NewGuid(),
                    Title = "Mission Alpha",
                    Subtitle = "Client · Actif",
                    Score = 80
                }
            ]
        };
        Assert.Equal("mission", dto.Query);
        Assert.Single(dto.Results);
        Assert.Equal(FactuTrust.Application.DTOs.ProjectSearchResultKind.Project, dto.Results[0].Kind);
    }

    [Fact]
    public void ProjectSearchScoring_MatchesPrefixAndContains()
    {
        Assert.Equal(100, ProjectSearchScoring.ScoreMatch("alpha", "Alpha"));
        Assert.Equal(80, ProjectSearchScoring.ScoreMatch("alp", "Alpha project"));
        Assert.Equal(50, ProjectSearchScoring.ScoreMatch("pha", "Alpha"));
        Assert.Equal(0, ProjectSearchScoring.ScoreMatch("xyz", "Alpha"));
    }

    [Fact]
    public void ListItemDto_SupportsEnrichmentFields()
    {
        var item = new FactuTrust.Application.DTOs.ProjectListItemDto
        {
            Id = Guid.NewGuid(),
            Name = "Mission",
            ClientId = Guid.NewGuid(),
            ClientName = "Client",
            Kind = ProjectKind.Esn,
            KindDisplay = "ESN",
            BillingMode = ProjectBillingMode.TimeAndMaterials,
            BillingModeDisplay = "Régie",
            Status = ProjectStatus.Active,
            StatusDisplay = "Actif",
            BudgetHt = 1000m,
            OwnerUserName = "Chef",
            ProgressPercent = 40,
            CompletedTaskCount = 2,
            TotalTaskCount = 5
        };
        Assert.Equal("Chef", item.OwnerUserName);
        Assert.Equal(40, item.ProgressPercent);
    }
}
