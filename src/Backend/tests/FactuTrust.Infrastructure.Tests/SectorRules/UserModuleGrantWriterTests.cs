using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.SectorRules;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.SectorRules;

/// <summary>
/// Plan §2.2 — shared <c>UserModuleGrant</c> rewrite logic, extracted so
/// <c>RegistrationSectorService</c>, <c>TenantSectorReconfigurationService</c> and
/// <c>CompanyModulesController</c> share one implementation.
/// </summary>
public sealed class UserModuleGrantWriterTests
{
    private static MasterDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Full_module_set_writes_no_grant_rows()
    {
        await using var db = NewDb();
        var userId = Guid.NewGuid();
        var fullSet = new HashSet<AppModule>(AppModuleExtensions.AllValues);

        await UserModuleGrantWriter.RewriteGrantsAsync(db, userId, fullSet, saveChanges: true, CancellationToken.None);

        Assert.Empty(await db.UserModuleGrants.Where(g => g.UserId == userId).ToListAsync());
    }

    [Fact]
    public async Task Restricted_set_writes_one_row_per_module()
    {
        await using var db = NewDb();
        var userId = Guid.NewGuid();
        var restricted = new HashSet<AppModule> { AppModule.Clients, AppModule.Products, AppModule.Sales, AppModule.Administration };

        await UserModuleGrantWriter.RewriteGrantsAsync(db, userId, restricted, saveChanges: true, CancellationToken.None);

        var grants = await db.UserModuleGrants.Where(g => g.UserId == userId).ToListAsync();
        Assert.Equal(AppModuleExtensions.AllValues.Length, grants.Count);
        Assert.All(grants, g => Assert.Equal(restricted.Contains(g.Module), g.IsEnabled));
    }

    [Fact]
    public async Task Rewrite_deletes_pre_existing_rows_before_writing_new_ones()
    {
        await using var db = NewDb();
        var userId = Guid.NewGuid();
        var first = new HashSet<AppModule> { AppModule.Clients, AppModule.Administration };
        await UserModuleGrantWriter.RewriteGrantsAsync(db, userId, first, saveChanges: true, CancellationToken.None);
        Assert.Equal(AppModuleExtensions.AllValues.Length, await db.UserModuleGrants.CountAsync(g => g.UserId == userId));

        // Rewriting to the full set must land on zero rows, not additively pile on top of the first write.
        var full = new HashSet<AppModule>(AppModuleExtensions.AllValues);
        await UserModuleGrantWriter.RewriteGrantsAsync(db, userId, full, saveChanges: true, CancellationToken.None);

        Assert.Empty(await db.UserModuleGrants.Where(g => g.UserId == userId).ToListAsync());
    }

    [Fact]
    public async Task SaveChanges_false_defers_persistence_to_the_caller()
    {
        await using var db = NewDb();
        var userId = Guid.NewGuid();
        var restricted = new HashSet<AppModule> { AppModule.Clients, AppModule.Administration };

        await UserModuleGrantWriter.RewriteGrantsAsync(db, userId, restricted, saveChanges: false, CancellationToken.None);
        // Not yet persisted to a fresh read against the same context's ChangeTracker-aware query —
        // but EF InMemory's local query still sees added-but-unsaved entities via the change tracker,
        // so assert via a raw entry-state check instead of a query that could vary by provider.
        Assert.Contains(db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added), _ => true);

        await db.SaveChangesAsync();
        Assert.Equal(AppModuleExtensions.AllValues.Length, await db.UserModuleGrants.CountAsync(g => g.UserId == userId));
    }
}
