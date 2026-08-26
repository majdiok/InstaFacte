using System.Globalization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using MediatR;

namespace FactuTrust.Application.Features.Stock.Queries;

/// <summary>
/// Query pour vérifier la disponibilité du stock avant validation de facture.
/// Retourne des messages pédagogiques expliquant l'impact de la vente.
/// </summary>
public sealed record CheckStockAvailabilityQuery(
    List<ProductQuantityRequest> Items,
    Guid? WarehouseId = null
) : IRequest<Result<StockAvailabilityResult>>;

/// <summary>
/// Demande de quantité pour un produit.
/// </summary>
public sealed record ProductQuantityRequest(
    Guid ProductId,
    decimal RequestedQuantity
);

/// <summary>
/// Résultat de la vérification de disponibilité du stock.
/// </summary>
public sealed record StockAvailabilityResult
{
    /// <summary>
    /// Indique si tous les produits sont disponibles en quantité suffisante.
    /// </summary>
    public bool AllAvailable { get; init; }
    
    /// <summary>
    /// Nombre de produits avec stock insuffisant (warning uniquement, pas de blocage).
    /// </summary>
    public int InsufficientCount { get; init; }
    
    /// <summary>
    /// Détails par produit avec messages pédagogiques.
    /// </summary>
    public List<ProductAvailabilityDetail> Details { get; init; } = new();
    
    /// <summary>
    /// Message résumé pédagogique pour l'utilisateur.
    /// </summary>
    public string SummaryMessage { get; init; } = string.Empty;
}

/// <summary>
/// Détail de disponibilité pour un produit avec message pédagogique.
/// </summary>
public sealed record ProductAvailabilityDetail
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string ProductCode { get; init; } = string.Empty;
    public decimal RequestedQuantity { get; init; }
    public decimal AvailableQuantity { get; init; }
    public decimal RemainingAfterSale { get; init; }
    public bool IsAvailable { get; init; }
    public bool IsStockManaged { get; init; }
    
    /// <summary>
    /// Message pédagogique pour l'utilisateur.
    /// Ex: "Cette vente va retirer 3 unités (il restera 9 unités)"
    /// </summary>
    public string HumanMessage { get; init; } = string.Empty;
    
    /// <summary>
    /// Niveau d'alerte : "ok", "warning", "insufficient"
    /// </summary>
    public string AlertLevel { get; init; } = "ok";
}

/// <summary>
/// Handler pour CheckStockAvailabilityQuery.
/// </summary>
public sealed class CheckStockAvailabilityQueryHandler 
    : IRequestHandler<CheckStockAvailabilityQuery, Result<StockAvailabilityResult>>
{
    private readonly IStockItemRepository _stockItemRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;
    private readonly ITenantContext _tenantContext;

    public CheckStockAvailabilityQueryHandler(
        IStockItemRepository stockItemRepository,
        IWarehouseRepository warehouseRepository,
        IProductRepository productRepository,
        ITenantContext tenantContext)
    {
        _stockItemRepository = stockItemRepository;
        _warehouseRepository = warehouseRepository;
        _productRepository = productRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Result<StockAvailabilityResult>> Handle(
        CheckStockAvailabilityQuery request, 
        CancellationToken cancellationToken)
    {
        if (_tenantContext.TenantId is null)
            return Result.Failure<StockAvailabilityResult>(
                Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        if (request.Items == null || !request.Items.Any())
            return Result.Success(new StockAvailabilityResult
            {
                AllAvailable = true,
                InsufficientCount = 0,
                Details = new List<ProductAvailabilityDetail>(),
                SummaryMessage = "Aucun produit à vérifier."
            });

        Warehouse? targetWarehouse;
        if (request.WarehouseId is { } wid)
        {
            var wh = await _warehouseRepository.GetByIdAsync(wid, cancellationToken);
            if (wh is null)
                return Result.Failure<StockAvailabilityResult>(Error.NotFound("Warehouse", wid));
            if (!wh.IsActive)
                return Result.Failure<StockAvailabilityResult>(
                    Error.Validation("Warehouse", "Cet entrepôt est désactivé."));
            targetWarehouse = wh;
        }
        else
        {
            var defaultWarehouse = await _warehouseRepository.GetDefaultAsync(cancellationToken);
            if (defaultWarehouse == null)
            {
                return Result.Failure<StockAvailabilityResult>(
                    Error.Validation("Warehouse", "Aucun entrepôt par défaut n'est configuré."));
            }

            targetWarehouse = defaultWarehouse;
        }

        var details = new List<ProductAvailabilityDetail>();
        var insufficientCount = 0;

        foreach (var item in request.Items)
        {
            var product = await _productRepository.GetByIdAsync(item.ProductId, cancellationToken);
            if (product == null)
            {
                details.Add(new ProductAvailabilityDetail
                {
                    ProductId = item.ProductId,
                    ProductName = "Produit inconnu",
                    ProductCode = "",
                    RequestedQuantity = item.RequestedQuantity,
                    AvailableQuantity = 0,
                    RemainingAfterSale = 0,
                    IsAvailable = false,
                    IsStockManaged = false,
                    HumanMessage = "Ce produit n'existe pas.",
                    AlertLevel = "error"
                });
                continue;
            }

            // Si le produit n'a pas la gestion de stock, pas de vérification
            if (!product.IsStockManaged)
            {
                details.Add(new ProductAvailabilityDetail
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    ProductCode = product.Code,
                    RequestedQuantity = item.RequestedQuantity,
                    AvailableQuantity = decimal.MaxValue,
                    RemainingAfterSale = decimal.MaxValue,
                    IsAvailable = true,
                    IsStockManaged = false,
                    HumanMessage = "Ce produit n'a pas de gestion de stock.",
                    AlertLevel = "ok"
                });
                continue;
            }

            // Récupérer le stock
            var stockItem = await _stockItemRepository.GetByProductAndWarehouseAsync(
                item.ProductId, targetWarehouse.Id, cancellationToken);

            var availableQuantity = stockItem?.QuantityAvailable ?? 0;
            var remainingAfterSale = availableQuantity - item.RequestedQuantity;
            var isAvailable = remainingAfterSale >= 0;

            if (!isAvailable)
            {
                insufficientCount++;
            }

            var (humanMessage, alertLevel) = GenerateHumanMessage(
                product.Name, 
                item.RequestedQuantity, 
                availableQuantity, 
                remainingAfterSale,
                stockItem?.MinimumStock ?? 0);

            details.Add(new ProductAvailabilityDetail
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ProductCode = product.Code,
                RequestedQuantity = item.RequestedQuantity,
                AvailableQuantity = availableQuantity,
                RemainingAfterSale = Math.Max(0, remainingAfterSale),
                IsAvailable = isAvailable,
                IsStockManaged = true,
                HumanMessage = humanMessage,
                AlertLevel = alertLevel
            });
        }

        var allAvailable = insufficientCount == 0;
        var summaryMessage = GenerateSummaryMessage(details, insufficientCount);

        return Result.Success(new StockAvailabilityResult
        {
            AllAvailable = allAvailable,
            InsufficientCount = insufficientCount,
            Details = details,
            SummaryMessage = summaryMessage
        });
    }

    /// <summary>
    /// Génère un message pédagogique pour un produit.
    /// </summary>
    private static (string Message, string AlertLevel) GenerateHumanMessage(
        string productName,
        decimal requested,
        decimal available,
        decimal remaining,
        decimal minimumStock)
    {
        var unitText = requested == 1 ? "unité" : "unités";
        var remainingUnitText = Math.Abs(remaining) == 1 ? "unité" : "unités";

        if (remaining < 0)
        {
            // Stock insuffisant (warning uniquement, pas de blocage)
            return (
                $"⚠️ Stock insuffisant : vous n'avez que {FormatQty(available)} {unitText} de {productName}. " +
                $"Il vous manque {FormatQty(Math.Abs(remaining))} {remainingUnitText}.",
                "warning"
            );
        }
        
        if (remaining <= minimumStock && minimumStock > 0)
        {
            // Le stock sera bas après cette vente
            return (
                $"Cette vente va retirer {FormatQty(requested)} {unitText}. " +
                $"Attention : il ne restera que {FormatQty(remaining)} {remainingUnitText} (stock bas).",
                "warning"
            );
        }
        
        if (remaining == 0)
        {
            // Rupture après cette vente
            return (
                $"Cette vente va retirer les {FormatQty(requested)} dernières {unitText}. " +
                $"Le produit sera en rupture de stock.",
                "warning"
            );
        }

        // Tout va bien
        return (
            $"Cette vente va retirer {FormatQty(requested)} {unitText}. Il restera {FormatQty(remaining)} {remainingUnitText}.",
            "ok"
        );
    }

    /// <summary>
    /// Formats stock quantities for user-facing messages (no trailing zeros, fr-FR separators).
    /// </summary>
    internal static string FormatQty(decimal value) =>
        value.ToString("0.###", CultureInfo.GetCultureInfo("fr-FR"));

    /// <summary>
    /// Génère un message résumé global.
    /// </summary>
    private static string GenerateSummaryMessage(
        List<ProductAvailabilityDetail> details, 
        int insufficientCount)
    {
        var stockManagedItems = details.Where(d => d.IsStockManaged).ToList();
        
        if (!stockManagedItems.Any())
        {
            return "Aucun produit avec gestion de stock dans cette vente.";
        }
        
        if (insufficientCount == 0)
        {
            var warnings = stockManagedItems.Count(d => d.AlertLevel == "warning");
            if (warnings > 0)
            {
                return $"✅ Stock suffisant, mais {warnings} produit(s) auront un stock bas après cette vente.";
            }
            return "✅ Tous les produits sont disponibles en quantité suffisante.";
        }

        if (insufficientCount == 1)
        {
            var item = stockManagedItems.First(d => !d.IsAvailable);
            return $"⚠️ Attention : stock insuffisant pour {item.ProductName}. Vous pouvez continuer, mais le stock sera négatif.";
        }

        return $"⚠️ Attention : stock insuffisant pour {insufficientCount} produits. Vous pouvez continuer, mais le stock sera négatif.";
    }
}
