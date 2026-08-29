using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Master-DB backed, memory-cache-softened implementation of <see cref="ISecurityStampTokenValidator"/>
/// (plan §6 Phase 2.5). Cache TTL is capped at 5 seconds — documented staleness bound, acceptable across
/// nodes since every node re-reads the master DB at least every 5 seconds per active user.
/// </summary>
public sealed class SecurityStampTokenValidator : ISecurityStampTokenValidator
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(5);

    private readonly MasterDbContext _masterContext;
    private readonly IMemoryCache _cache;

    public SecurityStampTokenValidator(MasterDbContext masterContext, IMemoryCache cache)
    {
        _masterContext = masterContext;
        _cache = cache;
    }

    private static string CacheKey(Guid userId) => $"sstamp:{userId}";

    public void Invalidate(Guid userId) => _cache.Remove(CacheKey(userId));

    public async Task<bool> IsValidAsync(
        Guid userId,
        string? tokenSecurityStamp,
        bool requireSecurityStampClaim,
        CancellationToken cancellationToken)
    {
        // Stage 2 of the rollout (plan §6 Phase 2.5): once every access token in circulation was issued
        // after the claim started being emitted, absence of the claim itself is a red flag.
        if (string.IsNullOrEmpty(tokenSecurityStamp) && requireSecurityStampClaim)
            return false;

        var snapshot = await _cache.GetOrCreateAsync(CacheKey(userId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await _masterContext.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new UserStampSnapshot(u.SecurityStamp, u.IsActive))
                .FirstOrDefaultAsync(cancellationToken);
        });

        if (snapshot is null)
            return false; // user not found (deleted, or never existed) — never pass through

        if (!snapshot.IsActive)
            return false;

        if (string.IsNullOrEmpty(tokenSecurityStamp))
            return true; // Stage 1 (tolerant mode): pre-rollout tokens without the claim are accepted.

        return string.Equals(snapshot.SecurityStamp, tokenSecurityStamp, StringComparison.Ordinal);
    }

    private sealed record UserStampSnapshot(string? SecurityStamp, bool IsActive);
}
