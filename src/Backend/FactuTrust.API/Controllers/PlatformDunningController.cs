using System.Security.Claims;
using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Lot C6 — CRUD campagnes dunning + lecture des états + renouvellement / extension grace.
/// </summary>
[ApiController]
[Route("api/platform/dunning")]
[Authorize(Policy = PlatformPolicies.PlatformAdmin)]
public sealed class PlatformDunningController : ControllerBase
{
    private readonly IDunningCampaignService _campaignService;
    private readonly IDunningStateQueryService _stateQueryService;
    private readonly ISubscriptionRenewalService _renewalService;
    private readonly ILogger<PlatformDunningController> _logger;

    public PlatformDunningController(
        IDunningCampaignService campaignService,
        IDunningStateQueryService stateQueryService,
        ISubscriptionRenewalService renewalService,
        ILogger<PlatformDunningController> logger)
    {
        _campaignService = campaignService;
        _stateQueryService = stateQueryService;
        _renewalService = renewalService;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Campaigns
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet("campaigns")]
    [Authorize(Policy = "perm:" + PlatformPermissions.InvoiceIssue)]
    [ProducesResponseType(typeof(ApiResponse<DunningCampaignsListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCampaigns(CancellationToken cancellationToken)
    {
        // Garantit la présence d'une campagne par défaut au premier appel.
        await _campaignService.EnsureDefaultExistsAsync(cancellationToken);
        var dto = await _campaignService.ListAsync(cancellationToken);
        return Ok(ApiResponse<DunningCampaignsListDto>.Ok(dto));
    }

    [HttpGet("campaigns/{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.InvoiceIssue)]
    [ProducesResponseType(typeof(ApiResponse<DunningCampaignDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCampaign(Guid id, CancellationToken cancellationToken)
    {
        var result = await _campaignService.GetAsync(id, cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<DunningCampaignDto>.Fail(result.Error.Description, result.Error.Code));
        return Ok(ApiResponse<DunningCampaignDto>.Ok(result.Value));
    }

    [HttpPost("campaigns")]
    [Authorize(Policy = "perm:" + PlatformPermissions.InvoiceIssue)]
    [ProducesResponseType(typeof(ApiResponse<DunningCampaignDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCampaign([FromBody] CreateDunningCampaignRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<DunningCampaignDto>.Fail("Requête invalide."));
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<DunningCampaignDto>.Fail("Non authentifié."));

        var result = await _campaignService.CreateAsync(request, actorId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<DunningCampaignDto>.Fail(result.Error.Description, result.Error.Code));
        _logger.LogInformation("Platform admin {ActorId} created dunning campaign {Name}", actorId, result.Value.Name);
        return CreatedAtAction(nameof(GetCampaign), new { id = result.Value.Id },
            ApiResponse<DunningCampaignDto>.Ok(result.Value, "Campagne créée."));
    }

    [HttpPut("campaigns/{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.InvoiceIssue)]
    [ProducesResponseType(typeof(ApiResponse<DunningCampaignDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCampaign(Guid id, [FromBody] UpdateDunningCampaignRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<DunningCampaignDto>.Fail("Requête invalide."));
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<DunningCampaignDto>.Fail("Non authentifié."));

        var result = await _campaignService.UpdateAsync(id, request, actorId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
                ? NotFound(ApiResponse<DunningCampaignDto>.Fail(result.Error.Description, result.Error.Code))
                : BadRequest(ApiResponse<DunningCampaignDto>.Fail(result.Error.Description, result.Error.Code));
        }
        return Ok(ApiResponse<DunningCampaignDto>.Ok(result.Value, "Campagne mise à jour."));
    }

    [HttpPost("campaigns/{id:guid}/activate")]
    [Authorize(Policy = "perm:" + PlatformPermissions.InvoiceIssue)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<object>.Fail("Non authentifié."));

        var result = await _campaignService.ActivateAsync(id, actorId, cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));
        return Ok(ApiResponse<object>.Ok(null!, "Campagne activée."));
    }

    [HttpPost("campaigns/{id:guid}/deactivate")]
    [Authorize(Policy = "perm:" + PlatformPermissions.InvoiceIssue)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<object>.Fail("Non authentifié."));

        var result = await _campaignService.DeactivateAsync(id, actorId, cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));
        return Ok(ApiResponse<object>.Ok(null!, "Campagne désactivée."));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // States (audit cycles dunning)
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet("states")]
    [Authorize(Policy = "perm:" + PlatformPermissions.InvoiceRead)]
    [ProducesResponseType(typeof(ApiResponse<DunningStatesPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListStates(
        [FromQuery] string? outcome = null,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var dto = await _stateQueryService.ListAsync(outcome, tenantId, page, pageSize, cancellationToken);
        return Ok(ApiResponse<DunningStatesPageDto>.Ok(dto));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Subscription renewal actions
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost("subscriptions/{subscriptionId:guid}/renew-now")]
    [Authorize(Policy = "perm:" + PlatformPermissions.InvoiceIssue)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RenewNow(Guid subscriptionId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<object>.Fail("Non authentifié."));

        var result = await _renewalService.RenewNowAsync(subscriptionId, actorId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
                ? NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
                : BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));
        }
        _logger.LogInformation("Platform admin {ActorId} manually renewed subscription {Id}", actorId, subscriptionId);
        return Ok(ApiResponse<object>.Ok(null!, "Abonnement renouvelé."));
    }

    [HttpPost("subscriptions/{subscriptionId:guid}/extend-grace")]
    [Authorize(Policy = "perm:" + PlatformPermissions.InvoiceIssue)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExtendGrace(Guid subscriptionId, [FromBody] ExtendGracePeriodRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Requête invalide."));
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<object>.Fail("Non authentifié."));

        var result = await _renewalService.ExtendGraceAsync(subscriptionId, request.Days, actorId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
                ? NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
                : BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));
        }
        return Ok(ApiResponse<object>.Ok(null!, $"Délai de grâce étendu de {request.Days} jour(s)."));
    }
}
