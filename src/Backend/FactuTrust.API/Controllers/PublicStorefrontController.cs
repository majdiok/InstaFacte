using System.Security.Cryptography;
using System.Text;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Storefront.Public;
using FactuTrust.Application.Features.Storefront.Public.Commands;
using FactuTrust.Application.Features.Storefront.Public.Queries;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Anonymous public API backing the 3D virtual street. Reads are served from the Master projection only.
/// </summary>
[ApiController]
[Route("api/public/street")]
[AllowAnonymous]
public sealed class PublicStorefrontController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly StorefrontOptions _options;
    private readonly ILogger<PublicStorefrontController> _logger;

    public PublicStorefrontController(
        IMediator mediator,
        IOptions<StorefrontOptions> options,
        ILogger<PublicStorefrontController> logger)
    {
        _mediator = mediator;
        _options = options.Value;
        _logger = logger;
    }

    [HttpGet("map")]
    [EnableRateLimiting("public-street-read")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PublicStreetMapEntryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMap(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return NotFound();

        var result = await _mediator.Send(new GetStreetMapQuery(), cancellationToken);
        if (result.IsFailure)
            return MapFailure(result.Error);

        return Ok(ApiResponse<IReadOnlyList<PublicStreetMapEntryDto>>.Ok(result.Value));
    }

    [HttpGet("storefronts/{slug}")]
    [EnableRateLimiting("public-street-read")]
    [ProducesResponseType(typeof(ApiResponse<PublicStorefrontDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStorefront(string slug, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return NotFound();

        var result = await _mediator.Send(new GetPublicStorefrontBySlugQuery(slug), cancellationToken);
        if (result.IsFailure)
            return MapFailure(result.Error);

        if (result.Value is null)
        {
            // Correlation only (no raw slug in logs — avoids leaking guessable storefront URLs in aggregates).
            _logger.LogDebug("Public storefront GET miss (slug hash {SlugHash})", SlugCorrelationHash(slug));
            return NotFound(ApiResponse<PublicStorefrontDetailDto>.Fail("Vitrine introuvable.", "NotFound"));
        }

        return Ok(ApiResponse<PublicStorefrontDetailDto>.Ok(result.Value));
    }

    [HttpGet("storefronts/{slug}/products")]
    [EnableRateLimiting("public-street-read")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PublicStorefrontProductDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProducts(
        string slug,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        [FromQuery] string? category = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return NotFound();

        var result = await _mediator.Send(
            new GetPublicStorefrontProductsQuery(slug, page, pageSize, category),
            cancellationToken);

        if (result.IsFailure)
            return MapFailure(result.Error);

        return Ok(ApiResponse<PagedResult<PublicStorefrontProductDto>>.Ok(result.Value));
    }

    [HttpGet("storefronts/{slug}/categories")]
    [EnableRateLimiting("public-street-read")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PublicStorefrontCategoryCountDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(string slug, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return NotFound();

        var result = await _mediator.Send(new GetPublicStorefrontCategoriesQuery(slug), cancellationToken);
        if (result.IsFailure)
            return MapFailure(result.Error);

        return Ok(ApiResponse<IReadOnlyList<PublicStorefrontCategoryCountDto>>.Ok(result.Value));
    }

    [HttpPost("orders")]
    [EnableRateLimiting("public-street-write")]
    [ProducesResponseType(typeof(ApiResponse<SubmitPublicStorefrontOrderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitOrder(
        [FromBody] SubmitPublicStorefrontOrderRequest body,
        [FromHeader(Name = "X-Turnstile-Token")] string? turnstileToken,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return NotFound();

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = HttpContext.Request.Headers.UserAgent.ToString();
        var result = await _mediator.Send(
            new SubmitPublicStorefrontOrderCommand(body, turnstileToken, ip, ua),
            cancellationToken);

        if (result.IsFailure)
            return MapFailure(result.Error);

        return Ok(ApiResponse<SubmitPublicStorefrontOrderResponse>.Ok(result.Value, "Commande enregistrée."));
    }

    private IActionResult MapFailure(Error error) =>
        error.Code switch
        {
            "Forbidden" => StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(error.Description, error.Code)),
            var c when c.StartsWith("Validation.", StringComparison.Ordinal) =>
                BadRequest(ApiResponse<object>.Fail(error.Description, error.Code)),
            _ => BadRequest(ApiResponse<object>.Fail(error.Description, error.Code))
        };

    private static string SlugCorrelationHash(string slug)
    {
        var normalized = (slug ?? string.Empty).Trim().ToLowerInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes.AsSpan(0, 8));
    }
}
