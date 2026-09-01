using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Infrastructure.Services.SectorCatalog;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.SectorCatalog;

/// <summary>Phase 2 — moteur de règles sectorielles en base (plan §WP-B2). Static provider parity with Phase 1.</summary>
public sealed class StaticSectorCatalogProviderTests
{
    [Fact]
    public void Static_snapshot_matches_catalog_segment_and_domain_counts()
    {
        var provider = new StaticSectorCatalogProvider();
        var snapshot = provider.GetSnapshot();

        Assert.Equal(SectorRuleSource.Static, snapshot.Source);
        Assert.Equal(6, snapshot.Segments.Count);
        Assert.Equal(10, snapshot.Domains.Count);

        // Phase 1 dynamic configuration (plan §3.1/§3.2): each segment's DomainCodes now mirrors
        // its catalog matrix (SegmentDefinition.AllowedDomainCodes), not "all 10 for everyone".
        foreach (var segment in snapshot.Segments)
        {
            var expectedCount = SectorConfigurationCatalog.Segments
                .Single(s => s.Code == segment.Code).AllowedDomainCodes.Count;
            Assert.Equal(expectedCount, segment.DomainCodes.Count);
        }
    }

    /// <summary>
    /// DomainCodes_projette_la_matrice_du_catalogue (plan §3.5): proves the projection is exactly
    /// the catalog's <c>AllowedDomainCodes</c> (as a set), for every segment.
    /// </summary>
    [Fact]
    public void DomainCodes_projette_la_matrice_du_catalogue()
    {
        var provider = new StaticSectorCatalogProvider();
        var snapshot = provider.GetSnapshot();

        foreach (var segmentDef in SectorConfigurationCatalog.Segments)
        {
            var segmentSnapshot = snapshot.Segments.Single(s => s.Code == segmentDef.Code);
            Assert.Equal(
                segmentDef.AllowedDomainCodes.OrderBy(c => c, StringComparer.Ordinal),
                segmentSnapshot.DomainCodes.OrderBy(c => c, StringComparer.Ordinal));
            Assert.Contains(BusinessDomains.Autre, segmentSnapshot.DomainCodes);
        }
    }

    [Fact]
    public void Static_snapshot_Resolve_matches_SectorConfigurationCatalog_Resolve_for_all_66_combos()
    {
        var provider = new StaticSectorCatalogProvider();
        var snapshot = provider.GetSnapshot();

        var segmentCodes = CompanySegments.All;
        var domainCodes = BusinessDomains.All.Append((string?)null);

        foreach (var segmentCode in segmentCodes)
        {
            foreach (var domainCode in domainCodes)
            {
                var expected = SectorConfigurationCatalog.Resolve(segmentCode, domainCode);
                var actual = snapshot.Resolve(segmentCode, domainCode);

                Assert.Equal(expected?.SegmentCode, actual?.SegmentCode);
                Assert.Equal(expected?.DomainCode, actual?.DomainCode);
                Assert.Equal(expected?.DefaultWarehouseName, actual?.DefaultWarehouseName);
                Assert.Equal(expected?.RecommendedModules ?? Array.Empty<AppModule>(), actual?.RecommendedModules ?? Array.Empty<AppModule>());
                Assert.Equal(expected?.OptionalModules ?? Array.Empty<AppModule>(), actual?.OptionalModules ?? Array.Empty<AppModule>());
            }
        }
    }
}
