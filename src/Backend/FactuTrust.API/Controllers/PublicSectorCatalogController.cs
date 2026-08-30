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
/// drives Step 1/3 of the registration wizard. Purely static data — no DB, no parameters.
/// </summary>
[ApiController]
[Route("api/public/sector-catalog")]
[AllowAnonymous]
public sealed class PublicSectorCatalogController : ControllerBase
{
    private readonly RegistrationSectorOptions _options;

    public PublicSectorCatalogController(IOptions<RegistrationSectorOptions> options)
    {
        _options = options.Value;
    }

    [HttpGet]
    [EnableRateLimiting("public-catalog")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    [ProducesResponseType(typeof(ApiResponse<SectorCatalogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Get()
    {
        if (!_options.Enabled)
            return NotFound();

        return Ok(ApiResponse<SectorCatalogDto>.Ok(BuildCatalog()));
    }

    internal static SectorCatalogDto BuildCatalog()
    {
        var coreModuleIds = SectorConfigurationCatalog.CoreModules.Select(m => (int)m).ToList();
        var coreModuleSet = new HashSet<AppModule>(SectorConfigurationCatalog.CoreModules);

        var segments = SectorConfigurationCatalog.Segments
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
                DefaultWarehouseName = s.DefaultWarehouseName
            })
            .ToList();

        var domains = SectorConfigurationCatalog.Domains
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

        return new SectorCatalogDto
        {
            Segments = segments,
            Domains = domains,
            Modules = modules
        };
    }
}
