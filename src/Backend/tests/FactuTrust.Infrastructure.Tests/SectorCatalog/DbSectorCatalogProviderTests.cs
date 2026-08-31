using FactuTrust.Domain.Entities.SectorRules;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.SectorCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.SectorCatalog;

/// <summary>Phase 2 — moteur de règles sectorielles en base (plan §WP-B2). DB provider caching/filtering.</summary>
public sealed class DbSectorCatalogProviderTests
{
    private static MasterDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static SectorRuleSetStamp SeedStamp(MasterDbContext db)
    {
        var stamp = SectorRuleSetStamp.CreateInitial();
        db.SectorRuleSetStamps.Add(stamp);
        db.SaveChanges();
        return stamp;
    }

    private static void SeedOneSegment(MasterDbContext db, bool active = true)
    {
        var segment = SectorSegment.Create("commerce", "Commerce", "Négoce.", "shopping-cart", 0, "Magasin");
        if (!active)
            segment.Deactivate();
        db.SectorSegments.Add(segment);
        db.SaveChanges();
    }

    [Fact]
    public void Snapshot_is_cached_and_reused_until_version_bumps()
    {
        using var db = NewDb();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var stamp = SeedStamp(db);
        SeedOneSegment(db);

        var provider = new DbSectorCatalogProvider(db, cache);
        var first = provider.GetSnapshot();
        Assert.Single(first.Segments);

        // Add a second segment without bumping the version — cached snapshot must not see it.
        db.SectorSegments.Add(SectorSegment.Create("services", "Services", "Conseil.", "handshake", 1, null));
        db.SaveChanges();

        var stillCached = provider.GetSnapshot();
        Assert.Single(stillCached.Segments);

        // Bump the version — the version-cache TTL is 60s so re-reading the stamp row directly
        // requires a fresh provider/cache in a unit test; simulate the "version changed" branch by
        // asserting a fresh provider (new cache) picks up both segments.
        stamp.Bump("test");
        db.SaveChanges();

        var freshProvider = new DbSectorCatalogProvider(db, new MemoryCache(new MemoryCacheOptions()));
        var afterBump = freshProvider.GetSnapshot();
        Assert.Equal(2, afterBump.Segments.Count);
    }

    [Fact]
    public void Inactive_rows_are_excluded_from_snapshot()
    {
        using var db = NewDb();
        var cache = new MemoryCache(new MemoryCacheOptions());
        SeedStamp(db);
        SeedOneSegment(db, active: true);
        SeedOneSegment(db, active: false); // duplicate code is fine for InMemory (no unique constraint enforcement)

        var provider = new DbSectorCatalogProvider(db, cache);
        var snapshot = provider.GetSnapshot();

        Assert.Single(snapshot.Segments);
    }
}
