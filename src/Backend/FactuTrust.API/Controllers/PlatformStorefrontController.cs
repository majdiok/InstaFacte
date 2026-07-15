using FactuTrust.API.Authorization;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Storefront.DTOs;
using FactuTrust.Application.Features.Storefront.Platform.Commands;
using FactuTrust.Application.Features.Storefront.Platform.Queries;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/platform/storefronts")]
[Authorize(Policy = PlatformPolicies.PlatformAdmin)]
public sealed class PlatformStorefrontController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly StorefrontOptions _options;

    public PlatformStorefrontController(IMediator mediator, IOptions<StorefrontOptions> options)
    {
        _mediator = mediator;
        _options = options.Value;
    }

    [HttpGet("pending")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StorefrontProfileDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPending(CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var result = await _mediator.Send(new ListPendingStorefrontsQuery(), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));

        return Ok(ApiResponse<IReadOnlyList<StorefrontProfileDto>>.Ok(result.Value));
    }

    /// <summary>
    /// Lot A4 — Liste les vitrines par statut métier.
    /// `filter` accepte : `pending` | `published` | `rejected` | `suspended` | `draft`.
    /// </summary>
    [HttpGet("by-status")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StorefrontProfileDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListByStatus([FromQuery] string filter, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var result = await _mediator.Send(new ListStorefrontsByStatusQuery(filter), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));

        return Ok(ApiResponse<IReadOnlyList<StorefrontProfileDto>>.Ok(result.Value));
    }

    /// <summary>Lot A4 — KPIs agrégés (Pending / Published / Rejected / Suspended).</summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<StorefrontStatsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var result = await _mediator.Send(new GetStorefrontStatsQuery(), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));

        return Ok(ApiResponse<StorefrontStatsDto>.Ok(result.Value));
    }

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse<StorefrontProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var result = await _mediator.Send(new ApproveStorefrontCommand(id), cancellationToken);
        if (result.IsFailure)
            return MapFailure(result.Error);

        return Ok(ApiResponse<StorefrontProfileDto>.Ok(result.Value, "Vitrine publiée."));
    }

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse<StorefrontProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectStorefrontBody body, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var result = await _mediator.Send(new RejectStorefrontCommand(id, body.Reason), cancellationToken);
        if (result.IsFailure)
            return MapFailure(result.Error);

        return Ok(ApiResponse<StorefrontProfileDto>.Ok(result.Value, "Vitrine renvoyée en brouillon."));
    }

    private void EnsureEnabled()
    {
        if (!_options.Enabled)
            throw new BadHttpRequestException("La fonctionnalité Rue virtuelle est désactivée.", StatusCodes.Status403Forbidden);
    }

    private IActionResult MapFailure(Error error) =>
        error.Code.Contains(".NotFound", StringComparison.Ordinal)
            ? NotFound(ApiResponse<object>.Fail(error.Description, error.Code))
            : BadRequest(ApiResponse<object>.Fail(error.Description, error.Code));
}

public sealed record RejectStorefrontBody
{
    public string Reason { get; init; } = null!;
}
