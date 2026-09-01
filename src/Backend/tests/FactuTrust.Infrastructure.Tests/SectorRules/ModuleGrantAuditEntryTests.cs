using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.SectorRules;

/// <summary>Plan §2.2 — master-DB audit trail for module-grant changes.</summary>
public sealed class ModuleGrantAuditEntryTests
{
    private static MasterDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Create_and_persist_round_trips_through_MasterDbContext()
    {
        await using var db = NewDb();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var entry = ModuleGrantAuditEntry.Create(tenantId, actorId, "company-modules-update", """{"enabledIds":[0,1,2]}""");
        db.ModuleGrantAuditEntries.Add(entry);
        await db.SaveChangesAsync();

        var reloaded = await db.ModuleGrantAuditEntries.AsNoTracking().SingleAsync(e => e.Id == entry.Id);
        Assert.Equal(tenantId, reloaded.TenantId);
        Assert.Equal(actorId, reloaded.ActorUserId);
        Assert.Equal("company-modules-update", reloaded.Action);
        Assert.Contains("enabledIds", reloaded.DiffJson);
    }

    [Fact]
    public void Create_rejects_blank_action()
    {
        Assert.Throws<ArgumentException>(() => ModuleGrantAuditEntry.Create(Guid.NewGuid(), null, "  ", "{}"));
    }

    [Fact]
    public void Create_allows_null_actor_for_system_initiated_changes()
    {
        var entry = ModuleGrantAuditEntry.Create(Guid.NewGuid(), actorUserId: null, "system-reseed", "{}");
        Assert.Null(entry.ActorUserId);
    }
}
