using FactuTrust.Domain.Entities.SectorRules;
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
/// Phase 2 — parity checker (plan §WP-B9): proves the seeded master-DB rule set reproduces the
/// static catalog across every observable surface, including all 66 resolved <c>SectorProfile</c>s.
/// This is the flip-gate test — <c>UseDbRules</c> must not be enabled until
/// <see cref="Seeded_db_provider_is_parity_equal_to_static_provider_for_all_66_combos"/> is green.
/// InMemory-backed so the assertion runs without SQL Server.
/// </summary>
public sealed class SectorCatalogProviderParityTests
{
    private static MasterDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static DbSectorCatalogProvider NewDbProvider(MasterDbContext db) =>
        new(db, new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task Seeded_db_provider_is_parity_equal_to_static_provider_for_all_66_combos()
    {
        // Seed the master-DB rule tables straight from the static catalog, then read them back via
        // the real DbSectorCatalogProvider (a fresh MemoryCache so the test sees the seeded rows).
        await using var db = NewDb();
        await SectorRuleSeeder.SeedAsync(db, force: false, actor: "parity-test", CancellationToken.None);

        var staticSnapshot = new StaticSectorCatalogProvider().GetSnapshot();
        var dbSnapshot = NewDbProvider(db).GetSnapshot();

        var result = SectorRuleParityChecker.Check(staticSnapshot, dbSnapshot);

        // The seeder projects the catalog verbatim, so the two rule sets must match exactly —
        // including all 6 × 11 = 66 resolved profiles.
        Assert.True(result.IsMatch,
            result.Differences.Count == 0
                ? "Parity check unexpectedly returned IsMatch=false with no differences."
                : "Parity differences detected:\n" + string.Join("\n", result.Differences));
    }

    [Fact]
    public async Task Parity_checker_reports_difference_when_module_rule_removed()
    {
        // Seed, then deactivate one SegmentBase module rule so the DB rule set drifts from the
        // static catalog — the checker must surface the divergence as a non-empty Differences list.
        await using var db = NewDb();
        await SectorRuleSeeder.SeedAsync(db, force: false, actor: "parity-test", CancellationToken.None);

        // Remove a SegmentBase module rule (the first one) to create an observable drift.
        var firstModuleRule = await db.SectorModuleRules
            .Where(r => r.RuleKind == SectorModuleRuleKind.SegmentBase)
            .FirstAsync(CancellationToken.None);
        firstModuleRule.Deactivate();
        await db.SaveChangesAsync(CancellationToken.None);

        // Bump the version stamp so the cached snapshot is invalidated for the fresh provider.
        var stamp = await db.SectorRuleSetStamps.SingleAsync(CancellationToken.None);
        stamp.Bump("parity-test");
        await db.SaveChangesAsync(CancellationToken.None);

        var staticSnapshot = new StaticSectorCatalogProvider().GetSnapshot();
        var dbSnapshot = NewDbProvider(db).GetSnapshot();

        var result = SectorRuleParityChecker.Check(staticSnapshot, dbSnapshot);

        Assert.False(result.IsMatch);
        Assert.NotEmpty(result.Differences);
        // The drift surfaces on the affected segment's base recommended modules and on every combo
        // that resolves that segment — the flat list must reference recommended modules.
        Assert.Contains(result.Differences, d => d.Contains("recommandé", StringComparison.Ordinal));
        // And at least one difference must reference a resolved combo (segment+domain), proving the
        // 66-combo profile comparison is exercised, not just the raw catalog-row comparison.
        Assert.Contains(result.Differences, d => d.Contains('+'));
    }

    [Fact]
    public void Parity_checker_reports_match_when_both_snapshots_are_the_static_catalog()
    {
        // Two identical static snapshots trivially match — a guard against a checker that would
        // report spurious differences on equal inputs.
        var snapshot = new StaticSectorCatalogProvider().GetSnapshot();

        var result = SectorRuleParityChecker.Check(snapshot, snapshot);

        Assert.True(result.IsMatch);
        Assert.Empty(result.Differences);
    }
}
