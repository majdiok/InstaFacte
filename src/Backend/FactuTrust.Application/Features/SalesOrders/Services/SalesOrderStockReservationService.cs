using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.SalesOrders.Services;

/// <summary>
/// Pose et lève les réservations de stock d'une commande client.
///
/// Tout passe par ici pour que le drapeau
/// <c>Features:SalesOrders:StockReservationEnabled</c> n'ait qu'un seul point d'application.
/// Drapeau désactivé — le défaut — ces méthodes ne touchent à rien :
/// <c>QuantityReserved</c> reste à zéro et le comportement du produit est inchangé.
/// </summary>
public interface ISalesOrderStockReservationService
{
    /// <summary>Réserve le reste à livrer de chaque ligne. Sans effet si le drapeau est éteint.</summary>
    Task<Result> ReserveAsync(SalesOrder order, CancellationToken cancellationToken = default);

    /// <summary>Libère les réservations restantes. Sans effet si le drapeau est éteint.</summary>
    Task<Result> ReleaseAsync(SalesOrder order, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class SalesOrderStockReservationService : ISalesOrderStockReservationService
{
    private readonly IStockItemRepository _stockItemRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;
    private readonly SalesOrderOptions _options;
    private readonly ILogger<SalesOrderStockReservationService> _logger;

    public SalesOrderStockReservationService(
        IStockItemRepository stockItemRepository,
        IWarehouseRepository warehouseRepository,
        IProductRepository productRepository,
        IOptions<SalesOrderOptions> options,
        ILogger<SalesOrderStockReservationService> logger)
    {
        _stockItemRepository = stockItemRepository;
        _warehouseRepository = warehouseRepository;
        _productRepository = productRepository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result> ReserveAsync(SalesOrder order, CancellationToken cancellationToken = default)
    {
        if (!_options.StockReservationEnabled)
            return Result.Success();

        if (order.IsStockReserved)
            return Result.Success(); // idempotent

        var warehouse = await ResolveWarehouseAsync(order, cancellationToken);
        if (warehouse is null)
        {
            _logger.LogWarning(
                "Aucun entrepôt résolu pour la commande {Number} — réservation ignorée",
                order.Number.Value);
            return Result.Success();
        }

        foreach (var line in order.Lines)
        {
            var product = await _productRepository.GetByIdAsync(line.ProductId, cancellationToken);
            if (product is null || !product.IsStockManaged)
                continue;

            var stockItem = await _stockItemRepository.GetByProductAndWarehouseAsync(
                line.ProductId, warehouse.Id, cancellationToken);

            if (stockItem is null)
                continue;

            // On ne réserve que ce qui reste à livrer : une confirmation tardive après une
            // livraison partielle ne doit pas immobiliser deux fois la même marchandise.
            var toReserve = line.PendingDeliveryQuantity;
            if (toReserve <= 0)
                continue;

            var reserve = stockItem.Reserve(toReserve);
            if (reserve.IsFailure)
            {
                // Stock insuffisant : la confirmation n'est PAS bloquée — c'est un engagement
                // commercial, pas un mouvement physique. L'écart sera visible au carnet.
                _logger.LogWarning(
                    "Réservation impossible pour le produit {ProductId} sur la commande {Number} : {Error}",
                    line.ProductId, order.Number.Value, reserve.Error.Description);
                continue;
            }

            await _stockItemRepository.UpdateAsync(stockItem, cancellationToken);
        }

        return order.MarkStockReserved();
    }

    public async Task<Result> ReleaseAsync(SalesOrder order, CancellationToken cancellationToken = default)
    {
        if (!_options.StockReservationEnabled)
            return Result.Success();

        if (!order.IsStockReserved)
            return Result.Success(); // idempotent

        var warehouse = await ResolveWarehouseAsync(order, cancellationToken);
        if (warehouse is null)
        {
            order.MarkStockReleased();
            return Result.Success();
        }

        foreach (var line in order.Lines)
        {
            var stockItem = await _stockItemRepository.GetByProductAndWarehouseAsync(
                line.ProductId, warehouse.Id, cancellationToken);

            if (stockItem is null)
                continue;

            // On ne libère jamais plus que ce qui est effectivement réservé : d'autres
            // commandes peuvent partager le même article.
            var toRelease = Math.Min(line.PendingDeliveryQuantity, stockItem.QuantityReserved);
            if (toRelease <= 0)
                continue;

            var release = stockItem.ReleaseReservation(toRelease);
            if (release.IsFailure)
            {
                _logger.LogWarning(
                    "Libération impossible pour le produit {ProductId} sur la commande {Number} : {Error}",
                    line.ProductId, order.Number.Value, release.Error.Description);
                continue;
            }

            await _stockItemRepository.UpdateAsync(stockItem, cancellationToken);
        }

        order.MarkStockReleased();
        return Result.Success();
    }

    private async Task<Warehouse?> ResolveWarehouseAsync(SalesOrder order, CancellationToken cancellationToken)
    {
        if (order.WarehouseId is { } id)
        {
            var warehouse = await _warehouseRepository.GetByIdAsync(id, cancellationToken);
            if (warehouse is not null)
                return warehouse;
        }

        return await _warehouseRepository.GetDefaultAsync(cancellationToken);
    }
}
