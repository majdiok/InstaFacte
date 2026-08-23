using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>FIFO/LIFO cost layer remaining on a stock item.</summary>
public sealed class StockValuationLayer : Entity
{
    public Guid StockItemId { get; private set; }
    public Guid? ProductLotId { get; private set; }
    public DateTime ReceivedAt { get; private set; }
    public decimal OriginalQuantity { get; private set; }
    public decimal RemainingQuantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public string? SourceReference { get; private set; }

    public decimal RemainingValue => RemainingQuantity * UnitCost;

    private StockValuationLayer() { }

    public static Result<StockValuationLayer> Create(
        Guid stockItemId,
        decimal quantity,
        decimal unitCost,
        DateTime receivedAt,
        Guid? productLotId = null,
        string? sourceReference = null)
    {
        if (stockItemId == Guid.Empty)
            return Result.Failure<StockValuationLayer>(Error.Validation("StockItemId", "L'identifiant du stock est obligatoire"));
        if (quantity <= 0)
            return Result.Failure<StockValuationLayer>(Error.Validation("Quantity", "La quantité doit être positive"));
        if (unitCost < 0)
            return Result.Failure<StockValuationLayer>(Error.Validation("UnitCost", "Le coût unitaire ne peut pas être négatif"));

        return Result.Success(new StockValuationLayer
        {
            StockItemId = stockItemId,
            ProductLotId = productLotId,
            ReceivedAt = receivedAt,
            OriginalQuantity = quantity,
            RemainingQuantity = quantity,
            UnitCost = unitCost,
            SourceReference = string.IsNullOrWhiteSpace(sourceReference) ? null : sourceReference.Trim()
        });
    }

    public Result Consume(decimal quantity)
    {
        if (quantity <= 0)
            return Result.Failure(Error.Validation("Quantity", "La quantité doit être positive"));
        if (quantity > RemainingQuantity)
            return Result.Failure(Error.Validation("Quantity",
                $"Couche de valorisation insuffisante. Restant: {RemainingQuantity}, Demandé: {quantity}"));
        RemainingQuantity -= quantity;
        return Result.Success();
    }

    public Result IncreaseRemaining(decimal quantity)
    {
        if (quantity <= 0)
            return Result.Failure(Error.Validation("Quantity", "La quantité doit être positive"));
        RemainingQuantity += quantity;
        OriginalQuantity += quantity;
        return Result.Success();
    }
}
