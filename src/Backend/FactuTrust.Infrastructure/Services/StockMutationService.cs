using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services;

public sealed class StockMutationService : IStockMutationService
{
    private readonly ITenantDbContextFactory _contextFactory;
    private readonly StockTraceabilityOptions _options;

    public StockMutationService(
        ITenantDbContextFactory contextFactory,
        IOptions<StockTraceabilityOptions> options)
    {
        _contextFactory = contextFactory;
        _options = options.Value;
    }

    public Result Apply(
        StockItem stockItem,
        StockMutationRequest request,
        Product? product = null,
        IStockTraceabilityStore? store = null)
    {
        if (!RequiresTraceability(product) && !RequiresLayeredCosting(product))
            return ApplyPassthrough(stockItem, request);

        if (store is null)
            return Result.Failure(Error.Validation("Store",
                "Un magasin de traçabilité est obligatoire pour un article suivi ou valorisé FIFO/LIFO."));

        return ApplyTracked(stockItem, product!, request, store);
    }

    public async Task<Result<StockMutationResult>> ApplyAsync(
        StockMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();

        var product = await context.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        var stockItem = await LoadStockItemAsync(context, request.ProductId, request.WarehouseId, cancellationToken);

        if (stockItem is null)
        {
            if (request.Kind is StockMutationKind.Exit or StockMutationKind.ReleaseAndExit)
            {
                return Result.Failure<StockMutationResult>(Error.Validation(
                    "StockItem",
                    "Aucun stock trouvé pour ce produit dans cet entrepôt."));
            }

            var created = StockItem.Create(request.ProductId, request.WarehouseId);
            if (created.IsFailure)
                return Result.Failure<StockMutationResult>(created.Error);

            stockItem = created.Value;
            context.StockItems.Add(stockItem);
        }

        var store = new EfStockTraceabilityStore(context);
        var apply = Apply(stockItem, request, product, store);
        if (apply.IsFailure)
            return Result.Failure<StockMutationResult>(apply.Error);

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(new StockMutationResult(stockItem.Id, stockItem.QuantityOnHand, stockItem.AverageCost));
    }

    public async Task<Result> CreateOpeningValuationLayersAsync(
        Guid productId,
        CostingMethod costingMethod,
        CancellationToken cancellationToken = default)
    {
        if (!_options.FifoLifoValuationEnabled)
        {
            return Result.Failure(Error.Validation("CostingMethod",
                "La valorisation FIFO/LIFO n'est pas activée pour ce tenant."));
        }

        if (costingMethod is not CostingMethod.Fifo and not CostingMethod.Lifo)
        {
            return Result.Failure(Error.Validation("CostingMethod",
                "La couche d'ouverture n'est applicable qu'au FIFO ou au LIFO."));
        }

        await using var context = _contextFactory.CreateContext();
        var product = await context.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null)
            return Result.Failure(Error.NotFound("Product", productId));

        var store = new EfStockTraceabilityStore(context);
        var items = await context.StockItems
            .Where(s => s.ProductId == productId)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;

        foreach (var item in items)
        {
            if (item.QuantityOnHand <= 0)
                continue;

            if (store.ListOpenLayers(item.Id).Count > 0)
            {
                return Result.Failure(Error.Validation("CostingMethod",
                    "Une couche de valorisation existe déjà. Impossible de recréer la couche d'ouverture."));
            }

            var layer = StockValuationLayer.Create(
                item.Id,
                item.QuantityOnHand,
                item.AverageCost,
                now,
                sourceReference: "OUVERTURE");
            if (layer.IsFailure)
                return layer;

            store.AddLayer(layer.Value);
        }

        var trace = product.ConfigureTraceability(
            product.TrackingMode,
            product.HasExpiryTracking,
            product.PickingPolicy,
            costingMethod,
            product.ExpiryAlertDays);
        if (trace.IsFailure)
            return trace;

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static async Task<StockItem?> LoadStockItemAsync(
        TenantDbContext context,
        Guid productId,
        Guid warehouseId,
        CancellationToken cancellationToken)
    {
        IQueryable<StockItem> query = context.StockItems.Include(s => s.Movements);
        if (context.Database.IsSqlServer())
        {
            query = context.StockItems
                .FromSqlInterpolated(
                    $"SELECT * FROM [StockItems] WITH (UPDLOCK, ROWLOCK) WHERE [ProductId] = {productId} AND [WarehouseId] = {warehouseId}")
                .Include(s => s.Movements);
            return await query.FirstOrDefaultAsync(cancellationToken);
        }

        return await query.FirstOrDefaultAsync(
            s => s.ProductId == productId && s.WarehouseId == warehouseId,
            cancellationToken);
    }

    private bool RequiresTraceability(Product? product)
    {
        if (product is null)
            return false;
        if (product.TrackingMode == TrackingMode.Lot && _options.LotTrackingEnabled)
            return true;
        if (product.TrackingMode == TrackingMode.Serial && _options.SerialTrackingEnabled)
            return true;
        return false;
    }

    private bool RequiresLayeredCosting(Product? product) =>
        product is not null
        && _options.FifoLifoValuationEnabled
        && product.CostingMethod is CostingMethod.Fifo or CostingMethod.Lifo;

    internal static Result ApplyPassthrough(StockItem stockItem, StockMutationRequest request) =>
        request.Kind switch
        {
            StockMutationKind.Entry => stockItem.RecordEntry(
                request.Quantity,
                request.UnitCost,
                request.Reason,
                request.Reference,
                request.Notes),
            StockMutationKind.Exit => stockItem.RecordExit(
                request.Quantity,
                request.Reason,
                request.Reference,
                request.Notes,
                request.ShortfallQuantity),
            StockMutationKind.ReleaseAndExit => stockItem.ReleaseAndExit(
                request.Quantity,
                request.Reason,
                request.Reference,
                request.Notes,
                request.ReservedQuantity,
                request.ShortfallQuantity),
            StockMutationKind.Adjust => stockItem.AdjustStock(request.Quantity, request.Notes),
            _ => Result.Failure(Error.Validation("Kind", "Type de mutation de stock inconnu"))
        };

    private Result ApplyTracked(
        StockItem stockItem,
        Product product,
        StockMutationRequest request,
        IStockTraceabilityStore store)
    {
        if (request.Kind == StockMutationKind.ReleaseAndExit)
        {
            var toRelease = request.ReservedQuantity ?? Math.Min(request.Quantity, stockItem.QuantityReserved);
            if (toRelease > 0)
            {
                var release = stockItem.ReleaseReservation(toRelease);
                if (release.IsFailure)
                    return release;
            }
        }

        var trackedRequest = request.Kind == StockMutationKind.ReleaseAndExit
            ? request with { Kind = StockMutationKind.Exit }
            : request;

        Result result = trackedRequest.Kind switch
        {
            StockMutationKind.Entry => ApplyTrackedEntry(stockItem, product, trackedRequest, store),
            StockMutationKind.Exit => ApplyTrackedExit(stockItem, product, trackedRequest, store),
            StockMutationKind.Adjust => ApplyTrackedAdjust(stockItem, product, trackedRequest, store),
            _ => Result.Failure(Error.Validation("Kind", "Type de mutation de stock inconnu"))
        };

        if (result.IsFailure)
            return result;

        if (RequiresLayeredCosting(product))
        {
            var remainingValue = store.ListOpenLayers(stockItem.Id).Sum(l => l.RemainingValue);
            stockItem.RecalculateDisplayAverageCost(remainingValue);
        }

        if (product.TrackingMode == TrackingMode.Lot)
        {
            var invariant = LotBalanceInvariant.AssertMatchesOnHand(
                stockItem.QuantityOnHand,
                store.ListBalances(stockItem.Id).Select(b => b.QuantityOnHand));
            if (invariant.IsFailure)
                return invariant;
        }

        return Result.Success();
    }

    private Result ApplyTrackedEntry(
        StockItem stockItem,
        Product product,
        StockMutationRequest request,
        IStockTraceabilityStore store)
    {
        if (product.TrackingMode == TrackingMode.Serial)
            return ApplySerialEntry(stockItem, product, request, store);

        var allocations = request.Allocations;
        if (allocations is null || allocations.Count == 0)
        {
            if (product.TrackingMode == TrackingMode.Lot)
            {
                return Result.Failure(Error.Validation(
                    "Allocations",
                    "Le numéro de lot est obligatoire pour une entrée d'article suivi."));
            }

            return ApplyLayeredEntry(stockItem, product, request, store, productLotId: null, request.Quantity, request.UnitCost);
        }

        if (Math.Abs(allocations.Sum(a => a.Quantity) - request.Quantity) > LotBalanceInvariant.Tolerance)
        {
            return Result.Failure(Error.Validation(
                "Allocations",
                "La somme des allocations doit être égale à la quantité de la ligne."));
        }

        foreach (var allocation in allocations)
        {
            Guid? lotId = null;
            if (product.TrackingMode == TrackingMode.Lot)
            {
                var lotResult = GetOrCreateLot(product, allocation, store);
                if (lotResult.IsFailure)
                    return lotResult;
                lotId = lotResult.Value.Id;

                var balance = store.FindBalance(stockItem.Id, lotId.Value);
                if (balance is null)
                {
                    var created = StockLotBalance.Create(stockItem.Id, lotId.Value);
                    if (created.IsFailure)
                        return created;
                    balance = created.Value;
                    store.AddBalance(balance);
                }

                var increase = balance.Increase(allocation.Quantity);
                if (increase.IsFailure)
                    return increase;
            }

            var unitCost = allocation.UnitCost ?? request.UnitCost;
            var entry = ApplyLayeredEntry(stockItem, product, request, store, lotId, allocation.Quantity, unitCost);
            if (entry.IsFailure)
                return entry;

            PersistDocumentAllocation(request, product.Id, allocation.Quantity, lotId, serialId: null, unitCost, store);
        }

        return Result.Success();
    }

    private Result ApplyLayeredEntry(
        StockItem stockItem,
        Product product,
        StockMutationRequest request,
        IStockTraceabilityStore store,
        Guid? productLotId,
        decimal quantity,
        decimal unitCost)
    {
        var layered = RequiresLayeredCosting(product);
        var entry = stockItem.RecordEntry(
            quantity,
            unitCost,
            request.Reason,
            request.Reference,
            request.Notes,
            productLotId,
            serialId: null,
            valuationLayerId: null,
            updateWeightedAverage: !layered);
        if (entry.IsFailure)
            return entry;

        if (!layered)
            return Result.Success();

        var lastMovement = stockItem.Movements.Last();
        var layerResult = StockValuationLayer.Create(
            stockItem.Id,
            quantity,
            unitCost,
            lastMovement.OccurredAt,
            productLotId,
            request.Reference);
        if (layerResult.IsFailure)
            return layerResult;

        store.AddLayer(layerResult.Value);
        return Result.Success();
    }

    private Result ApplyTrackedExit(
        StockItem stockItem,
        Product product,
        StockMutationRequest request,
        IStockTraceabilityStore store)
    {
        if (product.TrackingMode == TrackingMode.Serial)
            return ApplySerialExit(stockItem, product, request, store);

        IReadOnlyList<StockAllocationInput> allocations;
        if (request.Allocations is { Count: > 0 })
        {
            allocations = request.Allocations;
        }
        else if (product.TrackingMode == TrackingMode.Lot
                 && product.PickingPolicy is PickingPolicy.Fefo or PickingPolicy.FifoPhysical)
        {
            var auto = AutoAllocateLots(stockItem, product, request.Quantity, store);
            if (auto.IsFailure)
                return auto;
            allocations = auto.Value;
        }
        else if (product.TrackingMode == TrackingMode.Lot)
        {
            return Result.Failure(Error.Validation(
                "Allocations",
                "La sortie d'un article suivi par lot exige une allocation (saisie ou FEFO)."));
        }
        else
        {
            allocations = new[] { new StockAllocationInput(request.Quantity) };
        }

        if (Math.Abs(allocations.Sum(a => a.Quantity) - request.Quantity) > LotBalanceInvariant.Tolerance)
        {
            return Result.Failure(Error.Validation(
                "Allocations",
                "La somme des allocations doit être égale à la quantité de la ligne."));
        }

        foreach (var allocation in allocations)
        {
            Guid? lotId = allocation.ProductLotId;
            if (product.TrackingMode == TrackingMode.Lot)
            {
                var resolved = ResolveLot(product.Id, allocation, store);
                if (resolved.IsFailure)
                    return resolved;
                lotId = resolved.Value.Id;

                if (_options.BlockExpiredLotsOnExit
                    && product.HasExpiryTracking
                    && _options.ExpiryTrackingEnabled
                    && resolved.Value.IsExpired(DateTime.UtcNow))
                {
                    return Result.Failure(Error.Validation(
                        "ExpiryDate",
                        $"Le lot {resolved.Value.LotNumber} est périmé et ne peut pas sortir."));
                }

                var balance = store.FindBalance(stockItem.Id, lotId.Value);
                if (balance is null)
                    return Result.Failure(Error.Validation("Lot", $"Aucun solde pour le lot {resolved.Value.LotNumber}"));

                var decrease = balance.Decrease(allocation.Quantity);
                if (decrease.IsFailure)
                    return decrease;
            }

            var consume = ConsumeCostAndExit(
                stockItem,
                product,
                request,
                store,
                lotId,
                allocation.Quantity);
            if (consume.IsFailure)
                return consume;

            PersistDocumentAllocation(
                request,
                product.Id,
                allocation.Quantity,
                lotId,
                serialId: null,
                unitCost: null,
                store);
        }

        return Result.Success();
    }

    private Result ConsumeCostAndExit(
        StockItem stockItem,
        Product product,
        StockMutationRequest request,
        IStockTraceabilityStore store,
        Guid? productLotId,
        decimal quantity)
    {
        if (!RequiresLayeredCosting(product))
        {
            return stockItem.RecordExit(
                quantity,
                request.Reason,
                request.Reference,
                request.Notes,
                shortfallQuantity: null,
                unitCostOverride: null,
                productLotId);
        }

        var layers = store.ListOpenLayers(stockItem.Id)
            .Where(l => productLotId is null || l.ProductLotId == productLotId || l.ProductLotId is null)
            .ToList();

        layers = product.CostingMethod == CostingMethod.Lifo
            ? layers.OrderByDescending(l => l.ReceivedAt).ThenByDescending(l => l.Id).ToList()
            : layers.OrderBy(l => l.ReceivedAt).ThenBy(l => l.Id).ToList();

        var remaining = quantity;
        foreach (var layer in layers)
        {
            if (remaining <= 0)
                break;

            var take = Math.Min(remaining, layer.RemainingQuantity);
            var consume = layer.Consume(take);
            if (consume.IsFailure)
                return consume;

            var exit = stockItem.RecordExit(
                take,
                request.Reason,
                request.Reference,
                request.Notes,
                shortfallQuantity: null,
                unitCostOverride: layer.UnitCost,
                productLotId,
                serialId: null,
                valuationLayerId: layer.Id);
            if (exit.IsFailure)
                return exit;

            remaining -= take;
        }

        if (remaining > 0)
        {
            return Result.Failure(Error.Validation(
                "ValuationLayer",
                $"Couches de valorisation insuffisantes. Restant à valoriser: {remaining}"));
        }

        return Result.Success();
    }

    private Result ApplyTrackedAdjust(
        StockItem stockItem,
        Product product,
        StockMutationRequest request,
        IStockTraceabilityStore store)
    {
        if (product.TrackingMode != TrackingMode.None)
        {
            return Result.Failure(Error.Validation(
                "TrackingMode",
                "L'ajustement global est interdit sur un article suivi. Comptez par lot (inventaire)."));
        }

        var layered = RequiresLayeredCosting(product);
        var difference = request.Quantity - stockItem.QuantityOnHand;
        if (difference == 0)
            return Result.Success();

        if (!layered)
            return stockItem.AdjustStock(request.Quantity, request.Notes);

        if (difference > 0)
        {
            return ApplyLayeredEntry(
                stockItem,
                product,
                request with { Kind = StockMutationKind.Entry, Quantity = difference, Reason = MovementReason.InventoryAdjustment },
                store,
                productLotId: null,
                difference,
                stockItem.AverageCost);
        }

        return ConsumeCostAndExit(
            stockItem,
            product,
            request with { Kind = StockMutationKind.Exit, Quantity = -difference, Reason = MovementReason.InventoryAdjustment },
            store,
            productLotId: null,
            -difference);
    }

    private Result ApplySerialEntry(
        StockItem stockItem,
        Product product,
        StockMutationRequest request,
        IStockTraceabilityStore store)
    {
        var allocations = request.Allocations;
        if (allocations is null || allocations.Count == 0)
            return Result.Failure(Error.Validation("Allocations", "Les numéros de série sont obligatoires à l'entrée."));

        if (allocations.Count != (int)request.Quantity || allocations.Any(a => a.Quantity != 1m))
        {
            return Result.Failure(Error.Validation(
                "Allocations",
                "Chaque numéro de série correspond à une quantité de 1."));
        }

        foreach (var allocation in allocations)
        {
            if (string.IsNullOrWhiteSpace(allocation.SerialNumber) && allocation.SerialId is null)
                return Result.Failure(Error.Validation("SerialNumber", "Le numéro de série est obligatoire"));

            if (allocation.SerialId is { } existingId)
            {
                var existing = store.GetSerial(existingId);
                if (existing is null)
                    return Result.Failure(Error.NotFound("ProductSerial", existingId));
                var restore = existing.RestoreToStock(request.WarehouseId);
                if (restore.IsFailure)
                    return restore;
            }
            else
            {
                var duplicate = store.FindSerial(product.Id, allocation.SerialNumber!);
                if (duplicate is not null)
                    return Result.Failure(Error.Conflict($"Le numéro de série {duplicate.SerialNumber} existe déjà"));

                Guid? lotId = null;
                if (!string.IsNullOrWhiteSpace(allocation.LotNumber) || allocation.ProductLotId.HasValue)
                {
                    var lot = ResolveLot(product.Id, allocation, store);
                    if (lot.IsFailure)
                    {
                        var createdLot = GetOrCreateLot(product, allocation, store);
                        if (createdLot.IsFailure)
                            return createdLot;
                        lotId = createdLot.Value.Id;
                    }
                    else
                    {
                        lotId = lot.Value.Id;
                    }
                }

                var created = ProductSerial.Create(
                    product.Id,
                    allocation.SerialNumber!,
                    request.WarehouseId,
                    lotId,
                    allocation.ExpiryDate);
                if (created.IsFailure)
                    return created;
                store.AddSerial(created.Value);
                PersistDocumentAllocation(request, product.Id, 1m, lotId, created.Value.Id, request.UnitCost, store);
            }

            var entry = stockItem.RecordEntry(
                1m,
                request.UnitCost,
                request.Reason,
                request.Reference,
                request.Notes,
                productLotId: null,
                serialId: allocation.SerialId,
                updateWeightedAverage: !RequiresLayeredCosting(product));
            if (entry.IsFailure)
                return entry;

            if (RequiresLayeredCosting(product))
            {
                var layer = StockValuationLayer.Create(
                    stockItem.Id, 1m, request.UnitCost, DateTime.UtcNow, null, request.Reference);
                if (layer.IsFailure)
                    return layer;
                store.AddLayer(layer.Value);
            }
        }

        return Result.Success();
    }

    private Result ApplySerialExit(
        StockItem stockItem,
        Product product,
        StockMutationRequest request,
        IStockTraceabilityStore store)
    {
        var allocations = request.Allocations;
        if (allocations is null || allocations.Count == 0)
        {
            var available = store.ListInStockSerials(product.Id, request.WarehouseId)
                .Take((int)request.Quantity)
                .ToList();
            if (available.Count != (int)request.Quantity)
            {
                return Result.Failure(Error.Validation(
                    "Serial",
                    $"Numéros de série insuffisants. Demandé: {request.Quantity}, disponible: {available.Count}"));
            }

            allocations = available
                .Select(s => new StockAllocationInput(1m, SerialId: s.Id, SerialNumber: s.SerialNumber))
                .ToList();
        }

        foreach (var allocation in allocations)
        {
            var serial = allocation.SerialId is { } id
                ? store.GetSerial(id)
                : store.FindSerial(product.Id, allocation.SerialNumber ?? string.Empty);
            if (serial is null)
                return Result.Failure(Error.Validation("Serial", "Numéro de série introuvable"));

            var sold = serial.MarkSold();
            if (sold.IsFailure)
                return sold;

            var consume = ConsumeCostAndExit(stockItem, product, request, store, serial.ProductLotId, 1m);
            if (consume.IsFailure)
                return consume;

            PersistDocumentAllocation(request, product.Id, 1m, serial.ProductLotId, serial.Id, null, store);
        }

        return Result.Success();
    }

    private Result<IReadOnlyList<StockAllocationInput>> AutoAllocateLots(
        StockItem stockItem,
        Product product,
        decimal quantity,
        IStockTraceabilityStore store)
    {
        var balances = store.ListBalances(stockItem.Id);
        var candidates = new List<LotCandidate>();
        foreach (var balance in balances)
        {
            var lot = store.GetLot(balance.ProductLotId);
            if (lot is null)
                continue;
            candidates.Add(new LotCandidate(
                lot.Id,
                lot.LotNumber,
                lot.ExpiryDate,
                store.GetLotFirstReceivedAt(lot.Id),
                balance.QuantityAvailable));
        }

        var ordered = LotAllocationPolicy.OrderForPicking(candidates, product.PickingPolicy);
        var allocated = LotAllocationPolicy.Allocate(
            quantity,
            ordered,
            _options.BlockExpiredLotsOnExit && product.HasExpiryTracking && _options.ExpiryTrackingEnabled,
            DateTime.UtcNow);
        if (allocated.IsFailure)
            return Result.Failure<IReadOnlyList<StockAllocationInput>>(allocated.Error);

        IReadOnlyList<StockAllocationInput> inputs = allocated.Value
            .Select(a => new StockAllocationInput(a.Quantity, ProductLotId: a.ProductLotId, LotNumber: a.LotNumber))
            .ToList();
        return Result.Success(inputs);
    }

    private static Result<ProductLot> GetOrCreateLot(
        Product product,
        StockAllocationInput allocation,
        IStockTraceabilityStore store)
    {
        if (allocation.ProductLotId is { } id)
        {
            var existingById = store.GetLot(id);
            if (existingById is null)
                return Result.Failure<ProductLot>(Error.NotFound("ProductLot", id));
            return Result.Success(existingById);
        }

        if (string.IsNullOrWhiteSpace(allocation.LotNumber))
            return Result.Failure<ProductLot>(Error.Validation("LotNumber", "Le numéro de lot est obligatoire"));

        var existing = store.FindLotByNumber(product.Id, allocation.LotNumber);
        if (existing is not null)
            return Result.Success(existing);

        var created = ProductLot.Create(
            product.Id,
            allocation.LotNumber,
            allocation.ExpiryDate,
            allocation.ManufacturedOn);
        if (created.IsFailure)
            return created;

        store.AddLot(created.Value);
        return created;
    }

    private static Result<ProductLot> ResolveLot(
        Guid productId,
        StockAllocationInput allocation,
        IStockTraceabilityStore store)
    {
        if (allocation.ProductLotId is { } id)
        {
            var byId = store.GetLot(id);
            return byId is null
                ? Result.Failure<ProductLot>(Error.NotFound("ProductLot", id))
                : Result.Success(byId);
        }

        if (string.IsNullOrWhiteSpace(allocation.LotNumber))
            return Result.Failure<ProductLot>(Error.Validation("LotNumber", "Le numéro de lot est obligatoire"));

        var byNumber = store.FindLotByNumber(productId, allocation.LotNumber);
        return byNumber is null
            ? Result.Failure<ProductLot>(Error.Validation("LotNumber", $"Lot '{allocation.LotNumber}' introuvable"))
            : Result.Success(byNumber);
    }

    private static void PersistDocumentAllocation(
        StockMutationRequest request,
        Guid productId,
        decimal quantity,
        Guid? lotId,
        Guid? serialId,
        decimal? unitCost,
        IStockTraceabilityStore store)
    {
        if (request.DocumentKind is null || request.DocumentLineId is null)
            return;

        var created = StockDocumentAllocation.Create(
            request.DocumentKind.Value,
            request.DocumentLineId.Value,
            productId,
            quantity,
            lotId,
            serialId,
            unitCost);
        if (created.IsSuccess)
            store.AddAllocation(created.Value);
    }
}
