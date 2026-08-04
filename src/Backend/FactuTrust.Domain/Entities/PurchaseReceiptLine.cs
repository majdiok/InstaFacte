using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Line item on a purchase receipt (bon de réception d'achat).
/// Product data is snapshotted at creation time.
/// </summary>
public sealed class PurchaseReceiptLine : Entity
{
    public Guid PurchaseReceiptId { get; private set; }
    public PurchaseReceipt PurchaseReceipt { get; private set; } = null!;

    public int LineNumber { get; private set; }

    public Guid? PurchaseOrderLineId { get; private set; }

    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;

    public string ProductCode { get; private set; } = null!;
    public string ProductName { get; private set; } = null!;
    public string? ProductDescription { get; private set; }
    public string? Unit { get; private set; }

    /// <summary>Ordered quantity snapshot from the purchase order line (0 if manual).</summary>
    public decimal OrderedQuantity { get; private set; }

    /// <summary>Quantity received on THIS receipt.</summary>
    public decimal ReceivedQuantity { get; private set; }

    public decimal InvoicedQuantity { get; private set; }

    public decimal ReceivedNotInvoicedQuantity => Math.Max(0m, ReceivedQuantity - InvoicedQuantity);

    public bool IsFullyInvoiced => ReceivedQuantity > 0 && InvoicedQuantity >= ReceivedQuantity;

    public Money UnitPrice { get; private set; } = null!;
    public decimal? DiscountPercent { get; private set; }
    public VatRate VatRate { get; private set; }

    public Money SubTotal { get; private set; } = null!;
    public Money VatAmount { get; private set; } = null!;
    public Money Total { get; private set; } = null!;

    private PurchaseReceiptLine() { }

    internal static Result<PurchaseReceiptLine> Create(
        PurchaseReceipt receipt,
        int lineNumber,
        Product product,
        decimal receivedQuantity,
        Money unitPrice,
        decimal orderedQuantity = 0,
        Guid? purchaseOrderLineId = null,
        decimal? discountPercent = null)
    {
        if (product is null)
            return Result.Failure<PurchaseReceiptLine>(Error.Validation("Product", "Le produit est obligatoire"));

        if (receivedQuantity <= 0)
            return Result.Failure<PurchaseReceiptLine>(
                Error.Validation("ReceivedQuantity", "La quantité reçue doit être supérieure à zéro"));

        if (orderedQuantity < 0)
            return Result.Failure<PurchaseReceiptLine>(
                Error.Validation("OrderedQuantity", "La quantité commandée ne peut pas être négative"));

        if (discountPercent is < 0 or > 100)
            return Result.Failure<PurchaseReceiptLine>(
                Error.Validation("DiscountPercent", "La remise doit être comprise entre 0 et 100 %"));

        var line = new PurchaseReceiptLine
        {
            PurchaseReceiptId = receipt.Id,
            PurchaseReceipt = receipt,
            LineNumber = lineNumber,
            PurchaseOrderLineId = purchaseOrderLineId,
            ProductId = product.Id,
            Product = product,
            ProductCode = product.Code,
            ProductName = product.Name,
            ProductDescription = product.Description,
            Unit = product.Unit,
            OrderedQuantity = orderedQuantity,
            ReceivedQuantity = receivedQuantity,
            InvoicedQuantity = 0,
            UnitPrice = unitPrice,
            DiscountPercent = discountPercent,
            VatRate = product.VatRate
        };

        line.Calculate();
        return Result.Success(line);
    }

    internal Result Update(decimal receivedQuantity, Money? unitPrice = null, decimal? discountPercent = null)
    {
        if (receivedQuantity <= 0)
            return Result.Failure(Error.Validation("ReceivedQuantity", "La quantité reçue doit être supérieure à zéro"));

        if (discountPercent is < 0 or > 100)
            return Result.Failure(Error.Validation("DiscountPercent", "La remise doit être comprise entre 0 et 100 %"));

        ReceivedQuantity = receivedQuantity;
        if (unitPrice is not null)
            UnitPrice = unitPrice;
        DiscountPercent = discountPercent;
        Calculate();
        return Result.Success();
    }

    internal void SetLineNumber(int lineNumber) => LineNumber = lineNumber;

    internal Result RecordInvoiced(decimal invoicedQuantity)
    {
        if (invoicedQuantity <= 0)
            return Result.Failure(Error.Validation("InvoicedQuantity", "La quantité facturée doit être positive"));

        if (InvoicedQuantity + invoicedQuantity > ReceivedQuantity)
            return Result.Failure(Error.Validation("InvoicedQuantity",
                $"Facturation supérieure au reste reçu non facturé sur la ligne {LineNumber} (reste : {ReceivedNotInvoicedQuantity})"));

        InvoicedQuantity += invoicedQuantity;
        return Result.Success();
    }

    internal Result ReverseInvoiced(decimal quantity)
    {
        if (quantity <= 0)
            return Result.Failure(Error.Validation("InvoicedQuantity", "La quantité à annuler doit être positive"));

        if (quantity > InvoicedQuantity)
            return Result.Failure(Error.Validation("InvoicedQuantity",
                $"Impossible d'annuler {quantity} : seule {InvoicedQuantity} a été facturée sur cette ligne"));

        InvoicedQuantity -= quantity;
        return Result.Success();
    }

    private void Calculate()
    {
        var gross = UnitPrice.Multiply(ReceivedQuantity);
        var discountFactor = 1m - ((DiscountPercent ?? 0m) / 100m);
        SubTotal = gross.Multiply(discountFactor);
        VatAmount = SubTotal.ApplyPercentage(VatRate.ToDecimal());
        Total = SubTotal.Add(VatAmount);
    }
}
