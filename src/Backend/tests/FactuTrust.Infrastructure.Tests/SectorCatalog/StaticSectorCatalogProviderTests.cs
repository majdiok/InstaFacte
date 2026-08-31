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

        foreach (var segment in snapshot.Segments)
            Assert.Equal(10, segment.DomainCodes.Count);
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
