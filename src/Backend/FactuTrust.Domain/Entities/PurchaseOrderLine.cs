using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Represents a line item on a purchase order.
/// Follows the same pattern as QuoteLine.
/// </summary>
public sealed class PurchaseOrderLine : Entity
{
    public Guid PurchaseOrderId { get; private set; }
    public PurchaseOrder PurchaseOrder { get; private set; } = null!;

    public int LineNumber { get; private set; }

    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;

    public string ProductCode { get; private set; } = null!;
    public string ProductName { get; private set; } = null!;
    public string? ProductDescription { get; private set; }

    public decimal Quantity { get; private set; }
    public decimal ReceivedQuantity { get; private set; }
    public string? Unit { get; private set; }
    public Money UnitPrice { get; private set; } = null!;
    public VatRate VatRate { get; private set; }

    public Money SubTotal { get; private set; } = null!;
    public Money VatAmount { get; private set; } = null!;
    public Money Total { get; private set; } = null!;

    /// <summary>
    /// Remaining quantity to receive.
    /// </summary>
    public decimal PendingQuantity => Quantity - ReceivedQuantity;

    /// <summary>
    /// Whether this line has been fully received.
    /// </summary>
    public bool IsFullyReceived => ReceivedQuantity >= Quantity;

    private PurchaseOrderLine() { }

    internal static Result<PurchaseOrderLine> Create(
        PurchaseOrder purchaseOrder,
        int lineNumber,
        Product product,
        decimal quantity,
        Money unitPrice)
    {
        if (quantity <= 0)
            return Result.Failure<PurchaseOrderLine>(Error.Validation("Quantity", "La quantité doit être supérieure à zéro"));

        var line = new PurchaseOrderLine
        {
            PurchaseOrderId = purchaseOrder.Id,
            PurchaseOrder = purchaseOrder,
            LineNumber = lineNumber,
            ProductId = product.Id,
            Product = product,
            ProductCode = product.Code,
            ProductName = product.Name,
            ProductDescription = product.Description,
            Quantity = quantity,
            ReceivedQuantity = 0,
            Unit = product.Unit,
            UnitPrice = unitPrice,
            VatRate = product.VatRate
        };

        line.Calculate();

        return Result.Success(line);
    }

    internal Result Update(decimal quantity, Money? customUnitPrice = null)
    {
        if (quantity <= 0)
            return Result.Failure(Error.Validation("Quantity", "La quantité doit être supérieure à zéro"));

        Quantity = quantity;

        if (customUnitPrice is not null)
            UnitPrice = customUnitPrice;

        Calculate();

        return Result.Success();
    }

    /// <summary>
    /// Records the reception of goods for this line.
    /// </summary>
    internal Result RecordReception(decimal receivedQuantity)
    {
        if (receivedQuantity <= 0)
            return Result.Failure(Error.Validation("ReceivedQuantity", "La quantité reçue doit être supérieure à zéro"));

        if (ReceivedQuantity + receivedQuantity > Quantity)
            return Result.Failure(Error.Validation("ReceivedQuantity",
                $"La quantité reçue ({ReceivedQuantity + receivedQuantity}) dépasse la quantité commandée ({Quantity})"));

        ReceivedQuantity += receivedQuantity;

        return Result.Success();
    }

    internal void SetLineNumber(int lineNumber)
    {
        LineNumber = lineNumber;
    }

    private void Calculate()
    {
        SubTotal = UnitPrice.Multiply(Quantity);
        VatAmount = SubTotal.ApplyPercentage(VatRate.ToDecimal());
        Total = SubTotal.Add(VatAmount);
    }
}
