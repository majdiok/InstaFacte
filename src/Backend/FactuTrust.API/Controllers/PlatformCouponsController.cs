using System.Security.Claims;
using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>Lot C3 — CRUD coupons côté admin.</summary>
[ApiController]
[Route("api/platform/coupons")]
[Authorize(Policy = PlatformPolicies.PlatformAdmin)]
public sealed class PlatformCouponsController : ControllerBase
{
    private readonly ICouponAdminService _service;
    private readonly ILogger<PlatformCouponsController> _logger;

    public PlatformCouponsController(ICouponAdminService service, ILogger<PlatformCouponsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "perm:" + PlatformPermissions.CouponsManage)]
    [ProducesResponseType(typeof(ApiResponse<CouponsPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool? activeOnly = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var dto = await _service.ListAsync(activeOnly, search, page, pageSize, cancellationToken);
        return Ok(ApiResponse<CouponsPageDto>.Ok(dto));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.CouponsManage)]
    [ProducesResponseType(typeof(ApiResponse<CouponDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<CouponDto>.Fail(result.Error.Description, result.Error.Code));
        return Ok(ApiResponse<CouponDto>.Ok(result.Value));
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + PlatformPermissions.CouponsManage)]
    [ProducesResponseType(typeof(ApiResponse<CouponDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCouponRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<CouponDto>.Fail("Requête invalide."));

        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<CouponDto>.Fail("Non authentifié."));

        var result = await _service.CreateAsync(request, actorId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<CouponDto>.Fail(result.Error.Description, result.Error.Code));

        _logger.LogInformation("Platform admin {ActorId} created coupon {Code}", actorId, result.Value.Code);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id },
            ApiResponse<CouponDto>.Ok(result.Value, "Coupon créé."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.CouponsManage)]
    [ProducesResponseType(typeof(ApiResponse<CouponDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCouponRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<CouponDto>.Fail("Requête invalide."));

        var result = await _service.UpdateAsync(id, request, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
                ? NotFound(ApiResponse<CouponDto>.Fail(result.Error.Description, result.Error.Code))
                : BadRequest(ApiResponse<CouponDto>.Fail(result.Error.Description, result.Error.Code));
        }
        return Ok(ApiResponse<CouponDto>.Ok(result.Value, "Coupon mis à jour."));
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = "perm:" + PlatformPermissions.CouponsManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.DeactivateAsync(id, cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));
        return Ok(ApiResponse<object>.Ok(null!, "Coupon désactivé."));
    }

    [HttpPost("{id:guid}/reactivate")]
    [Authorize(Policy = "perm:" + PlatformPermissions.CouponsManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.ReactivateAsync(id, cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));
        return Ok(ApiResponse<object>.Ok(null!, "Coupon réactivé."));
    }

    [HttpGet("{id:guid}/redemptions")]
    [Authorize(Policy = "perm:" + PlatformPermissions.CouponsManage)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CouponRedemptionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListRedemptions(Guid id, CancellationToken cancellationToken)
    {
        var list = await _service.ListRedemptionsAsync(id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CouponRedemptionDto>>.Ok(list));
    }
}
