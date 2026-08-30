using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.SectorConfiguration;

/// <summary>
/// Sector-aware registration wizard — declarative catalog tests (plan §6.1 B1, §8).
/// </summary>
public sealed class SectorConfigurationCatalogTests
{
    [Fact]
    public void Catalog_has_exactly_6_segments_and_10_domains()
    {
        Assert.Equal(6, FactuTrust.Domain.SectorConfiguration.SectorConfigurationCatalog.Segments.Count);
        Assert.Equal(10, FactuTrust.Domain.SectorConfiguration.SectorConfigurationCatalog.Domains.Count);
    }

    [Theory]
    [InlineData(CompanySegments.Entreprise)]
    [InlineData(CompanySegments.Commerce)]
    [InlineData(CompanySegments.Services)]
    [InlineData(CompanySegments.BtpConstruction)]
    [InlineData(CompanySegments.Association)]
    [InlineData(CompanySegments.EtablissementEducatif)]
    public void Resolve_returns_a_profile_for_every_known_segment(string segment)
    {
        var profile = FactuTrust.Domain.SectorConfiguration.SectorConfigurationCatalog.Resolve(segment, null);

        Assert.NotNull(profile);
        Assert.Equal(segment, profile!.SegmentCode);
        Assert.Null(profile.DomainCode);
    }

    [Fact]
    public void Resolve_merges_domain_overlay_on_top_of_segment_base()
    {
        var withoutDomain = FactuTrust.Domain.SectorConfiguration.SectorConfigurationCatalog.Resolve(CompanySegments.Entreprise, null)!;
        var withDomain = FactuTrust.Domain.SectorConfiguration.SectorConfigurationCatalog.Resolve(
            CompanySegments.Entreprise, BusinessDomains.TechnologieInformatique)!;

        Assert.Equal(BusinessDomains.TechnologieInformatique, withDomain.DomainCode);
        Assert.Contains(AppModule.Projects, withDomain.RecommendedModules);
        Assert.Contains(AppModule.RecurringContracts, withDomain.RecommendedModules);
        Assert.DoesNotContain(AppModule.Projects, withoutDomain.RecommendedModules);

        // Segment base modules survive the overlay merge.
        foreach (var module in withoutDomain.RecommendedModules)
            Assert.Contains(module, withDomain.RecommendedModules);
    }

    [Theory]
    [InlineData(" ENTREPRISE ")]
    [InlineData("Entreprise")]
    [InlineData("entreprise")]
    public void Resolve_is_case_and_whitespace_insensitive(string rawSegment)
    {
        var profile = FactuTrust.Domain.SectorConfiguration.SectorConfigurationCatalog.Resolve(rawSegment, null);

        Assert.NotNull(profile);
        Assert.Equal(CompanySegments.Entreprise, profile!.SegmentCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-real-segment")]
    public void Resolve_returns_null_for_null_blank_or_unknown_segment(string? segment)
    {
        Assert.Null(FactuTrust.Domain.SectorConfiguration.SectorConfigurationCatalog.Resolve(segment, null));
    }

    [Fact]
    public void Resolve_treats_unknown_domain_as_absent_domain()
    {
        var withUnknownDomain = FactuTrust.Domain.SectorConfiguration.SectorConfigurationCatalog.Resolve(
            CompanySegments.Entreprise, "not-a-real-domain");
        var withNoDomain = FactuTrust.Domain.SectorConfiguration.SectorConfigurationCatalog.Resolve(CompanySegments.Entreprise, null);

        Assert.NotNull(withUnknownDomain);
        Assert.Null(withUnknownDomain!.DomainCode);
        Assert.Equal(withNoDomain!.RecommendedModules, withUnknownDomain.RecommendedModules);
    }

    public static IEnumerable<object[]> AllSegmentDomainCombinations()
    {
        foreach (var segment in CompanySegments.All)
        {
            yield return new object[] { segment, null! };
            foreach (var domain in BusinessDomains.All)
                yield return new object[] { segment, domain };
        }
    }

    [Theory]
    [MemberData(nameof(AllSegmentDomainCombinations))]
    public void Every_profile_contains_all_core_modules_and_never_Honoraires(string segment, string? domain)
    {
        var profile = FactuTrust.Domain.SectorConfiguration.SectorConfigurationCatalog.Resolve(segment, domain);
        Assert.NotNull(profile);

        foreach (var coreModule in FactuTrust.Domain.SectorConfiguration.SectorConfigurationCatalog.CoreModules)
            Assert.Contains(coreModule, profile!.CoreModules);

        Assert.DoesNotContain(AppModule.Honoraires, profile!.CoreModules);
        Assert.DoesNotContain(AppModule.Honoraires, profile.RecommendedModules);
        Assert.DoesNotContain(AppModule.Honoraires, profile.OptionalModules);
    }

    [Theory]
    [MemberData(nameof(AllSegmentDomainCombinations))]
    public void Core_recommended_and_optional_modules_are_disjoint_and_cover_every_non_Honoraires_module(
        string segment, string? domain)
    {
        var profile = FactuTrust.Domain.SectorConfiguration.SectorConfigurationCatalog.Resolve(segment, domain)!;

        var core = new HashSet<AppModule>(profile.CoreModules);
        var recommended = new HashSet<AppModule>(profile.RecommendedModules);
        var optional = new HashSet<AppModule>(profile.OptionalModules);

        Assert.Empty(core.Intersect(recommended));
        Assert.Empty(core.Intersect(optional));
        Assert.Empty(recommended.Intersect(optional));

        var union = core.Union(recommended).Union(optional).ToHashSet();
        var expected = AppModuleExtensions.AllValues.Where(m => m != AppModule.Honoraires).ToHashSet();
        Assert.Equal(expected, union);
    }
}
