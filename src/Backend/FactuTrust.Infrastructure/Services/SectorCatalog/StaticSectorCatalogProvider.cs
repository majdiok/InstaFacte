using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.SectorConfiguration;

namespace FactuTrust.Infrastructure.Services.SectorCatalog;

/// <summary>
/// Permanent fallback <see cref="ISectorCatalogProvider"/> wrapping <see cref="SectorConfigurationCatalog"/>
/// (plan §WP-B2, D2). Built once in a <c>static readonly</c> — zero allocation per call, so the
/// hot registration path pays nothing extra when <c>UseDbRules=false</c>. <b>Never deleted</b>:
/// this is the class that keeps Phase 1 behavior available forever, flag-off or DB-outage.
/// </summary>
public sealed class StaticSectorCatalogProvider : ISectorCatalogProvider
{
    private static readonly SectorRuleSnapshot CachedSnapshot = BuildSnapshot();

    public SectorRuleSnapshot GetSnapshot() => CachedSnapshot;

    private static SectorRuleSnapshot BuildSnapshot()
    {
        // Review R1: this provider is used both when UseDbRules=false AND as the DB-outage
        // fallback inside CompositeSectorCatalogProvider — either way it must be a COMPLETE
        // rollback to Phase 1 behavior. Module dependencies (Phase 2) and chart-account data
        // templates (Phase 3) are additive features gated behind the DB rules; exposing them here
        // would silently keep them active even with the flag off, contradicting
        // ISectorDataTemplateApplier's documented contract ("the static provider always returns
        // an empty list, so this is a no-op for free whenever UseDbRules is off"). Segments,
        // domains and default settings are Phase 1 and stay fully populated from the catalog.
        var reference = SectorConfigurationCatalog.BuildCatalogSnapshot();

        return reference with
        {
            ModuleDependencies = Array.Empty<ModuleDependencySnapshot>(),
            DataTemplates = Array.Empty<DataTemplateSnapshot>()
        };
    }
}
