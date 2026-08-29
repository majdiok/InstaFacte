using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Unit coverage for <see cref="SecurityStampTokenValidator"/> — plan §6 Phase 2.5 / §7.1.D/F.
/// Uses an EF Core InMemory-backed <see cref="MasterDbContext"/> (no real SQL Server needed): the
/// validator only issues plain LINQ queries against <c>Users</c>, never touches Identity's
/// UserManager machinery, so seeding rows directly via <c>Add</c> is sufficient and much cheaper
/// than a full SQL Server round-trip.
/// </summary>
public sealed class SecurityStampTokenValidatorTests
{
    private static MasterDbContext BuildMaster(string dbName) =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options);

    private static ApplicationUser MakeUser(bool isActive, string securityStamp) => new()
    {
        Id = Guid.NewGuid(),
        UserName = $"{Guid.NewGuid():N}@example.com",
        Email = $"{Guid.NewGuid():N}@example.com",
        FirstName = "Test",
        LastName = "User",
        TenantId = Guid.NewGuid(),
        IsActive = isActive,
        SecurityStamp = securityStamp
    };

    [Fact]
    public async Task Matching_stamp_and_active_user_is_valid()
    {
        var dbName = Guid.NewGuid().ToString();
        var user = MakeUser(isActive: true, securityStamp: "stamp-A");
        using var master = BuildMaster(dbName);
        master.Users.Add(user);
        await master.SaveChangesAsync();

        var validator = new SecurityStampTokenValidator(master, new MemoryCache(new MemoryCacheOptions()));

        var result = await validator.IsValidAsync(user.Id, "stamp-A", requireSecurityStampClaim: false, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task Mismatched_stamp_is_rejected_this_is_the_whole_point_of_revocation()
    {
        var dbName = Guid.NewGuid().ToString();
        var user = MakeUser(isActive: true, securityStamp: "stamp-CURRENT");
        using var master = BuildMaster(dbName);
        master.Users.Add(user);
        await master.SaveChangesAsync();

        var validator = new SecurityStampTokenValidator(master, new MemoryCache(new MemoryCacheOptions()));

        // Token carries the OLD stamp (issued before a role change / deactivation rotated it).
        var result = await validator.IsValidAsync(user.Id, "stamp-OLD", requireSecurityStampClaim: false, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task Inactive_user_is_rejected_even_with_matching_stamp()
    {
        var dbName = Guid.NewGuid().ToString();
        var user = MakeUser(isActive: false, securityStamp: "stamp-A");
        using var master = BuildMaster(dbName);
        master.Users.Add(user);
        await master.SaveChangesAsync();

        var validator = new SecurityStampTokenValidator(master, new MemoryCache(new MemoryCacheOptions()));

        var result = await validator.IsValidAsync(user.Id, "stamp-A", requireSecurityStampClaim: false, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task Nonexistent_user_is_rejected_fail_closed_never_passes_through()
    {
        using var master = BuildMaster(Guid.NewGuid().ToString());
        var validator = new SecurityStampTokenValidator(master, new MemoryCache(new MemoryCacheOptions()));

        var result = await validator.IsValidAsync(Guid.NewGuid(), "any-stamp", requireSecurityStampClaim: false, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task Rollout_stage1_tolerant_token_without_sstamp_claim_is_accepted()
    {
        var dbName = Guid.NewGuid().ToString();
        var user = MakeUser(isActive: true, securityStamp: "stamp-A");
        using var master = BuildMaster(dbName);
        master.Users.Add(user);
        await master.SaveChangesAsync();

        var validator = new SecurityStampTokenValidator(master, new MemoryCache(new MemoryCacheOptions()));

        // Pre-rollout token: no sstamp claim at all, RequireSecurityStampClaim still false (stage 1).
        var result = await validator.IsValidAsync(user.Id, tokenSecurityStamp: null, requireSecurityStampClaim: false, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task Rollout_stage2_strict_token_without_sstamp_claim_is_rejected()
    {
        var dbName = Guid.NewGuid().ToString();
        var user = MakeUser(isActive: true, securityStamp: "stamp-A");
        using var master = BuildMaster(dbName);
        master.Users.Add(user);
        await master.SaveChangesAsync();

        var validator = new SecurityStampTokenValidator(master, new MemoryCache(new MemoryCacheOptions()));

        // Stage 2 of the rollout: RequireSecurityStampClaim=true → a claim-less token is now a red flag.
        var result = await validator.IsValidAsync(user.Id, tokenSecurityStamp: null, requireSecurityStampClaim: true, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task Stamp_rotation_after_first_lookup_is_masked_by_the_five_second_cache_window()
    {
        // Documents the accepted staleness bound (plan §6 Phase 2.5): within the cache TTL, a
        // just-rotated stamp is NOT yet visible to a validator instance that already cached the
        // old snapshot. This is the deliberate trade-off (no Redis / no invalidation bus) — the
        // bound is enforced by the cache's own AbsoluteExpirationRelativeToNow, not tested here by
        // sleeping 5s (flaky/slow); instead we assert the masking behavior directly.
        var dbName = Guid.NewGuid().ToString();
        var user = MakeUser(isActive: true, securityStamp: "stamp-BEFORE-ROTATION");
        var cache = new MemoryCache(new MemoryCacheOptions());

        using var master1 = BuildMaster(dbName);
        master1.Users.Add(user);
        await master1.SaveChangesAsync();

        var validator = new SecurityStampTokenValidator(master1, cache);

        // First call populates the cache with the pre-rotation stamp.
        Assert.True(await validator.IsValidAsync(user.Id, "stamp-BEFORE-ROTATION", requireSecurityStampClaim: false, CancellationToken.None));

        // Rotate the stamp via a DIFFERENT context against the SAME InMemory database (simulates
        // another node/request committing the mutation).
        using (var master2 = BuildMaster(dbName))
        {
            var tracked = await master2.Users.FirstAsync(u => u.Id == user.Id);
            tracked.SecurityStamp = "stamp-AFTER-ROTATION";
            await master2.SaveChangesAsync();
        }

        // Same validator instance (same cache) still sees the OLD stamp within the TTL window —
        // this is the documented staleness bound, not a bug.
        Assert.True(await validator.IsValidAsync(user.Id, "stamp-BEFORE-ROTATION", requireSecurityStampClaim: false, CancellationToken.None));

        // A validator with a FRESH cache (new node / cache miss) sees the rotated stamp immediately.
        var freshValidator = new SecurityStampTokenValidator(master1, new MemoryCache(new MemoryCacheOptions()));
        Assert.False(await freshValidator.IsValidAsync(user.Id, "stamp-BEFORE-ROTATION", requireSecurityStampClaim: false, CancellationToken.None));
    }

    [Fact]
    public async Task Invalidate_breaks_the_cache_mask_immediately_on_the_same_node()
    {
        // Complements Stamp_rotation_after_first_lookup_is_masked_by_the_five_second_cache_window:
        // proves the escape hatch a stamp-rotating mutation is expected to call right after commit
        // (TenantUsersController.Update, plan §6 Phase 2.5) — same validator/cache instance, no need
        // to wait out the TTL or spin up a fresh validator.
        var dbName = Guid.NewGuid().ToString();
        var user = MakeUser(isActive: true, securityStamp: "stamp-BEFORE-ROTATION");
        var cache = new MemoryCache(new MemoryCacheOptions());

        using var master = BuildMaster(dbName);
        master.Users.Add(user);
        await master.SaveChangesAsync();

        var validator = new SecurityStampTokenValidator(master, cache);

        // First call populates the cache with the pre-rotation stamp.
        Assert.True(await validator.IsValidAsync(user.Id, "stamp-BEFORE-ROTATION", requireSecurityStampClaim: false, CancellationToken.None));

        var tracked = await master.Users.FirstAsync(u => u.Id == user.Id);
        tracked.SecurityStamp = "stamp-AFTER-ROTATION";
        await master.SaveChangesAsync();

        // Without invalidation, this SAME validator instance would still report the old stamp valid
        // (the masking behavior documented above). Evict, then observe the fresh result immediately.
        validator.Invalidate(user.Id);

        Assert.False(await validator.IsValidAsync(user.Id, "stamp-BEFORE-ROTATION", requireSecurityStampClaim: false, CancellationToken.None));
    }

    [Fact]
    public async Task Master_db_failure_propagates_the_exception_so_the_caller_can_fail_closed()
    {
        // SecurityStampTokenValidator itself does not swallow exceptions — plan §6 Phase 2.5's
        // fail-closed guarantee is implemented by the CALLER (Program.cs OnTokenValidated's
        // try/catch → context.Fail()). This test pins that contract: if the validator ever started
        // swallowing DB errors into a "valid" result, this would regress silently and dangerously.
        var master = BuildMaster(Guid.NewGuid().ToString());
        await master.DisposeAsync(); // simulates an unreachable/broken master DB

        var validator = new SecurityStampTokenValidator(master, new MemoryCache(new MemoryCacheOptions()));

        await Assert.ThrowsAnyAsync<ObjectDisposedException>(
            () => validator.IsValidAsync(Guid.NewGuid(), "stamp", requireSecurityStampClaim: false, CancellationToken.None));
    }
}
