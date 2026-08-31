using FactuTrust.Domain.SectorConfiguration;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Seam between <c>IRegistrationSectorService</c>/<c>PublicSectorCatalogController</c> and the
/// sector rule source (static catalog vs master-DB rule tables) — plan §WP-B2, D2.
/// <see cref="GetSnapshot"/> is deliberately <b>synchronous</b>: <c>ResolveProfile</c>'s signature
/// is locked sync, and implementations must serve from an in-memory cache on the hot path (only a
/// cache miss may touch the database, and only when DB rules are active).
/// </summary>
public interface ISectorCatalogProvider
{
    SectorRuleSnapshot GetSnapshot();
}
