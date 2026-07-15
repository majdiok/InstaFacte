using System.Security.Claims;
using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Lot C5 — CRUD config providers de paiement + audit intents.
/// </summary>
[ApiController]
[Route("api/platform/payment-providers")]
[Authorize(Policy = PlatformPolicies.PlatformAdmin)]
public sealed class PlatformPaymentProvidersController : ControllerBase
{
    private readonly IPaymentProviderConfigService _configService;
    private readonly IPaymentIntentQueryService _intentQueryService;
    private readonly ILogger<PlatformPaymentProvidersController> _logger;

    public PlatformPaymentProvidersController(
        IPaymentProviderConfigService configService,
        IPaymentIntentQueryService intentQueryService,
        ILogger<PlatformPaymentProvidersController> logger)
    {
        _configService = configService;
        _intentQueryService = intentQueryService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "perm:" + PlatformPermissions.ProvidersConfigure)]
    [ProducesResponseType(typeof(ApiResponse<PaymentProviderConfigsListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var dto = await _configService.ListAsync(cancellationToken);
        return Ok(ApiResponse<PaymentProviderConfigsListDto>.Ok(dto));
    }

    [HttpGet("{providerCode}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.ProvidersConfigure)]
    [ProducesResponseType(typeof(ApiResponse<PaymentProviderConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string providerCode, CancellationToken cancellationToken)
    {
        var result = await _configService.GetAsync(providerCode, cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<PaymentProviderConfigDto>.Fail(result.Error.Description, result.Error.Code));
        return Ok(ApiResponse<PaymentProviderConfigDto>.Ok(result.Value));
    }

    [HttpPut("{providerCode}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.ProvidersConfigure)]
    [ProducesResponseType(typeof(ApiResponse<PaymentProviderConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string providerCode, [FromBody] UpdatePaymentProviderConfigRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<PaymentProviderConfigDto>.Fail("Requête invalide."));
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<PaymentProviderConfigDto>.Fail("Non authentifié."));

        var result = await _configService.UpdateAsync(providerCode, request, actorId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
                ? NotFound(ApiResponse<PaymentProviderConfigDto>.Fail(result.Error.Description, result.Error.Code))
                : BadRequest(ApiResponse<PaymentProviderConfigDto>.Fail(result.Error.Description, result.Error.Code));
        }

        _logger.LogInformation("Platform admin {ActorId} updated payment provider {Provider}", actorId, providerCode);
        return Ok(ApiResponse<PaymentProviderConfigDto>.Ok(result.Value, "Configuration mise à jour."));
    }

    [HttpGet("intents")]
    [Authorize(Policy = "perm:" + PlatformPermissions.ProvidersConfigure)]
    [ProducesResponseType(typeof(ApiResponse<PaymentIntentsPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListIntents(
        [FromQuery] string? providerCode = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var dto = await _intentQueryService.ListAsync(providerCode, status, tenantId, from, to, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PaymentIntentsPageDto>.Ok(dto));
    }
}
