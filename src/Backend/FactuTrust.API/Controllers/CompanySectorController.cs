using System.Security.Claims;
using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Plan §2.3 — self-service sector (segment/domain) re-configuration for the current tenant, on top
/// of the existing platform-admin-only <see cref="ITenantSectorReconfigurationService"/> (plan
/// §WP-B7). Same preview/apply engine — this controller just maps the tenant-scoped, French,
/// simplified request/response shapes and adds a self-service-only 1-change/24h rate limit (the
/// platform-admin flow is deliberately NOT rate-limited the same way, since an operator may need to
/// fix several tenants back-to-back).
/// </summary>
[ApiController]
[Route("api/company/sector")]
[Authorize]
public sealed class CompanySectorController : ControllerBase
{
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromHours(24);

    private readonly ITenantContext _tenantContext;
    private readonly MasterDbContext _masterDb;
    private readonly ISectorCatalogProvider _catalogProvider;
    private readonly ITenantSectorReconfigurationService _reconfigurationService;
    private readonly ITenantDbContextFactory _tenantDbContextFactory;
    private readonly ILogger<CompanySectorController> _logger;

    public CompanySectorController(
        ITenantContext tenantContext,
        MasterDbContext masterDb,
        ISectorCatalogProvider catalogProvider,
        ITenantSectorReconfigurationService reconfigurationService,
        ITenantDbContextFactory tenantDbContextFactory,
        ILogger<CompanySectorController> logger)
    {
        _tenantContext = tenantContext;
        _masterDb = masterDb;
        _catalogProvider = catalogProvider;
        _reconfigurationService = reconfigurationService;
        _tenantDbContextFactory = tenantDbContextFactory;
        _logger = logger;
    }

    private bool TryGetActorId(out Guid actorId) => Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out actorId);

    /// <summary>Current classification plus the full catalog of segments/domains the tenant may switch to.</summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.SettingsRead)]
    [ProducesResponseType(typeof(ApiResponse<CompanySectorDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(ApiResponse<CompanySectorDto>.Fail("Contexte tenant introuvable."));

        var tenant = await _masterDb.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId.Value, cancellationToken);
        if (tenant is null)
            return NotFound(ApiResponse<CompanySectorDto>.Fail("Société introuvable."));

        var snapshot = _catalogProvider.GetSnapshot();

        return Ok(ApiResponse<CompanySectorDto>.Ok(new CompanySectorDto
        {
            CompanySegment = tenant.CompanySegment,
            BusinessDomain = tenant.BusinessDomain,
            AvailableSegments = snapshot.Segments
                .OrderBy(s => s.SortOrder)
                .Select(s => new CompanySectorCatalogEntryDto { Code = s.Code, LabelFr = s.LabelFr })
                .ToList(),
            AvailableDomains = snapshot.Domains
                .OrderBy(d => d.SortOrder)
                .Select(d => new CompanySectorCatalogEntryDto { Code = d.Code, LabelFr = d.LabelFr })
                .ToList()
        }));
    }

    /// <summary>Side-effect-free preview of the requested segment/domain change.</summary>
    [HttpPost("preview")]
    [Authorize(Policy = PermissionPolicies.SettingsUpdate)]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    [ProducesResponseType(typeof(ApiResponse<CompanySectorPreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Preview([FromBody] CompanySectorChangeRequestDto dto, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(ApiResponse<CompanySectorPreviewDto>.Fail("Contexte tenant introuvable."));

        TryGetActorId(out var actorId);

        var request = new SectorReconfigurationRequestDto
        {
            CompanySegment = dto.CompanySegment,
            BusinessDomain = dto.BusinessDomain,
            RecomputeModuleGrants = true,
            ApplyDataTemplates = true,
            UserIds = null
        };

        var result = await _reconfigurationService.PreviewAsync(tenantId.Value, request, actorId, cancellationToken);
        if (result.IsFailure)
            return MapFailure<CompanySectorPreviewDto>(result.Error);

        return Ok(ApiResponse<CompanySectorPreviewDto>.Ok(MapPreview(result.Value)));
    }

    /// <summary>
    /// Applies the requested segment/domain change: recomputes module grants for every active user,
    /// applies matching sector data templates additively, and audits the change. Rate-limited to one
    /// change per rolling 24h window per tenant (429) — platform admins are not subject to this
    /// limit (see <c>PlatformTenantSectorConfigurationController</c>). Plan §2.3: requires the
    /// <c>SettingsUpdate</c> permission AND the <c>Administrateur</c> role, so a non-admin with the
    /// permission cannot trigger tenant-wide sector re-provisioning.
    /// </summary>
    [HttpPut]
    [Authorize(Policy = PermissionPolicies.SettingsUpdate)]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    [ProducesResponseType(typeof(ApiResponse<CompanySectorApplyResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Update([FromBody] CompanySectorChangeRequestDto dto, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(ApiResponse<CompanySectorApplyResultDto>.Fail("Contexte tenant introuvable."));

        if (!TryGetActorId(out var actorId))
            return Unauthorized(ApiResponse<CompanySectorApplyResultDto>.Fail("Non authentifié."));

        var retryAfter = await ResolveRateLimitRetryAfterAsync(cancellationToken);
        if (retryAfter is not null)
        {
            Response.Headers.RetryAfter = ((int)retryAfter.Value.TotalSeconds).ToString();
            return StatusCode(StatusCodes.Status429TooManyRequests, ApiResponse<CompanySectorApplyResultDto>.Fail(
                "Une seule modification de la configuration sectorielle est autorisée par 24 heures. Veuillez réessayer plus tard."));
        }

        var request = new SectorReconfigurationRequestDto
        {
            CompanySegment = dto.CompanySegment,
            BusinessDomain = dto.BusinessDomain,
            RecomputeModuleGrants = true,
            ApplyDataTemplates = true,
            UserIds = null
        };

        var result = await _reconfigurationService.ApplyAsync(tenantId.Value, request, actorId, cancellationToken);
        if (result.IsFailure)
            return MapFailure<CompanySectorApplyResultDto>(result.Error);

        var effectiveChange = result.Value.EffectiveChange;
        var enabledModuleIds = effectiveChange.Users
            .SelectMany(u => u.TargetEnabledModuleIds)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        var stepWarnings = result.Value.Steps
            .Where(s => !s.Success)
            .Select(s => $"Étape « {s.Step} » en échec : {s.Error}")
            .ToList();
        var warnings = effectiveChange.Warnings.Concat(stepWarnings).ToList();

        _logger.LogInformation(
            "CompanySectorController.Update: tenant {TenantId} actor {ActorId} segment={Segment} domain={Domain}",
            tenantId.Value, actorId, effectiveChange.CurrentSegment, effectiveChange.CurrentDomain);

        return Ok(ApiResponse<CompanySectorApplyResultDto>.Ok(new CompanySectorApplyResultDto
        {
            CompanySegment = effectiveChange.CurrentSegment,
            BusinessDomain = effectiveChange.CurrentDomain,
            EnabledModuleIds = enabledModuleIds,
            Warnings = warnings
        }, "Configuration sectorielle mise à jour."));
    }

    private static CompanySectorPreviewDto MapPreview(SectorReconfigurationPreviewDto preview)
    {
        var modulesToEnable = preview.Users
            .SelectMany(u => u.ModulesToEnable)
            .Distinct()
            .OrderBy(id => id)
            .Where(id => Enum.IsDefined(typeof(AppModule), id))
            .Select(id => new CompanySectorModulePreviewDto { Id = id, LabelFr = ((AppModule)id).ToDisplayString() })
            .ToList();

        var templates = preview.Templates
            .Where(t => !t.AlreadyApplied)
            .Select(t => t.Code)
            .ToList();

        return new CompanySectorPreviewDto
        {
            ModulesToEnable = modulesToEnable,
            Templates = templates,
            Warnings = preview.Warnings
        };
    }

    /// <summary>
    /// Maps a service <see cref="Error"/> to the plan's HTTP contract:
    /// <see cref="ITenantSectorReconfigurationService.TenantNotFoundCode"/> ⇒ 404; any other
    /// validation error (unknown/unlinked segment/domain) ⇒ 400.
    /// </summary>
    private IActionResult MapFailure<T>(Error error)
    {
        if (string.Equals(error.Code, ITenantSectorReconfigurationService.TenantNotFoundCode, StringComparison.Ordinal))
            return NotFound(ApiResponse<T>.Fail(error.Description, error.Code));
        return BadRequest(ApiResponse<T>.Fail(error.Description, error.Code));
    }

    /// <summary>
    /// Returns the remaining wait time if the tenant applied a sector reconfiguration within the
    /// last <see cref="RateLimitWindow"/> (via the tenant DB's hash-chained <c>AuditLogs</c>,
    /// filtered on <see cref="ITenantSectorReconfigurationService.AuditAction"/>), or null when a new
    /// change is allowed right now. Fails open (returns null) if the tenant DB can't be reached —
    /// consistent with the underlying service's own fault-isolated audit step, which never blocks
    /// the apply itself on an audit failure.
    /// </summary>
    private async Task<TimeSpan?> ResolveRateLimitRetryAfterAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var tenantDb = _tenantDbContextFactory.CreateContext();
            var lastChangeAt = await tenantDb.AuditLogs
                .AsNoTracking()
                .Where(a => a.Action == ITenantSectorReconfigurationService.AuditAction)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => a.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastChangeAt == default)
                return null;

            var elapsed = DateTime.UtcNow - lastChangeAt;
            return elapsed < RateLimitWindow ? RateLimitWindow - elapsed : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CompanySectorController: could not evaluate the sector-reconfiguration rate limit — failing open.");
            return null;
        }
    }
}
