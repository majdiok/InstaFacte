using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Persistence.Seeds;
using FactuTrust.Infrastructure.Services.SectorCatalog;
using FactuTrust.Infrastructure.Services.SectorRules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.SectorRules;

/// <summary>
/// Plan §3.1 — suggested tax regimes in the sector catalog + admin edit path. Covers: the static
/// catalog DTO surfaces the suggestions, the static/rollback provider keeps them populated
/// (informational, unlike ModuleDependencies), the seeder seeds them, the DB provider loads them,
/// and the admin CRUD (create/update/duplicate-reject/unknown-segment-reject) works and bumps the
/// version stamp.
/// </summary>
public sealed class SectorTaxRegimeSuggestionTests
{
    private static MasterDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static SectorRuleAdminService NewService(MasterDbContext db) => new(db);

    [Fact]
    public void Static_catalog_dto_surfaces_suggested_tax_regimes()
    {
        var dto = SectorCatalogDtoMapper.BuildCatalog(new StaticSectorCatalogProvider().GetSnapshot());

        // Nine catalog-declared suggestions across the six segments.
        Assert.Equal(9, dto.SuggestedTaxRegimes.Count);

        // Spot-check a couple of (segment, regime, noteFr) tuples.
        Assert.Contains(dto.SuggestedTaxRegimes, s =>
            s.SegmentCode == CompanySegments.Association && s.Regime == (int)TaxRegime.Exempt
            && !string.IsNullOrWhiteSpace(s.NoteFr));
        Assert.Contains(dto.SuggestedTaxRegimes, s =>
            s.SegmentCode == CompanySegments.BtpConstruction && s.Regime == (int)TaxRegime.RealRegime);
    }

    [Fact]
    public void Static_provider_keeps_tax_regime_suggestions_populated_unlike_active_features()
    {
        var snapshot = new StaticSectorCatalogProvider().GetSnapshot();

        // Design decision (plan §3.1): tax regime suggestions are purely informational, like
        // DefaultSettings, so the static/rollback provider keeps them fully populated — whereas the
        // Phase 2/3 *active* features (ModuleDependencies, DataTemplates) are deliberately emptied
        // so a flag-off is a complete rollback.
        Assert.NotEmpty(snapshot.TaxRegimeSuggestions);
        Assert.Empty(snapshot.ModuleDependencies);
        Assert.Empty(snapshot.DataTemplates);
    }

    [Fact]
    public async Task Seeder_seeds_all_catalog_tax_regime_suggestions()
    {
        await using var db = NewDb();
        await SectorRuleSeeder.SeedAsync(db, force: false, actor: "test", CancellationToken.None);

        var rows = await db.SectorTaxRegimeSuggestions
            .Where(s => s.IsActive)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(CancellationToken.None);

        Assert.Equal(SectorConfigurationCatalog.TaxRegimeSuggestions.Count, rows.Count);
        // All seeded rows are catalog-owned.
        Assert.All(rows, r => Assert.True(r.IsManagedByCatalog));
    }

    [Fact]
    public async Task Db_provider_loads_seeded_tax_regime_suggestions()
    {
        await using var db = NewDb();
        await SectorRuleSeeder.SeedAsync(db, force: false, actor: "test", CancellationToken.None);

        var snapshot = new DbSectorCatalogProvider(db, new MemoryCache(new MemoryCacheOptions())).GetSnapshot();

        Assert.Equal(SectorConfigurationCatalog.TaxRegimeSuggestions.Count, snapshot.TaxRegimeSuggestions.Count);
        Assert.Contains(snapshot.TaxRegimeSuggestions, s =>
            s.SegmentCode == CompanySegments.Association && s.Regime == (int)TaxRegime.Exempt);
    }

    [Fact]
    public async Task Admin_create_and_update_tax_regime_suggestion_bumps_version_and_marks_admin_managed()
    {
        await using var db = NewDb();
        await SectorRuleSeeder.SeedAsync(db, force: false, actor: "test", CancellationToken.None);
        var service = NewService(db);

        var versionBefore = await service.GetVersionAsync(CancellationToken.None);

        var created = await service.CreateTaxRegimeSuggestionAsync(
            new CreateSectorTaxRegimeSuggestionRequest
            {
                SegmentCode = CompanySegments.Commerce,
                Regime = (int)TaxRegime.Exempt,
                NoteFr = "Note de test admin.",
                SortOrder = 99
            },
            actor: "admin", CancellationToken.None);

        Assert.True(created.IsSuccess);
        Assert.Equal(CompanySegments.Commerce, created.Value.SegmentCode);
        Assert.Equal((int)TaxRegime.Exempt, created.Value.Regime);
        Assert.True(created.Value.IsActive);

        var versionAfterCreate = await service.GetVersionAsync(CancellationToken.None);
        Assert.True(versionAfterCreate > versionBefore, "Create must bump the catalog version stamp.");

        var updated = await service.UpdateTaxRegimeSuggestionAsync(
            created.Value.Id,
            new UpdateSectorTaxRegimeSuggestionRequest { NoteFr = "Note modifiée.", SortOrder = 50 },
            actor: "admin", CancellationToken.None);

        Assert.True(updated.IsSuccess);
        Assert.Equal("Note modifiée.", updated.Value.NoteFr);
        Assert.Equal(50, updated.Value.SortOrder);

        // An admin edit must flip the row to admin-managed so startup reconciliation never overwrites it.
        var row = await db.SectorTaxRegimeSuggestions.SingleAsync(s => s.Id == created.Value.Id, CancellationToken.None);
        Assert.False(row.IsManagedByCatalog);
    }

    [Fact]
    public async Task Admin_create_rejects_duplicate_segment_regime_pair()
    {
        await using var db = NewDb();
        await SectorRuleSeeder.SeedAsync(db, force: false, actor: "test", CancellationToken.None);
        var service = NewService(db);

        // The catalog already seeds commerce + RealRegime — recreating the same pair must conflict.
        var result = await service.CreateTaxRegimeSuggestionAsync(
            new CreateSectorTaxRegimeSuggestionRequest
            {
                SegmentCode = CompanySegments.Commerce,
                Regime = (int)TaxRegime.RealRegime,
                NoteFr = "Doublon."
            },
            actor: "admin", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
    }

    [Fact]
    public async Task Admin_create_rejects_unknown_segment()
    {
        await using var db = NewDb();
        var service = NewService(db);

        var result = await service.CreateTaxRegimeSuggestionAsync(
            new CreateSectorTaxRegimeSuggestionRequest
            {
                SegmentCode = "segment-inconnu",
                Regime = (int)TaxRegime.RealRegime,
                NoteFr = "x"
            },
            actor: "admin", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.SegmentCode", result.Error.Code);
    }

    [Fact]
    public async Task Admin_deactivate_tax_regime_suggestion_soft_deletes()
    {
        await using var db = NewDb();
        await SectorRuleSeeder.SeedAsync(db, force: false, actor: "test", CancellationToken.None);
        var service = NewService(db);

        var first = await db.SectorTaxRegimeSuggestions.FirstAsync(CancellationToken.None);
        var result = await service.DeactivateTaxRegimeSuggestionAsync(first.Id, actor: "admin", CancellationToken.None);

        Assert.True(result.IsSuccess);

        var row = await db.SectorTaxRegimeSuggestions.AsNoTracking().SingleAsync(s => s.Id == first.Id, CancellationToken.None);
        Assert.False(row.IsActive);
        // Still present (soft delete), never hard-deleted.
        Assert.NotNull(row);
    }
}
