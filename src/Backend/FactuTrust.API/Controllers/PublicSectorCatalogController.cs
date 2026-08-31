using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Anonymous public API exposing the sector configuration catalog (plan §3 C1/C8, §6.1 B6) that
/// drives Step 1/3 of the registration wizard. Phase 1: purely static data. Phase 2 (plan
/// §WP-B2/§WP-B4): served from <see cref="ISectorCatalogProvider"/> — static catalog or master-DB
/// rule tables, depending on <c>Features:RegistrationSector:UseDbRules</c>.
/// </summary>
[ApiController]
[Route("api/public/sector-catalog")]
[AllowAnonymous]
public sealed class PublicSectorCatalogController : ControllerBase
{
    private readonly RegistrationSectorOptions _options;
    private readonly ISectorCatalogProvider _catalogProvider;

    public PublicSectorCatalogController(
        IOptions<RegistrationSectorOptions> options,
        ISectorCatalogProvider catalogProvider)
    {
        _options = options.Value;
        _catalogProvider = catalogProvider;
    }

    [HttpGet]
    [EnableRateLimiting("public-catalog")]
    [ProducesResponseType(typeof(ApiResponse<SectorCatalogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Get()
    {
        if (!_options.Enabled)
            return NotFound();

        var snapshot = _catalogProvider.GetSnapshot();

        // Phase 2 (plan §WP-B4): static source keeps the original 1h HTTP cache; DB-backed
        // snapshots use a 5 min cache so backoffice edits reach anonymous clients quickly without
        // increasing origin load beyond the existing public-catalog rate limiter.
        var maxAgeSeconds = snapshot.Source == SectorRuleSource.Db ? 300 : 3600;
        if (HttpContext is not null)
            Response.Headers.CacheControl = $"public,max-age={maxAgeSeconds}";

        return Ok(ApiResponse<SectorCatalogDto>.Ok(BuildCatalog(snapshot)));
    }

    internal static SectorCatalogDto BuildCatalog(SectorRuleSnapshot snapshot)
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

        return new SectorCatalogDto
        {
            Segments = segments,
            Domains = domains,
            Modules = modules,
            ModuleDependencies = moduleDependencies
        };
    }
}
