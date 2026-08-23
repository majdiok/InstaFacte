using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>Quantity of a lot in a given stock item (product × warehouse).</summary>
public sealed class StockLotBalance : Entity
{
    public Guid StockItemId { get; private set; }
    public Guid ProductLotId { get; private set; }
    public decimal QuantityOnHand { get; private set; }
    public decimal QuantityReserved { get; private set; }
    public decimal QuantityAvailable => QuantityOnHand - QuantityReserved;

    private StockLotBalance() { }

    public static Result<StockLotBalance> Create(Guid stockItemId, Guid productLotId)
    {
        if (stockItemId == Guid.Empty)
            return Result.Failure<StockLotBalance>(Error.Validation("StockItemId", "L'identifiant du stock est obligatoire"));
        if (productLotId == Guid.Empty)
            return Result.Failure<StockLotBalance>(Error.Validation("ProductLotId", "L'identifiant du lot est obligatoire"));

        return Result.Success(new StockLotBalance
        {
            StockItemId = stockItemId,
            ProductLotId = productLotId,
            QuantityOnHand = 0,
            QuantityReserved = 0
        });
    }

    public Result Increase(decimal quantity)
    {
        if (quantity <= 0)
            return Result.Failure(Error.Validation("Quantity", "La quantité doit être positive"));
        QuantityOnHand += quantity;
        return Result.Success();
    }

    public Result Decrease(decimal quantity)
    {
        if (quantity <= 0)
            return Result.Failure(Error.Validation("Quantity", "La quantité doit être positive"));
        if (quantity > QuantityAvailable)
            return Result.Failure(Error.Validation("Quantity",
                $"Stock de lot insuffisant. Disponible: {QuantityAvailable}, Demandé: {quantity}"));
        QuantityOnHand -= quantity;
        return Result.Success();
    }

    public Result SetOnHand(decimal newQuantity)
    {
        if (newQuantity < 0)
            return Result.Failure(Error.Validation("Quantity", "La quantité ne peut pas être négative"));
        if (QuantityReserved > newQuantity)
            return Result.Failure(Error.Validation("Quantity",
                "La quantité physique ne peut pas être inférieure à la quantité réservée"));
        QuantityOnHand = newQuantity;
        return Result.Success();
    }
}
