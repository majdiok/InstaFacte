using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;

namespace FactuTrust.Application.DTOs;

/// <summary>
/// Maps a <see cref="SectorRuleSnapshot"/> (static or DB-backed) to the public
/// <see cref="SectorCatalogDto"/> contract (plan §2.1). Extracted out of
/// <c>PublicSectorCatalogController</c> (API assembly, previously <c>internal</c>) so it can also be
/// exercised by parity tests in <c>FactuTrust.Infrastructure.Tests</c>, which does not reference the
/// API project.
/// </summary>
public static class SectorCatalogDtoMapper
{
    public static SectorCatalogDto BuildCatalog(SectorRuleSnapshot snapshot)
    {
        var coreModuleIds = SectorConfigurationCatalog.CoreModules.Select(m => (int)m).ToList();
        var coreModuleSet = new HashSet<AppModule>(SectorConfigurationCatalog.CoreModules);

        var segments = snapshot.Segments
            .OrderBy(s => s.SortOrder)
            .Select(s => new SectorSegmentDto
            {
                Code = s.Code,
                LabelFr = s.LabelFr,
                DescriptionFr = s.DescriptionFr,
                IconKey = s.IconKey,
                SortOrder = s.SortOrder,
                CoreModuleIds = coreModuleIds,
                RecommendedModuleIds = s.BaseRecommendedModules.Select(m => (int)m).ToList(),
                DefaultWarehouseName = s.DefaultWarehouseName,
                DomainCodes = s.DomainCodes
            })
            .ToList();

        var domains = snapshot.Domains
            .OrderBy(d => d.SortOrder)
            .Select(d => new SectorDomainDto
            {
                Code = d.Code,
                LabelFr = d.LabelFr,
                SortOrder = d.SortOrder,
                AdditionalModuleIds = d.OverlayModules.Select(m => (int)m).ToList()
            })
            .ToList();

        var modules = AppModuleExtensions.AllValues
            .Where(m => m != AppModule.Honoraires)
            .Select(m => new SectorModuleDto
            {
                Id = (int)m,
                Code = m.ToString(),
                LabelFr = m.ToDisplayString(),
                IsCore = coreModuleSet.Contains(m)
            })
            .ToList();

        var moduleDependencies = snapshot.ModuleDependencies
            .Select(d => new SectorModuleDependencyDto { ModuleId = d.ModuleId, RequiredModuleId = d.RequiredModuleId })
            .ToList();

        var suggestedTaxRegimes = snapshot.TaxRegimeSuggestions
            .Select(s => new SectorTaxRegimeSuggestionDto
            {
                SegmentCode = s.SegmentCode,
                Regime = s.Regime,
                NoteFr = s.NoteFr
            })
            .ToList();

        return new SectorCatalogDto
        {
            Segments = segments,
            Domains = domains,
            Modules = modules,
            ModuleDependencies = moduleDependencies,
            SuggestedTaxRegimes = suggestedTaxRegimes,
            CatalogVersion = snapshot.CatalogVersionTag
        };
    }
}
