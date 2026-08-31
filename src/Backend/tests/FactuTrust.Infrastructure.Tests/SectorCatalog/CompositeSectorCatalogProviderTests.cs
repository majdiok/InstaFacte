using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Entities.SectorRules;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.SectorCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.SectorCatalog;

/// <summary>Phase 2 — moteur de règles sectorielles en base (plan §WP-B2, D2/D10). Composite fallback semantics.</summary>
public sealed class CompositeSectorCatalogProviderTests
{
    private static MasterDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static CompositeSectorCatalogProvider NewComposite(MasterDbContext db, bool useDbRules)
    {
        var staticProvider = new StaticSectorCatalogProvider();
        var dbProvider = new DbSectorCatalogProvider(db, new MemoryCache(new MemoryCacheOptions()));
        var options = Options.Create(new RegistrationSectorOptions { UseDbRules = useDbRules });
        return new CompositeSectorCatalogProvider(staticProvider, dbProvider, options, NullLogger<CompositeSectorCatalogProvider>.Instance);
    }

    [Fact]
    public void Flag_off_returns_static_snapshot_without_touching_db()
    {
        using var db = NewDb();
        var composite = NewComposite(db, useDbRules: false);

        var snapshot = composite.GetSnapshot();

        Assert.Equal(SectorRuleSource.Static, snapshot.Source);
    }

    [Fact]
    public void Flag_on_with_empty_tables_falls_back_to_static()
    {
        using var db = NewDb();
        db.SectorRuleSetStamps.Add(SectorRuleSetStamp.CreateInitial());
        db.SaveChanges();

        var composite = NewComposite(db, useDbRules: true);
        var snapshot = composite.GetSnapshot();

        Assert.Equal(SectorRuleSource.Static, snapshot.Source);
    }

    [Fact]
    public void Flag_on_with_populated_db_returns_db_snapshot()
    {
        using var db = NewDb();
        db.SectorRuleSetStamps.Add(SectorRuleSetStamp.CreateInitial());
        db.SectorSegments.Add(SectorSegment.Create("commerce", "Commerce", "Négoce.", "shopping-cart", 0, "Magasin"));
        db.SaveChanges();

        var composite = NewComposite(db, useDbRules: true);
        var snapshot = composite.GetSnapshot();

        Assert.Equal(SectorRuleSource.Db, snapshot.Source);
        Assert.Single(snapshot.Segments);
    }

    [Fact]
    public void Flag_on_with_db_error_falls_back_to_static_and_logs_warning()
    {
        // A disposed context makes any query on it throw, exercising the composite's catch path.
        var db = NewDb();
        db.Dispose();

        var composite = NewComposite(db, useDbRules: true);
        var snapshot = composite.GetSnapshot();

        Assert.Equal(SectorRuleSource.Static, snapshot.Source);
    }
}
