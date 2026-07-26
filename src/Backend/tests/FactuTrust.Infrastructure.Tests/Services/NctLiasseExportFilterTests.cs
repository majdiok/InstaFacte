using FactuTrust.Application.Accounting;
using FactuTrust.Application.DTOs;
using FactuTrust.Infrastructure.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class NctLiasseExportFilterTests
{
    private static NctFinancialStatementsDto SampleLiasse()
    {
        var cur = new Dictionary<string, decimal>
        {
            ["214"] = 30000m, ["224"] = 45000m, ["281"] = -40000m,
            ["101"] = -15000m, ["401"] = -5000m, ["532"] = 8000m,
            ["70"] = -20000m, ["601"] = 12000m
        };
        var baseDto = NctStatementBuilder.Build(2025, cur, new Dictionary<string, decimal>(), enabled: true);
        var detailed = NctDetailedNotesBuilder.Build(cur, new Dictionary<string, decimal>());
        return baseDto with { DetailedNotes = detailed };
    }

    [Fact]
    public void Apply_OmitsUncheckedSections_ViaFlags()
    {
        var source = SampleLiasse();
        var options = new NctLiasseExportOptions
        {
            FiscalYear = 2025,
            AsOfDate = new DateOnly(2025, 12, 31),
            IncludeAssets = true,
            IncludeLiabilities = false,
            IncludeIncomeStatement = false,
            IncludeCashFlow = false,
            IncludeAnnexAssets = false,
            IncludeAnnexLiabilities = false,
            IncludeAnnexIncomeStatement = false,
            IncludeAnnexCashFlow = false,
            SelectedNoteNumbers = Array.Empty<int>()
        };

        var result = NctLiasseExportFilter.Apply(source, options);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IncludeAssets);
        Assert.False(result.Value.IncludeIncomeStatement);
        Assert.Empty(result.Value.DetailedNotes);
        // Source non mutée
        Assert.NotEmpty(source.IncomeStatement.Lines);
    }

    [Fact]
    public void Apply_FiltersNotesBySelection()
    {
        var source = SampleLiasse();
        var options = new NctLiasseExportOptions
        {
            FiscalYear = 2025,
            AsOfDate = new DateOnly(2025, 12, 31),
            IncludeAnnexAssets = true,
            SelectedNoteNumbers = new[] { 1, 4 }
        };

        var result = NctLiasseExportFilter.Apply(source, options);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { 1, 4 }, result.Value.DetailedNotes.Select(n => n.Number).OrderBy(n => n));
    }

    [Fact]
    public void Apply_RequiresAtLeastOneSelection()
    {
        var source = SampleLiasse();
        var options = new NctLiasseExportOptions
        {
            FiscalYear = 2025,
            AsOfDate = new DateOnly(2025, 12, 31)
        };

        var result = NctLiasseExportFilter.Apply(source, options);

        Assert.True(result.IsFailure);
        Assert.Contains("Sélectionnez", result.Error.Description);
    }

    [Fact]
    public void PreviousPeriodLabel_DependsOnModeOnly()
    {
        var yearEnd = NctLiasseExportOptions.All(2025) with
        {
            AsOfDate = new DateOnly(2025, 6, 30),
            PreviousYearLabelMode = NctPreviousYearLabelMode.YearEnd31Dec
        };
        var sameDate = yearEnd with { PreviousYearLabelMode = NctPreviousYearLabelMode.SameCalendarDate };

        Assert.Equal("31/12/2024", yearEnd.PreviousPeriodLabel);
        Assert.Equal("30/06/2024", sameDate.PreviousPeriodLabel);
        Assert.Equal("30/06/2025", yearEnd.CurrentPeriodLabel);
    }
}
