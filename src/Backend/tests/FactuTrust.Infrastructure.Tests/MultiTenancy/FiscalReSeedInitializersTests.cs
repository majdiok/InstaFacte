using FactuTrust.Domain.Entities;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.MultiTenancy;

/// <summary>
/// Plan §2.4 — the fiscal re-seed on TaxRegime change reuses these three initializers verbatim.
/// Covers the idempotence contract added this session (they now REPORT via a bool return whether
/// anything was actually inserted/refreshed, which <c>TenantService.ReSeedFiscalParametersAsync</c>
/// uses to build the "what was updated" response). <see cref="TenantDbContext"/> instances here are
/// InMemory-backed — <c>TenantService.ReSeedFiscalParametersAsync</c> itself hard-codes the SQL
/// Server provider and can only be exercised against a real tenant DB, so this test targets the
/// reusable initializer building blocks directly.
/// </summary>
public sealed class FiscalReSeedInitializersTests
{
    private static TenantDbContext NewContext() =>
        new(new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task WithholdingTaxCatalogInitializer_FirstRun_InsertsSystemTypes_AndReportsChanged()
    {
        using var context = NewContext();

        var changed = await WithholdingTaxCatalogInitializer.EnsureSystemTypesSeededAsync(context);

        Assert.True(changed);
        Assert.NotEmpty(context.WithholdingTaxTypes);
    }

    [Fact]
    public async Task WithholdingTaxCatalogInitializer_SecondRun_IsNoOp_AndReportsUnchanged()
    {
        using var context = NewContext();
        await WithholdingTaxCatalogInitializer.EnsureSystemTypesSeededAsync(context);
        var countAfterFirstRun = await context.WithholdingTaxTypes.CountAsync();

        var changedOnReRun = await WithholdingTaxCatalogInitializer.EnsureSystemTypesSeededAsync(context);

        Assert.False(changedOnReRun);
        Assert.Equal(countAfterFirstRun, await context.WithholdingTaxTypes.CountAsync());
    }

    [Fact]
    public async Task WithholdingFiscalYearParameterInitializer_FirstRun_InsertsYears_AndReportsChanged()
    {
        using var context = NewContext();

        var changed = await WithholdingFiscalYearParameterInitializer.EnsureDefaultsSeededAsync(context);

        Assert.True(changed);
        Assert.NotEmpty(context.WithholdingFiscalYearParameters);
    }

    [Fact]
    public async Task WithholdingFiscalYearParameterInitializer_SecondRun_IsNoOp_AndReportsUnchanged()
    {
        using var context = NewContext();
        await WithholdingFiscalYearParameterInitializer.EnsureDefaultsSeededAsync(context);

        var changedOnReRun = await WithholdingFiscalYearParameterInitializer.EnsureDefaultsSeededAsync(context);

        Assert.False(changedOnReRun);
    }

    [Fact]
    public async Task IncomeTaxYearParameterInitializer_FirstRun_InsertsYears_AndReportsChanged()
    {
        using var context = NewContext();

        var changed = await IncomeTaxYearParameterInitializer.EnsureDefaultsSeededAsync(context);

        Assert.True(changed);
        Assert.NotEmpty(context.IncomeTaxYearParameters);
    }

    [Fact]
    public async Task IncomeTaxYearParameterInitializer_SecondRun_IsNoOp_AndReportsUnchanged()
    {
        using var context = NewContext();
        await IncomeTaxYearParameterInitializer.EnsureDefaultsSeededAsync(context);

        var changedOnReRun = await IncomeTaxYearParameterInitializer.EnsureDefaultsSeededAsync(context);

        Assert.False(changedOnReRun);
    }

    [Fact]
    public async Task IncomeTaxYearParameterInitializer_NeverOverwritesUserModifiedRows()
    {
        using var context = NewContext();
        await IncomeTaxYearParameterInitializer.EnsureDefaultsSeededAsync(context);

        var row = await context.IncomeTaxYearParameters.FirstAsync(p => p.FiscalYear == 2024);
        row.Update(
            row.IsStandardRate, row.IsReducedRate, row.IsSectorRate,
            row.MinTaxRate, row.MinTaxReducedRate, row.MinTaxFloorTnd,
            row.CssApplies, cssRate: 0.99m, row.CssFloorTnd, // clearly diverges from the legal defaults so a refresh would be detectable
            row.AcompteRate, row.AcompteCount, row.DeficitCarryForwardYears,
            row.IrppBracketsJson,
            row.MinTaxFloorReducedTnd, row.RoundTaxableToDinar,
            markUserModified: true);
        await context.SaveChangesAsync();

        await IncomeTaxYearParameterInitializer.EnsureDefaultsSeededAsync(context);

        var reloaded = await context.IncomeTaxYearParameters
            .AsNoTracking()
            .FirstAsync(p => p.FiscalYear == 2024);
        Assert.True(reloaded.IsUserModified);
        Assert.Equal(0.99m, reloaded.CssRate);
    }

    [Theory]
    [InlineData("entreprise", TaxRegime.RealRegime, false)]
    [InlineData("entreprise", TaxRegime.FlatRateRegime, true)]
    [InlineData("commerce", TaxRegime.FlatRateRegime, false)]
    [InlineData("association", TaxRegime.RealRegime, true)]
    [InlineData("association", TaxRegime.Exempt, false)]
    public void UsualTaxRegimeCatalog_IsAtypical_MatchesTheHandPickedTable(string segmentCode, TaxRegime regime, bool expectedAtypical)
    {
        Assert.Equal(expectedAtypical, UsualTaxRegimeCatalog.IsAtypical(segmentCode, regime));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-segment")]
    public void UsualTaxRegimeCatalog_IsAtypical_NeverFlagsUnknownOrMissingSegments(string? segmentCode)
    {
        Assert.False(UsualTaxRegimeCatalog.IsAtypical(segmentCode, TaxRegime.Exempt));
    }
}
