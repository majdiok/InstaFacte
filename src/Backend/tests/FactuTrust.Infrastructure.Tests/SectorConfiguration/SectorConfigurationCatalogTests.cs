using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using Xunit;
using Catalog = FactuTrust.Domain.SectorConfiguration.SectorConfigurationCatalog;

namespace FactuTrust.Infrastructure.Tests.SectorConfiguration;

/// <summary>
/// Sector-aware registration wizard — declarative catalog tests (plan §6.1 B1, §8).
/// </summary>
public sealed class SectorConfigurationCatalogTests
{
    [Fact]
    public void Catalog_has_exactly_6_segments_and_10_domains()
    {
        Assert.Equal(6, Catalog.Segments.Count);
        Assert.Equal(10, Catalog.Domains.Count);
    }

    /// <summary>
    /// Chaque_segment_a_une_liste_de_domaines_valides_et_contient_autre (plan §3.5): every
    /// segment's <c>AllowedDomainCodes</c> is non-empty, contains no duplicates, and always
    /// includes the universal "autre" safety-net fallback.
    /// </summary>
    [Fact]
    public void Chaque_segment_a_une_liste_de_domaines_valides_et_contient_autre()
    {
        foreach (var segment in Catalog.Segments)
        {
            Assert.NotEmpty(segment.AllowedDomainCodes);
            Assert.Equal(segment.AllowedDomainCodes.Count, segment.AllowedDomainCodes.Distinct().Count());
            Assert.Contains(BusinessDomains.Autre, segment.AllowedDomainCodes);
        }
    }

    /// <summary>
    /// La_matrice_ne_reference_que_des_codes_de_domaines_connus (plan §3.5): every code in every
    /// segment's <c>AllowedDomainCodes</c> resolves to a real <see cref="BusinessDomains"/> code.
    /// </summary>
    [Fact]
    public void La_matrice_ne_reference_que_des_codes_de_domaines_connus()
    {
        foreach (var segment in Catalog.Segments)
        {
            foreach (var domainCode in segment.AllowedDomainCodes)
                Assert.True(BusinessDomains.IsKnown(domainCode), $"'{domainCode}' n'est pas un code de domaine connu.");
        }
    }

    /// <summary>
    /// Canonical French labels must match the design mockup
    /// (/code/.plans/designs/type-societe-reference.html) exactly, including the '&amp;'
    /// separators (not '/') and the 'Autre domaine' fallback label — coordinated with the
    /// frontend catalog (registration-catalog.ts) so both sides render identical text.
    /// </summary>
    [Fact]
    public void Segment_and_domain_labels_match_the_design_mockup_canon()
    {
        var segmentLabels = Catalog.Segments.ToDictionary(s => s.Code, s => s.LabelFr);
        Assert.Equal("Entreprise", segmentLabels[CompanySegments.Entreprise]);
        Assert.Equal("Commerce", segmentLabels[CompanySegments.Commerce]);
        Assert.Equal("Prestations de services", segmentLabels[CompanySegments.Services]);
        Assert.Equal("BTP & Construction", segmentLabels[CompanySegments.BtpConstruction]);
        Assert.Equal("Association", segmentLabels[CompanySegments.Association]);
        Assert.Equal("Établissement éducatif", segmentLabels[CompanySegments.EtablissementEducatif]);

        var domainLabels = Catalog.Domains.ToDictionary(d => d.Code, d => d.LabelFr);
        Assert.Equal("Technologie & Informatique", domainLabels[BusinessDomains.TechnologieInformatique]);
        Assert.Equal("Alimentation & Agroalimentaire", domainLabels[BusinessDomains.AlimentationAgroalimentaire]);
        Assert.Equal("Santé & Paramédical", domainLabels[BusinessDomains.SanteParamedical]);
        Assert.Equal("Textile & Habillement", domainLabels[BusinessDomains.TextileHabillement]);
        Assert.Equal("Transport & Logistique", domainLabels[BusinessDomains.TransportLogistique]);
        Assert.Equal("Immobilier", domainLabels[BusinessDomains.Immobilier]);
        Assert.Equal("Énergie & Environnement", domainLabels[BusinessDomains.EnergieEnvironnement]);
        Assert.Equal("Communication & Marketing", domainLabels[BusinessDomains.CommunicationMarketing]);
        Assert.Equal("Artisanat", domainLabels[BusinessDomains.Artisanat]);
        Assert.Equal("Autre domaine", domainLabels[BusinessDomains.Autre]);

        Assert.All(Catalog.Segments, s => Assert.DoesNotContain('/', s.LabelFr));
        Assert.All(Catalog.Domains, d => Assert.DoesNotContain('/', d.LabelFr));
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
        var profile = Catalog.Resolve(segment, null);

        Assert.NotNull(profile);
        Assert.Equal(segment, profile!.SegmentCode);
        Assert.Null(profile.DomainCode);
    }

    [Fact]
    public void Resolve_merges_domain_overlay_on_top_of_segment_base()
    {
        var withoutDomain = Catalog.Resolve(CompanySegments.Entreprise, null)!;
        var withDomain = Catalog.Resolve(
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
        var profile = Catalog.Resolve(rawSegment, null);

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
        Assert.Null(Catalog.Resolve(segment, null));
    }

    [Fact]
    public void Resolve_treats_unknown_domain_as_absent_domain()
    {
        var withUnknownDomain = Catalog.Resolve(
            CompanySegments.Entreprise, "not-a-real-domain");
        var withNoDomain = Catalog.Resolve(CompanySegments.Entreprise, null);

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
        var profile = Catalog.Resolve(segment, domain);
        Assert.NotNull(profile);

        foreach (var coreModule in Catalog.CoreModules)
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
        var profile = Catalog.Resolve(segment, domain)!;

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
