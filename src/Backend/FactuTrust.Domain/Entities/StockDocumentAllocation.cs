using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities;

/// <summary>Lot/serial split attached to a commercial or stock document line.</summary>
public sealed class StockDocumentAllocation : Entity
{
    public StockDocumentKind DocumentKind { get; private set; }
    public Guid DocumentLineId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid? ProductLotId { get; private set; }
    public Guid? SerialId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal? UnitCost { get; private set; }

    private StockDocumentAllocation() { }

    public static Result<StockDocumentAllocation> Create(
        StockDocumentKind documentKind,
        Guid documentLineId,
        Guid productId,
        decimal quantity,
        Guid? productLotId = null,
        Guid? serialId = null,
        decimal? unitCost = null)
    {
        if (documentLineId == Guid.Empty)
            return Result.Failure<StockDocumentAllocation>(Error.Validation("DocumentLineId", "La ligne document est obligatoire"));
        if (productId == Guid.Empty)
            return Result.Failure<StockDocumentAllocation>(Error.Validation("ProductId", "Le produit est obligatoire"));
        if (quantity <= 0)
            return Result.Failure<StockDocumentAllocation>(Error.Validation("Quantity", "La quantité doit être positive"));
        if (unitCost is < 0)
            return Result.Failure<StockDocumentAllocation>(Error.Validation("UnitCost", "Le coût unitaire ne peut pas être négatif"));

        return Result.Success(new StockDocumentAllocation
        {
            DocumentKind = documentKind,
            DocumentLineId = documentLineId,
            ProductId = productId,
            ProductLotId = productLotId,
            SerialId = serialId,
            Quantity = quantity,
            UnitCost = unitCost
        });
    }
}
