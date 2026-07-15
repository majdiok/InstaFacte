using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.ProductCategories.Commands;
using FactuTrust.Application.Features.ProductCategories.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Controller for product categories (dropdown, CRUD).
/// </summary>
[ApiController]
[Route("api/product-categories")]
[Authorize]
public class ProductCategoriesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ProductCategoriesController> _logger;

    public ProductCategoriesController(IMediator mediator, ILogger<ProductCategoriesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get product categories for dropdown (active only by default).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.ProductsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductCategorySelectDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProductCategories(
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var query = new GetProductCategoriesQuery(activeOnly);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ProductCategorySelectDto>>.Ok(result));
    }

    /// <summary>
    /// Get full list of product categories for admin (list page).
    /// </summary>
    [HttpGet("list")]
    [Authorize(Policy = PermissionPolicies.ProductsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductCategoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProductCategoriesList(CancellationToken cancellationToken = default)
    {
        var query = new GetProductCategoriesListQuery();
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ProductCategoryDto>>.Ok(result));
    }

    /// <summary>
    /// Get product category by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.ProductsRead)]
    [ProducesResponseType(typeof(ApiResponse<ProductCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductCategory(Guid id, CancellationToken cancellationToken = default)
    {
        var query = new GetProductCategoryByIdQuery(id);
        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsFailure)
            return NotFound(ApiResponse<ProductCategoryDto>.Fail(result.Error.Description));

        return Ok(ApiResponse<ProductCategoryDto>.Ok(result.Value));
    }

    /// <summary>
    /// Create a new product category.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.ProductsCreate)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProductCategory(
        [FromBody] CreateProductCategoryDto dto,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateProductCategoryCommand(dto);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Conflict")
                return Conflict(ApiResponse<Guid>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        }

        _logger.LogInformation("Product category created with ID {CategoryId}", result.Value);

        return CreatedAtAction(
            nameof(GetProductCategory),
            new { id = result.Value },
            ApiResponse<Guid>.Ok(result.Value, "Catégorie créée avec succès"));
    }

    /// <summary>
    /// Update an existing product category.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.ProductsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<ProductCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProductCategory(
        Guid id,
        [FromBody] UpdateProductCategoryDto dto,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateProductCategoryCommand(id, dto);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "NotFound" || result.Error.Code == "ProductCategory.NotFound")
                return NotFound(ApiResponse<ProductCategoryDto>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<ProductCategoryDto>.Fail(result.Error.Description));
        }

        return Ok(ApiResponse<ProductCategoryDto>.Ok(result.Value, "Catégorie mise à jour."));
    }
}
