using System.Security.Claims;
using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Phase 2 — backoffice admin CRUD over the master-DB sector-rule tables (plan §WP-B5).
///
/// Routes: <c>api/platform/sector-rules[/segments|/domains|/segment-domains|/module-rules|
/// /module-dependencies|/settings|/templates]</c>, plus <c>/seed-from-catalog</c> (super admin
/// only) and <c>/parity</c> (read-only, plan §WP-B9).
///
/// Entry gate: <see cref="PlatformPolicies.PlatformAdmin"/>. Fine-grained per-action policies:
/// <see cref="PlatformPermissions.SectorRulesRead"/> for reads, <see cref="PlatformPermissions.SectorRulesManage"/>
/// for writes/deletes, <see cref="PlatformPolicies.SuperAdminOnly"/> for the catalog reseed.
/// Deletes are always soft (never a hard delete) — <c>DELETE</c> deactivates and returns 200.
/// </summary>
[ApiController]
[Route("api/platform/sector-rules")]
[Authorize(Policy = PlatformPolicies.PlatformAdmin)]
public sealed class PlatformSectorRulesController : ControllerBase
{
    private readonly ISectorRuleAdminService _service;
    private readonly ILogger<PlatformSectorRulesController> _logger;

    public PlatformSectorRulesController(ISectorRuleAdminService service, ILogger<PlatformSectorRulesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private bool TryGetActorId(out Guid actorId) => Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out actorId);

    // ---------- Full set / seed ----------

    [HttpGet]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesRead)]
    [ProducesResponseType(typeof(ApiResponse<SectorRuleSetAdminDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFullSet(CancellationToken cancellationToken)
    {
        var dto = await _service.GetFullSetAsync(cancellationToken);
        return Ok(ApiResponse<SectorRuleSetAdminDto>.Ok(dto));
    }

    [HttpPost("seed-from-catalog")]
    [Authorize(Policy = PlatformPolicies.SuperAdminOnly)]
    [ProducesResponseType(typeof(ApiResponse<SectorRuleSeedResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SeedFromCatalog([FromQuery] bool force, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized(ApiResponse<SectorRuleSeedResultDto>.Fail("Non authentifié."));

        var result = await _service.SeedFromCatalogAsync(force, actorId.ToString(), cancellationToken);

        _logger.LogInformation(
            "Platform super-admin {ActorId} seeded sector rules from catalog (force={Force}, inserted={Inserted}, updated={Updated}, version={Version})",
            actorId, force, result.Inserted, result.Updated, result.NewVersion);

        var dto = new SectorRuleSeedResultDto
        {
            Inserted = result.Inserted,
            Updated = result.Updated,
            SkippedExisting = result.SkippedExisting,
            NewVersion = result.NewVersion,
            Forced = result.Forced
        };
        return Ok(ApiResponse<SectorRuleSeedResultDto>.Ok(dto, "Règles sectorielles synchronisées depuis le catalogue."));
    }

    // ---------- Segments ----------

    [HttpGet("segments")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesRead)]
    public async Task<IActionResult> ListSegments(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<SectorSegmentAdminDto>>.Ok(await _service.ListSegmentsAsync(cancellationToken)));

    [HttpGet("segments/{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesRead)]
    public async Task<IActionResult> GetSegment(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetSegmentAsync(id, cancellationToken);
        return result.IsFailure
            ? NotFound(ApiResponse<SectorSegmentAdminDto>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<SectorSegmentAdminDto>.Ok(result.Value));
    }

    [HttpPost("segments")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesManage)]
    public async Task<IActionResult> CreateSegment([FromBody] CreateSectorSegmentRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized(ApiResponse<SectorSegmentAdminDto>.Fail("Non authentifié."));

        var result = await _service.CreateSegmentAsync(request, actorId.ToString(), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<SectorSegmentAdminDto>.Fail(result.Error.Description, result.Error.Code));

        return CreatedAtAction(nameof(GetSegment), new { id = result.Value.Id },
            ApiResponse<SectorSegmentAdminDto>.Ok(result.Value, "Segment créé."));
    }

    [HttpPut("segments/{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesManage)]
    public async Task<IActionResult> UpdateSegment(Guid id, [FromBody] UpdateSectorSegmentRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized(ApiResponse<SectorSegmentAdminDto>.Fail("Non authentifié."));

        var result = await _service.UpdateSegmentAsync(id, request, actorId.ToString(), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<SectorSegmentAdminDto>.Fail(result.Error.Description, result.Error.Code));

        return Ok(ApiResponse<SectorSegmentAdminDto>.Ok(result.Value, "Segment mis à jour."));
    }

    [HttpDelete("segments/{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesManage)]
    public async Task<IActionResult> DeactivateSegment(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized(ApiResponse<object>.Fail("Non authentifié."));

        var result = await _service.DeactivateSegmentAsync(id, actorId.ToString(), cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));

        return Ok(ApiResponse<object>.Ok(null!, "Segment désactivé."));
    }

    // ---------- Domains ----------

    [HttpGet("domains")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesRead)]
    public async Task<IActionResult> ListDomains(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<SectorDomainAdminDto>>.Ok(await _service.ListDomainsAsync(cancellationToken)));

    [HttpGet("domains/{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesRead)]
    public async Task<IActionResult> GetDomain(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetDomainAsync(id, cancellationToken);
        return result.IsFailure
            ? NotFound(ApiResponse<SectorDomainAdminDto>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<SectorDomainAdminDto>.Ok(result.Value));
    }

    [HttpPost("domains")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesManage)]
    public async Task<IActionResult> CreateDomain([FromBody] CreateSectorDomainRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized(ApiResponse<SectorDomainAdminDto>.Fail("Non authentifié."));

        var result = await _service.CreateDomainAsync(request, actorId.ToString(), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<SectorDomainAdminDto>.Fail(result.Error.Description, result.Error.Code));

        return CreatedAtAction(nameof(GetDomain), new { id = result.Value.Id },
            ApiResponse<SectorDomainAdminDto>.Ok(result.Value, "Domaine créé."));
    }

    [HttpPut("domains/{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesManage)]
    public async Task<IActionResult> UpdateDomain(Guid id, [FromBody] UpdateSectorDomainRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized(ApiResponse<SectorDomainAdminDto>.Fail("Non authentifié."));

        var result = await _service.UpdateDomainAsync(id, request, actorId.ToString(), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<SectorDomainAdminDto>.Fail(result.Error.Description, result.Error.Code));

        return Ok(ApiResponse<SectorDomainAdminDto>.Ok(result.Value, "Domaine mis à jour."));
    }

    [HttpDelete("domains/{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesManage)]
    public async Task<IActionResult> DeactivateDomain(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized(ApiResponse<object>.Fail("Non authentifié."));

        var result = await _service.DeactivateDomainAsync(id, actorId.ToString(), cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));

        return Ok(ApiResponse<object>.Ok(null!, "Domaine désactivé."));
    }

    // ---------- Segment-domains ----------

    [HttpGet("segment-domains")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesRead)]
    public async Task<IActionResult> ListSegmentDomains(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<SectorSegmentDomainAdminDto>>.Ok(await _service.ListSegmentDomainsAsync(cancellationToken)));

    [HttpGet("segment-domains/{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesRead)]
    public async Task<IActionResult> GetSegmentDomain(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetSegmentDomainAsync(id, cancellationToken);
        return result.IsFailure
            ? NotFound(ApiResponse<SectorSegmentDomainAdminDto>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<SectorSegmentDomainAdminDto>.Ok(result.Value));
    }

    [HttpPost("segment-domains")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesManage)]
    public async Task<IActionResult> CreateSegmentDomain([FromBody] CreateSectorSegmentDomainRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized(ApiResponse<SectorSegmentDomainAdminDto>.Fail("Non authentifié."));

        var result = await _service.CreateSegmentDomainAsync(request, actorId.ToString(), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<SectorSegmentDomainAdminDto>.Fail(result.Error.Description, result.Error.Code));

        return CreatedAtAction(nameof(GetSegmentDomain), new { id = result.Value.Id },
            ApiResponse<SectorSegmentDomainAdminDto>.Ok(result.Value, "Association créée."));
    }

    [HttpPut("segment-domains/{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesManage)]
    public async Task<IActionResult> UpdateSegmentDomain(Guid id, [FromBody] UpdateSectorSegmentDomainRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized(ApiResponse<SectorSegmentDomainAdminDto>.Fail("Non authentifié."));

        var result = await _service.UpdateSegmentDomainAsync(id, request, actorId.ToString(), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<SectorSegmentDomainAdminDto>.Fail(result.Error.Description, result.Error.Code));

        return Ok(ApiResponse<SectorSegmentDomainAdminDto>.Ok(result.Value, "Association mise à jour."));
    }

    [HttpDelete("segment-domains/{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesManage)]
    public async Task<IActionResult> DeactivateSegmentDomain(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized(ApiResponse<object>.Fail("Non authentifié."));

        var result = await _service.DeactivateSegmentDomainAsync(id, actorId.ToString(), cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));

        return Ok(ApiResponse<object>.Ok(null!, "Association désactivée."));
    }

    // ---------- Module rules ----------

    [HttpGet("module-rules")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesRead)]
    public async Task<IActionResult> ListModuleRules(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<SectorModuleRuleAdminDto>>.Ok(await _service.ListModuleRulesAsync(cancellationToken)));

    [HttpGet("module-rules/{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesRead)]
    public async Task<IActionResult> GetModuleRule(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetModuleRuleAsync(id, cancellationToken);
        return result.IsFailure
            ? NotFound(ApiResponse<SectorModuleRuleAdminDto>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<SectorModuleRuleAdminDto>.Ok(result.Value));
    }

    [HttpPost("module-rules")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesManage)]
    public async Task<IActionResult> CreateModuleRule([FromBody] CreateSectorModuleRuleRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized(ApiResponse<SectorModuleRuleAdminDto>.Fail("Non authentifié."));

        var result = await _service.CreateModuleRuleAsync(request, actorId.ToString(), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<SectorModuleRuleAdminDto>.Fail(result.Error.Description, result.Error.Code));

        return CreatedAtAction(nameof(GetModuleRule), new { id = result.Value.Id },
            ApiResponse<SectorModuleRuleAdminDto>.Ok(result.Value, "Règle de module créée."));
    }

    [HttpPut("module-rules/{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesManage)]
    public async Task<IActionResult> UpdateModuleRule(Guid id, [FromBody] UpdateSectorModuleRuleRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized(ApiResponse<SectorModuleRuleAdminDto>.Fail("Non authentifié."));

        var result = await _service.UpdateModuleRuleAsync(id, request, actorId.ToString(), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<SectorModuleRuleAdminDto>.Fail(result.Error.Description, result.Error.Code));

        return Ok(ApiResponse<SectorModuleRuleAdminDto>.Ok(result.Value, "Règle de module mise à jour."));
    }

    [HttpDelete("module-rules/{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesManage)]
    public async Task<IActionResult> DeactivateModuleRule(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized(ApiResponse<object>.Fail("Non authentifié."));

        var result = await _service.DeactivateModuleRuleAsync(id, actorId.ToString(), cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));

        return Ok(ApiResponse<object>.Ok(null!, "Règle de module désactivée."));
    }

    // ---------- Module dependencies ----------

    [HttpGet("module-dependencies")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesRead)]
    public async Task<IActionResult> ListModuleDependencies(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<SectorModuleDependencyAdminDto>>.Ok(await _service.ListModuleDependenciesAsync(cancellationToken)));

    [HttpGet("module-dependencies/{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesRead)]
    public async Task<IActionResult> GetModuleDependency(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetModuleDependencyAsync(id, cancellationToken);
        return result.IsFailure
            ? NotFound(ApiResponse<SectorModuleDependencyAdminDto>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<SectorModuleDependencyAdminDto>.Ok(result.Value));
    }

    [HttpPost("module-dependencies")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesManage)]
    public async Task<IActionResult> CreateModuleDependency([FromBody] CreateSectorModuleDependencyRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized(ApiResponse<SectorModuleDependencyAdminDto>.Fail("Non authentifié."));

        var result = await _service.CreateModuleDependencyAsync(request, actorId.ToString(), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<SectorModuleDependencyAdminDto>.Fail(result.Error.Description, result.Error.Code));

        return CreatedAtAction(nameof(GetModuleDependency), new { id = result.Value.Id },
            ApiResponse<SectorModuleDependencyAdminDto>.Ok(result.Value, "Dépendance créée."));
    }

    [HttpDelete("module-dependencies/{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesManage)]
    public async Task<IActionResult> DeactivateModuleDependency(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized(ApiResponse<object>.Fail("Non authentifié."));

        var result = await _service.DeactivateModuleDependencyAsync(id, actorId.ToString(), cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));

        return Ok(ApiResponse<object>.Ok(null!, "Dépendance désactivée."));
    }

    // ---------- Default settings ----------

    [HttpGet("settings")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesRead)]
    public async Task<IActionResult> ListSettings(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<SectorDefaultSettingAdminDto>>.Ok(await _service.ListSettingsAsync(cancellationToken)));

    [HttpGet("settings/{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesRead)]
    public async Task<IActionResult> GetSetting(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetSettingAsync(id, cancellationToken);
        return result.IsFailure
            ? NotFound(ApiResponse<SectorDefaultSettingAdminDto>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<SectorDefaultSettingAdminDto>.Ok(result.Value));
    }

    [HttpPost("settings")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesManage)]
    public async Task<IActionResult> CreateSetting([FromBody] CreateSectorDefaultSettingRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized(ApiResponse<SectorDefaultSettingAdminDto>.Fail("Non authentifié."));

        var result = await _service.CreateSettingAsync(request, actorId.ToString(), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<SectorDefaultSettingAdminDto>.Fail(result.Error.Description, result.Error.Code));

        return CreatedAtAction(nameof(GetSetting), new { id = result.Value.Id },
            ApiResponse<SectorDefaultSettingAdminDto>.Ok(result.Value, "Paramètre créé."));
    }

    [HttpPut("settings/{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesManage)]
    public async Task<IActionResult> UpdateSetting(Guid id, [FromBody] UpdateSectorDefaultSettingRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized(ApiResponse<SectorDefaultSettingAdminDto>.Fail("Non authentifié."));

        var result = await _service.UpdateSettingAsync(id, request, actorId.ToString(), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<SectorDefaultSettingAdminDto>.Fail(result.Error.Description, result.Error.Code));

        return Ok(ApiResponse<SectorDefaultSettingAdminDto>.Ok(result.Value, "Paramètre mis à jour."));
    }

    [HttpDelete("settings/{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesManage)]
    public async Task<IActionResult> DeactivateSetting(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized(ApiResponse<object>.Fail("Non authentifié."));

        var result = await _service.DeactivateSettingAsync(id, actorId.ToString(), cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));

        return Ok(ApiResponse<object>.Ok(null!, "Paramètre désactivé."));
    }

    // ---------- Data templates ----------

    [HttpGet("templates")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesRead)]
    public async Task<IActionResult> ListTemplates(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<SectorDataTemplateAdminDto>>.Ok(await _service.ListTemplatesAsync(cancellationToken)));

    [HttpGet("templates/{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesRead)]
    public async Task<IActionResult> GetTemplate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetTemplateAsync(id, cancellationToken);
        return result.IsFailure
            ? NotFound(ApiResponse<SectorDataTemplateAdminDto>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<SectorDataTemplateAdminDto>.Ok(result.Value));
    }

    [HttpPost("templates")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesManage)]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateSectorDataTemplateRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized(ApiResponse<SectorDataTemplateAdminDto>.Fail("Non authentifié."));

        var result = await _service.CreateTemplateAsync(request, actorId.ToString(), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<SectorDataTemplateAdminDto>.Fail(result.Error.Description, result.Error.Code));

        return CreatedAtAction(nameof(GetTemplate), new { id = result.Value.Id },
            ApiResponse<SectorDataTemplateAdminDto>.Ok(result.Value, "Modèle créé."));
    }

    [HttpPut("templates/{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesManage)]
    public async Task<IActionResult> UpdateTemplate(Guid id, [FromBody] UpdateSectorDataTemplateRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized(ApiResponse<SectorDataTemplateAdminDto>.Fail("Non authentifié."));

        var result = await _service.UpdateTemplateAsync(id, request, actorId.ToString(), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<SectorDataTemplateAdminDto>.Fail(result.Error.Description, result.Error.Code));

        return Ok(ApiResponse<SectorDataTemplateAdminDto>.Ok(result.Value, "Modèle mis à jour."));
    }

    [HttpDelete("templates/{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SectorRulesManage)]
    public async Task<IActionResult> DeactivateTemplate(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized(ApiResponse<object>.Fail("Non authentifié."));

        var result = await _service.DeactivateTemplateAsync(id, actorId.ToString(), cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));

        return Ok(ApiResponse<object>.Ok(null!, "Modèle désactivé."));
    }
}
