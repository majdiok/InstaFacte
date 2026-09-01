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

    /// <summary>
    /// SHA-256 hex digest of the in-memory catalog's content at the last seed run (review R2) —
    /// lets <c>SectorRuleSeeder.ReconcileOnStartupAsync</c> detect a catalog change (new/removed
    /// segment, dependency edge, template, ...) across app restarts, WITHOUT re-running a full
    /// force-seed on every single startup when nothing changed. <c>null</c> for rows created
    /// before this column existed or by a seed run older than the hash tracking.
    /// </summary>
    public string? CatalogContentHash { get; private set; }

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

    public void SetCatalogHash(string hash) => CatalogContentHash = hash;
}
