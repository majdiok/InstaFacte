namespace FactuTrust.Application.Configuration;

/// <summary>
/// Sector-aware registration wizard kill-switch (plan §3 C6, §6.1 B3). When <see cref="Enabled"/>
/// is false, <c>POST /api/auth/register</c> ignores <c>CompanySegment</c>/<c>BusinessDomain</c>/
/// <c>EnabledModules</c> (logged, treated as absent) and <c>GET /api/public/sector-catalog</c>
/// returns 404. Both effects revert instantly by flipping this flag back — no redeploy, no data
/// migration.
/// </summary>
public sealed class RegistrationSectorOptions
{
    public const string SectionName = "Features:RegistrationSector";

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Phase 2 (plan §WP-B2, D2/D10): when <c>true</c>, <c>CompositeSectorCatalogProvider</c> reads
    /// the master-DB rule tables instead of the static catalog. Defaults to <c>false</c> so no
    /// config change is required to keep byte-identical Phase 1 behavior; the DB source is never
    /// throwing — an error or an empty table set always falls back to static.
    /// </summary>
    public bool UseDbRules { get; set; } = false;
}
