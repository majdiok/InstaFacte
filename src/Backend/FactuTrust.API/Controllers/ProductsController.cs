using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Products.Commands;
using FactuTrust.Application.Features.Products.Queries;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Controller for managing products and services.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController : ControllerBase
{
    private const int MaxProductImageSizeBytes = 2 * 1024 * 1024; // 2 MB
    private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp"
    };

    private readonly IMediator _mediator;
    private readonly IProductRepository _productRepository;
    private readonly IProductImageSearchService _imageSearchService;
    private readonly IProductImageStorageService _imageStorageService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        IMediator mediator,
        IProductRepository productRepository,
        IProductImageSearchService imageSearchService,
        IProductImageStorageService imageStorageService,
        ITenantContext tenantContext,
        ILogger<ProductsController> logger)
    {
        _mediator = mediator;
        _productRepository = productRepository;
        _imageSearchService = imageSearchService;
        _imageStorageService = imageStorageService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Get paginated list of products with optional search and filters.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.ProductsRead)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ProductListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProducts(
        [FromQuery] string? search,
        [FromQuery] ProductType? type,
        [FromQuery] bool? isActive,
        [FromQuery] Guid? categoryId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? warehouseId = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetProductsQuery(search, type, isActive, categoryId, page, pageSize, warehouseId);
        var result = await _mediator.Send(query, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<PagedResult<ProductListDto>>.Fail(result.Error.Description));
        return Ok(ApiResponse<PagedResult<ProductListDto>>.Ok(result.Value));
    }

    /// <summary>
    /// Get product details by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.ProductsRead)]
    [ProducesResponseType(typeof(ApiResponse<ProductDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProduct(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetProductByIdQuery(id);
        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsFailure)
            return NotFound(ApiResponse<ProductDetailDto>.Fail(result.Error.Description));

        return Ok(ApiResponse<ProductDetailDto>.Ok(result.Value));
    }

    /// <summary>
    /// Recherche un article par code-barres, en correspondance EXACTE.
    ///
    /// Destiné au scan en caisse : un code qui ne correspond à aucun article renvoie 404,
    /// jamais un article approchant. Le scan interrogeait auparavant le code produit interne
    /// avec repli sur une correspondance partielle, et pouvait encaisser un autre article.
    /// </summary>
    [HttpGet("by-barcode/{barcode}")]
    [Authorize(Policy = PermissionPolicies.ProductsRead)]
    [ProducesResponseType(typeof(ApiResponse<ProductDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductByBarcode(string barcode, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetProductByBarcodeQuery(barcode), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Code == "NotFound"
                ? NotFound(ApiResponse<ProductDetailDto>.Fail("Aucun article ne correspond à ce code-barres"))
                : BadRequest(ApiResponse<ProductDetailDto>.Fail(result.Error.Description));
        }

        return Ok(ApiResponse<ProductDetailDto>.Ok(result.Value));
    }

    /// <summary>
    /// Create a new product.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.ProductsCreate)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProduct(
        [FromBody] CreateProductDto dto,
        CancellationToken cancellationToken)
    {
        var command = new CreateProductCommand(dto);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Conflict")
                return Conflict(ApiResponse<Guid>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        }

        _logger.LogInformation("Product created with ID {ProductId}", result.Value);

        return CreatedAtAction(
            nameof(GetProduct),
            new { id = result.Value },
            ApiResponse<Guid>.Ok(result.Value, "Produit créé avec succès"));
    }

    /// <summary>
    /// Update an existing product.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.ProductsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<ProductDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProduct(
        Guid id,
        [FromBody] UpdateProductDto dto,
        CancellationToken cancellationToken)
    {
        var command = new UpdateProductCommand(id, dto);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "NotFound" || result.Error.Code == "Product.NotFound")
                return NotFound(ApiResponse<ProductDetailDto>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<ProductDetailDto>.Fail(result.Error.Description));
        }

        return Ok(ApiResponse<ProductDetailDto>.Ok(result.Value, "Produit mis à jour."));
    }

    /// <summary>
    /// Delete a product. Fails if product is used in invoices.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.ProductsDelete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProduct(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeleteProductCommand(id);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "NotFound" || result.Error.Code == "Product.NotFound")
                return NotFound(ApiResponse<object>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }

        return Ok(ApiResponse<object>.Ok(null!, "Produit supprimé."));
    }

    /// <summary>
    /// Toggle product active status.
    /// </summary>
    [HttpPatch("{id:guid}/toggle-active")]
    [Authorize(Policy = PermissionPolicies.ProductsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<ProductDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleActive(Guid id, CancellationToken cancellationToken)
    {
        var command = new ToggleProductActiveCommand(id);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return NotFound(ApiResponse<ProductDetailDto>.Fail(result.Error.Description));

        return Ok(ApiResponse<ProductDetailDto>.Ok(result.Value,
            result.Value.IsActive ? "Produit activé." : "Produit désactivé."));
    }

    /// <summary>
    /// Gets the product image URL. If the product has no image, searches external APIs (Unsplash, Google)
    /// and caches the result in the database.
    /// </summary>
    [HttpGet("{id:guid}/image-url")]
    [Authorize(Policy = PermissionPolicies.ProductsRead)]
    [ProducesResponseType(typeof(ApiResponse<string?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductImageUrl(Guid id, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        if (product is null)
            return NotFound(ApiResponse<string?>.Fail("Produit introuvable."));

        if (!string.IsNullOrEmpty(product.ImageUrl))
            return Ok(ApiResponse<string?>.Ok(product.ImageUrl));

        var searchQuery = string.IsNullOrEmpty(product.Category?.Name)
            ? product.Name
            : $"{product.Name} {product.Category.Name}";
        var imageUrl = await _imageSearchService.SearchImageUrlAsync(searchQuery, cancellationToken);

        if (!string.IsNullOrEmpty(imageUrl))
        {
            try
            {
                await _productRepository.UpdateImageUrlAsync(id, imageUrl, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save image URL for product {ProductId}", id);
            }
        }

        return Ok(ApiResponse<string?>.Ok(imageUrl));
    }

    /// <summary>
    /// Uploads a product image. Replaces any existing image.
    /// Allowed: JPEG, PNG, WebP; max 2 MB.
    /// </summary>
    [HttpPost("{id:guid}/image")]
    [Authorize(Policy = PermissionPolicies.ProductsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> UploadProductImage(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.TenantId is null)
            return BadRequest(ApiResponse<string>.Fail("Contexte tenant manquant."));

        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        if (product is null)
            return NotFound(ApiResponse<string>.Fail("Produit introuvable."));

        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<string>.Fail("Aucun fichier fourni."));

        if (file.Length > MaxProductImageSizeBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge,
                ApiResponse<string>.Fail("L'image ne doit pas dépasser 2 Mo."));

        var contentType = file.ContentType ?? string.Empty;
        if (!AllowedImageContentTypes.Contains(contentType.Trim()))
            return BadRequest(ApiResponse<string>.Fail("Type de fichier non autorisé. Utilisez JPEG, PNG ou WebP."));

        try
        {
            await using var stream = file.OpenReadStream();
            var relativeUrl = await _imageStorageService.SaveAsync(
                _tenantContext.TenantId.Value,
                id,
                stream,
                contentType,
                cancellationToken);

            await _productRepository.UpdateImageUrlAsync(id, relativeUrl, cancellationToken);

            var absoluteUrl = $"{Request.Scheme}://{Request.Host}{relativeUrl}";
            return Ok(ApiResponse<string>.Ok(absoluteUrl, "Image enregistrée."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Removes the product image.
    /// </summary>
    [HttpDelete("{id:guid}/image")]
    [Authorize(Policy = PermissionPolicies.ProductsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProductImage(Guid id, CancellationToken cancellationToken = default)
    {
        if (_tenantContext.TenantId is null)
            return BadRequest(ApiResponse<object>.Fail("Contexte tenant manquant."));

        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        if (product is null)
            return NotFound(ApiResponse<object>.Fail("Produit introuvable."));

        await _imageStorageService.DeleteAsync(_tenantContext.TenantId.Value, id, cancellationToken);
        await _productRepository.ClearImageUrlAsync(id, cancellationToken);

        return Ok(ApiResponse<object>.Ok(null!, "Image supprimée."));
    }
}
