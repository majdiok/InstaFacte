using FactuTrust.API.Authorization;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Storefront.DTOs;
using FactuTrust.Application.Features.Storefront.TenantAdmin.Commands;
using FactuTrust.Application.Features.Storefront.TenantAdmin.Queries;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Tenant-facing API for managing the public 3D storefront of the authenticated company.
/// All endpoints require the <see cref="PermissionPolicies.StorefrontTenantOwner"/> policy,
/// which is granted only to <c>Administrator</c> and <c>Supervisor</c> roles.
/// </summary>
/// <remarks>
/// The platform-side moderation API (approve/reject/suspend) lives separately in
/// <c>PlatformStorefrontController</c> (exposed through the backoffice only).
/// </remarks>
[ApiController]
[Route("api/storefront/tenant")]
[Authorize(Policy = PermissionPolicies.StorefrontTenantOwner)]
public sealed class StorefrontTenantController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<StorefrontTenantController> _logger;
    private readonly StorefrontOptions _options;

    public StorefrontTenantController(
        IMediator mediator,
        ILogger<StorefrontTenantController> logger,
        IOptions<StorefrontOptions> options)
    {
        _mediator = mediator;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Returns the current storefront profile for the authenticated tenant, or <c>null</c>
    /// if the tenant has not yet opted in.
    /// </summary>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(ApiResponse<StorefrontProfileDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        EnsureFeatureEnabled();

        var result = await _mediator.Send(new GetStorefrontProfileQuery(), cancellationToken);
        if (result.IsFailure)
            return MapFailure<StorefrontProfileDto?>(result.Error);

        return Ok(ApiResponse<StorefrontProfileDto?>.Ok(result.Value));
    }

    /// <summary>
    /// Creates the tenant's storefront profile (opt-in). The storefront is created in
    /// <c>Draft</c> status and requires further configuration before submission for review.
    /// </summary>
    [HttpPost("profile")]
    [ProducesResponseType(typeof(ApiResponse<StorefrontProfileDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> OptIn(
        [FromBody] OptInStorefrontRequest request,
        CancellationToken cancellationToken)
    {
        EnsureFeatureEnabled();

        var result = await _mediator.Send(new OptInStorefrontCommand(request), cancellationToken);
        if (result.IsFailure)
            return MapFailure<StorefrontProfileDto>(result.Error);

        _logger.LogInformation(
            "Storefront opt-in created for tenant {TenantId} with slug {Slug}",
            result.Value.TenantId,
            result.Value.Slug);

        return CreatedAtAction(
            nameof(GetProfile),
            null,
            ApiResponse<StorefrontProfileDto>.Ok(result.Value, "Vitrine publique créée en brouillon."));
    }

    /// <summary>
    /// Updates the tenant's storefront profile (branding, description, public contacts).
    /// </summary>
    [HttpPut("profile")]
    [ProducesResponseType(typeof(ApiResponse<StorefrontProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateStorefrontProfileRequest request,
        CancellationToken cancellationToken)
    {
        EnsureFeatureEnabled();

        var result = await _mediator.Send(new UpdateStorefrontProfileCommand(request), cancellationToken);
        if (result.IsFailure)
            return MapFailure<StorefrontProfileDto>(result.Error);

        return Ok(ApiResponse<StorefrontProfileDto>.Ok(result.Value, "Vitrine mise à jour."));
    }

    /// <summary>
    /// Submits the storefront for platform moderation (transitions <c>Draft</c> → <c>PendingReview</c>).
    /// </summary>
    [HttpPost("submit-for-review")]
    [ProducesResponseType(typeof(ApiResponse<StorefrontProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitForReview(CancellationToken cancellationToken)
    {
        EnsureFeatureEnabled();

        var result = await _mediator.Send(new SubmitStorefrontForReviewCommand(), cancellationToken);
        if (result.IsFailure)
            return MapFailure<StorefrontProfileDto>(result.Error);

        _logger.LogInformation(
            "Storefront {StorefrontId} submitted for review by tenant {TenantId}",
            result.Value.Id, result.Value.TenantId);

        return Ok(ApiResponse<StorefrontProfileDto>.Ok(result.Value,
            "Vitrine soumise à la modération. Vous serez notifié après validation."));
    }

    /// <summary>
    /// Unpublishes the tenant's storefront (transitions <c>Published</c> → <c>Draft</c>).
    /// </summary>
    [HttpPost("unpublish")]
    [ProducesResponseType(typeof(ApiResponse<StorefrontProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unpublish(CancellationToken cancellationToken)
    {
        EnsureFeatureEnabled();

        var result = await _mediator.Send(new UnpublishStorefrontCommand(), cancellationToken);
        if (result.IsFailure)
            return MapFailure<StorefrontProfileDto>(result.Error);

        _logger.LogInformation(
            "Storefront {StorefrontId} unpublished by tenant {TenantId}",
            result.Value.Id, result.Value.TenantId);

        return Ok(ApiResponse<StorefrontProfileDto>.Ok(result.Value, "Vitrine dépubliée."));
    }

    /// <summary>
    /// Lists the tenant's active products with their public-visibility flag.
    /// </summary>
    [HttpGet("products")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<StorefrontEligibleProductDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEligibleProducts(
        [FromQuery] string? search,
        [FromQuery] bool? onlyPublic,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        EnsureFeatureEnabled();

        var query = new GetStorefrontEligibleProductsQuery(search, onlyPublic, page, pageSize);
        var result = await _mediator.Send(query, cancellationToken);
        if (result.IsFailure)
            return MapFailure<PagedResult<StorefrontEligibleProductDto>>(result.Error);

        return Ok(ApiResponse<PagedResult<StorefrontEligibleProductDto>>.Ok(result.Value));
    }

    /// <summary>
    /// Toggles the public visibility of a product on the tenant's storefront.
    /// Returns <c>true</c> when the value actually changed (idempotent).
    /// </summary>
    [HttpPatch("products/{productId:guid}/visibility")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetProductVisibility(
        Guid productId,
        [FromBody] SetProductPublicVisibilityRequest request,
        CancellationToken cancellationToken)
    {
        EnsureFeatureEnabled();

        var result = await _mediator.Send(
            new SetProductPublicVisibilityCommand(productId, request.IsPubliclyListed),
            cancellationToken);
        if (result.IsFailure)
            return MapFailure<bool>(result.Error);

        return Ok(ApiResponse<bool>.Ok(result.Value,
            result.Value ? "Visibilité mise à jour." : "Aucun changement nécessaire."));
    }

    private void EnsureFeatureEnabled()
    {
        if (!_options.Enabled)
            throw new Microsoft.AspNetCore.Http.BadHttpRequestException(
                "La fonctionnalité Rue virtuelle est désactivée.", StatusCodes.Status403Forbidden);
    }

    private IActionResult MapFailure<T>(Error error)
    {
        return error.Code switch
        {
            "Unauthorized" => Unauthorized(ApiResponse<T>.Fail(error.Description, error.Code)),
            "Forbidden" => StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<T>.Fail(error.Description, error.Code)),
            "Conflict" => Conflict(ApiResponse<T>.Fail(error.Description, error.Code)),
            var c when c.EndsWith(".NotFound", StringComparison.Ordinal) =>
                NotFound(ApiResponse<T>.Fail(error.Description, error.Code)),
            _ => BadRequest(ApiResponse<T>.Fail(error.Description, error.Code))
        };
    }
}

/// <summary>
/// Payload for <see cref="StorefrontTenantController.SetProductVisibility"/>.
/// </summary>
public sealed record SetProductPublicVisibilityRequest
{
    public bool IsPubliclyListed { get; init; }
}
