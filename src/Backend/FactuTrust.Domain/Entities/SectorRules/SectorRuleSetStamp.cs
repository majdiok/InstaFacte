namespace FactuTrust.Domain.Entities.SectorRules;

/// <summary>
/// Single-row version stamp for the sector rule tables (Phase 2, plan §WP-B1). Every write path
/// (seeder, admin CRUD) increments <see cref="Version"/> inside the same <c>SaveChangesAsync</c> —
/// this is the cache-busting key <c>DbSectorCatalogProvider</c> uses to invalidate its 10-minute
/// snapshot cache. Exactly one row exists, with <see cref="Id"/> always <c>1</c>.
/// </summary>
public sealed class SectorRuleSetStamp
{
    public const int SingletonId = 1;

    public int Id { get; private set; } = SingletonId;
    public long Version { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    private SectorRuleSetStamp() { }

    public static SectorRuleSetStamp CreateInitial()
    {
        return new SectorRuleSetStamp
        {
            Id = SingletonId,
            Version = 0,
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedBy = null
        };
    }

    public void Bump(string? actor)
    {
        Version++;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = actor;
    }
}
