using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Stock.Queries;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Application.Features.Inventory.Commands;

/// <summary>
/// Démarre un nouvel inventaire physique.
/// L'utilisateur choisit entre inventaire complet ou partiel.
/// </summary>
public sealed record StartInventoryCommand(
    InventoryType Type,
    Guid? WarehouseId = null,
    List<Guid>? ProductIds = null,
    string? Notes = null) : IRequest<Result<StartInventoryResult>>;

/// <summary>
/// Résultat du démarrage d'un inventaire avec message pédagogique.
/// </summary>
public sealed record StartInventoryResult
{
    public Guid InventoryId { get; init; }
    public int TotalProducts { get; init; }
    public string HumanMessage { get; init; } = string.Empty;
    public IReadOnlyList<InventoryProductItem> Products { get; init; } = Array.Empty<InventoryProductItem>();
}

/// <summary>
/// Produit inclus dans l'inventaire avec sa quantité théorique.
/// </summary>
public sealed record InventoryProductItem
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string? ProductCode { get; init; }
    public decimal TheoreticalQuantity { get; init; }
    public bool IsCounted { get; init; }
    public decimal? CountedQuantity { get; init; }
    public Guid? ProductLotId { get; init; }
    public string? LotNumber { get; init; }
    public TrackingMode TrackingMode { get; init; }
    public bool HasExpiryTracking { get; init; }
}

public sealed class StartInventoryCommandValidator : AbstractValidator<StartInventoryCommand>
{
    public StartInventoryCommandValidator()
    {
        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Le type d'inventaire doit être Complet ou Partiel.");

        When(x => x.Type == InventoryType.Partial, () =>
        {
            RuleFor(x => x.ProductIds)
                .NotEmpty()
                .WithMessage("Veuillez sélectionner au moins un produit pour l'inventaire partiel.");
        });
    }
}

public sealed class StartInventoryCommandHandler : IRequestHandler<StartInventoryCommand, Result<StartInventoryResult>>
{
    private readonly IPhysicalInventoryRepository _inventoryRepository;
    private readonly IStockItemRepository _stockItemRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;
    private readonly ITenantContext _tenantContext;
    private readonly IDocumentNumberService _documentNumberService;
    private readonly IStockTraceabilityQuery _traceability;

    public StartInventoryCommandHandler(
        IPhysicalInventoryRepository inventoryRepository,
        IStockItemRepository stockItemRepository,
        IWarehouseRepository warehouseRepository,
        IProductRepository productRepository,
        ITenantContext tenantContext,
        IDocumentNumberService documentNumberService,
        IStockTraceabilityQuery traceability)
    {
        _inventoryRepository = inventoryRepository;
        _stockItemRepository = stockItemRepository;
        _warehouseRepository = warehouseRepository;
        _productRepository = productRepository;
        _tenantContext = tenantContext;
        _documentNumberService = documentNumberService;
        _traceability = traceability;
    }

    public async Task<Result<StartInventoryResult>> Handle(StartInventoryCommand request, CancellationToken cancellationToken)
    {
        if (_tenantContext.TenantId is null)
            return Result.Failure<StartInventoryResult>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        // Determine warehouse
        Guid warehouseId;
        if (request.WarehouseId.HasValue)
        {
            warehouseId = request.WarehouseId.Value;
        }
        else
        {
            var defaultWarehouse = await _warehouseRepository.GetDefaultAsync(cancellationToken);
            if (defaultWarehouse == null)
                return Result.Failure<StartInventoryResult>(Error.Validation("Warehouse", "Aucun entrepôt par défaut n'est configuré."));
            warehouseId = defaultWarehouse.Id;
        }

        // Check for existing active inventory
        if (await _inventoryRepository.HasActiveInventoryAsync(warehouseId, cancellationToken))
            return Result.Failure<StartInventoryResult>(Error.Conflict("Un inventaire est déjà en cours pour cet entrepôt. Veuillez le terminer ou l'annuler avant d'en démarrer un nouveau."));

        // Get products to inventory
        List<(Guid ProductId, string ProductName, string? ProductCode, decimal TheoreticalQuantity)> productsToInventory;

        if (request.Type == InventoryType.Complete)
        {
            var stockManagedProducts = await _productRepository.GetStockManagedProductsAsync(cancellationToken);
            if (!stockManagedProducts.Any())
                return Result.Failure<StartInventoryResult>(Error.Validation("Products", "Aucun produit avec gestion de stock activée dans le catalogue."));

            var stockItems = await _stockItemRepository.GetByWarehouseForInventoryAsync(warehouseId, cancellationToken);
            var quantityByProductId = stockItems
                .GroupBy(s => s.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(s => s.QuantityOnHand));

            productsToInventory = stockManagedProducts.Select(p =>
            {
                var qty = quantityByProductId.TryGetValue(p.Id, out var q) ? q : 0m;
                return (p.Id, p.Name, (string?)p.Code, qty);
            }).ToList();
        }
        else
        {
            // Partial inventory - only selected products
            if (request.ProductIds == null || !request.ProductIds.Any())
                return Result.Failure<StartInventoryResult>(Error.Validation("ProductIds", "Veuillez sélectionner au moins un produit."));

            var products = await GetProductsAsync(request.ProductIds, cancellationToken);
            productsToInventory = new List<(Guid, string, string?, decimal)>();

            foreach (var productId in request.ProductIds)
            {
                var product = products.FirstOrDefault(p => p.Id == productId);
                if (product == null)
                    return Result.Failure<StartInventoryResult>(Error.NotFound("Produit", productId));

                var stockItem = await _stockItemRepository.GetByProductAndWarehouseAsync(productId, warehouseId, cancellationToken);
                var quantity = stockItem?.QuantityOnHand ?? 0;
                
                productsToInventory.Add((productId, product.Name, product.Code, quantity));
            }
        }

        // Get next reference (atomic sequence)
        var fiscalYear = DateTime.UtcNow.Year;
        var docResult = await _documentNumberService.ReserveNextAsync(
            _tenantContext.TenantId!.Value,
            NumberingDocumentType.PhysicalInventory,
            fiscalYear,
            DateTime.UtcNow,
            cancellationToken);
        var reference = docResult.Value;
        var detailed = new List<(Guid ProductId, string ProductName, string? ProductCode, decimal TheoreticalQuantity, Guid? ProductLotId, string? LotNumber)>();
        foreach (var row in productsToInventory)
        {
            var stockItem = await _stockItemRepository.GetByProductAndWarehouseAsync(row.ProductId, warehouseId, cancellationToken);
            if (stockItem is not null)
            {
                var lots = await _traceability.ListLotsAsync(stockItem.Id, cancellationToken);
                if (lots.Count > 0)
                {
                    foreach (var lot in lots)
                    {
                        detailed.Add((row.ProductId, row.ProductName, row.ProductCode, lot.QuantityOnHand, lot.ProductLotId, lot.LotNumber));
                    }
                    continue;
                }
            }

            detailed.Add((row.ProductId, row.ProductName, row.ProductCode, row.TheoreticalQuantity, null, null));
        }

        var inventoryResult = PhysicalInventory.StartDetailed(
            reference,
            warehouseId,
            request.Type,
            detailed,
            request.Notes);

        if (inventoryResult.IsFailure)
            return Result.Failure<StartInventoryResult>(inventoryResult.Error);

        var inventory = inventoryResult.Value;
        try
        {
            await _inventoryRepository.AddAsync(inventory, cancellationToken);
        }
        catch (DbUpdateException ex) when (IsInventoryReferenceUniqueViolation(ex))
        {
            reference = await _inventoryRepository.GetNextReferenceAsync(cancellationToken);
            inventoryResult = PhysicalInventory.StartDetailed(
                reference,
                warehouseId,
                request.Type,
                detailed,
                request.Notes);
            if (inventoryResult.IsFailure)
                return Result.Failure<StartInventoryResult>(inventoryResult.Error);
            inventory = inventoryResult.Value;
            await _inventoryRepository.AddAsync(inventory, cancellationToken);
        }

        // Generate pedagogical message
        var typeLabel = request.Type == InventoryType.Complete ? "complet" : "partiel";
        var humanMessage = $"📋 Inventaire {typeLabel} démarré ! Vous avez {productsToInventory.Count} produit{(productsToInventory.Count > 1 ? "s" : "")} à compter.";

        var trackingByProduct = await BuildTrackingLookupAsync(productsToInventory.Select(p => p.ProductId), cancellationToken);

        return Result.Success(new StartInventoryResult
        {
            InventoryId = inventory.Id,
            TotalProducts = productsToInventory.Count,
            HumanMessage = humanMessage,
            Products = inventory.CountLines.Select(l => MapProductItem(l, trackingByProduct)).ToList()
        });
    }

    private async Task<IReadOnlyDictionary<Guid, ProductTrackingInfo>> BuildTrackingLookupAsync(
        IEnumerable<Guid> productIds,
        CancellationToken cancellationToken)
    {
        var ids = productIds.Distinct().ToList();
        return ids.Count == 0
            ? new Dictionary<Guid, ProductTrackingInfo>()
            : await _productRepository.GetTrackingInfoByIdsAsync(ids, cancellationToken);
    }

    private static InventoryProductItem MapProductItem(
        InventoryCountLine line,
        IReadOnlyDictionary<Guid, ProductTrackingInfo>? trackingByProduct)
    {
        ProductTrackingInfo? tracking = null;
        if (trackingByProduct is not null)
            trackingByProduct.TryGetValue(line.ProductId, out tracking);

        return new InventoryProductItem
        {
            ProductId = line.ProductId,
            ProductName = line.ProductName,
            ProductCode = line.ProductCode,
            TheoreticalQuantity = line.TheoreticalQuantity,
            IsCounted = line.IsCounted,
            CountedQuantity = line.CountedQuantity,
            ProductLotId = line.ProductLotId,
            LotNumber = line.LotNumber,
            TrackingMode = tracking?.TrackingMode ?? TrackingMode.None,
            HasExpiryTracking = tracking?.HasExpiryTracking ?? false
        };
    }

    private async Task<List<Product>> GetProductsAsync(List<Guid> productIds, CancellationToken cancellationToken)
    {
        var products = new List<Product>();
        foreach (var id in productIds)
        {
            var product = await _productRepository.GetByIdAsync(id, cancellationToken);
            if (product != null)
                products.Add(product);
        }
        return products;
    }

    private static bool IsInventoryReferenceUniqueViolation(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("IX_PhysicalInventories_Reference", StringComparison.OrdinalIgnoreCase)
               || (message.Contains("PhysicalInventories", StringComparison.OrdinalIgnoreCase) && message.Contains("Reference", StringComparison.OrdinalIgnoreCase) && (message.Contains("2627") || message.Contains("2601")));
    }
}
