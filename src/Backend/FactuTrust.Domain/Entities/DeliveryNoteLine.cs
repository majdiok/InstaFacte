using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Represents a line item in a delivery note.
/// Each line MUST be linked to a Product. Product data is snapshotted at creation time
/// so the delivery note remains immutable even if the product catalog changes later.
/// </summary>
public sealed class DeliveryNoteLine : Entity
{
    public Guid DeliveryNoteId { get; private set; }
    public DeliveryNote DeliveryNote { get; private set; } = null!;

    public int LineNumber { get; private set; }

    /// <summary>
    /// Mandatory link to the source Product.
    /// </summary>
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;

    /// <summary>
    /// Snapshot of the product code at creation time.
    /// </summary>
    public string ProductCode { get; private set; } = null!;

    /// <summary>
    /// Snapshot of product name at creation time.
    /// </summary>
    public string Designation { get; private set; } = null!;

    /// <summary>
    /// Snapshot of product description at creation time.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Snapshot of the product unit at creation time.
    /// </summary>
    public string Unit { get; private set; } = null!;

    /// <summary>
    /// Snapshot of the product unit price HT at creation time (decimal amount in TND).
    /// This value is NEVER recalculated after creation.
    /// </summary>
    public decimal UnitPriceHT { get; private set; }

    /// <summary>
    /// Snapshot of the product VAT rate (percent integer: 0, 7, 13, 19) at creation time.
    /// </summary>
    public int VatRatePercent { get; private set; }

    /// <summary>
    /// Quantity ordered/expected to be delivered.
    /// </summary>
    public decimal OrderedQuantity { get; private set; }

    /// <summary>
    /// Actual quantity delivered. Set during delivery confirmation.
    /// </summary>
    public decimal DeliveredQuantity { get; private set; }

    /// <summary>
    /// Quantity rejected by client during delivery.
    /// </summary>
    public decimal RejectedQuantity { get; private set; }

    /// <summary>
    /// Reason for rejection if any quantity was rejected.
    /// </summary>
    public string? RejectionReason { get; private set; }

    public string? Notes { get; private set; }

    private DeliveryNoteLine() { }

    /// <summary>
    /// Creates a delivery note line from a Product, snapshotting all relevant product data.
    /// </summary>
    public static Result<DeliveryNoteLine> Create(
        DeliveryNote deliveryNote,
        int lineNumber,
        Product product,
        decimal orderedQuantity,
        string? notes = null)
    {
        if (product is null)
            return Result.Failure<DeliveryNoteLine>(
                Error.Validation("Product", "Le produit est obligatoire"));

        if (orderedQuantity <= 0)
            return Result.Failure<DeliveryNoteLine>(
                Error.Validation("OrderedQuantity", "La quantité doit être supérieure à zéro"));

        if (product.UnitPrice.Amount <= 0)
            return Result.Failure<DeliveryNoteLine>(
                Error.Validation("UnitPrice", "Le prix unitaire du produit doit être supérieur à zéro"));

        var line = new DeliveryNoteLine
        {
            DeliveryNoteId = deliveryNote.Id,
            DeliveryNote = deliveryNote,
            LineNumber = lineNumber,
            ProductId = product.Id,
            Product = product,
            ProductCode = product.Code,
            Designation = product.Name,
            Description = product.Description,
            Unit = product.Unit ?? "unité",
            UnitPriceHT = product.UnitPrice.Amount,
            VatRatePercent = (int)product.VatRate,
            OrderedQuantity = orderedQuantity,
            DeliveredQuantity = 0,
            RejectedQuantity = 0,
            Notes = notes?.Trim()
        };

        return Result.Success(line);
    }

    /// <summary>
    /// Updates only the editable fields (quantity, notes). Product snapshot is immutable.
    /// </summary>
    public Result Update(decimal orderedQuantity, string? notes = null)
    {
        if (!DeliveryNote.Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce bon de livraison ne peut plus être modifié"));

        if (orderedQuantity <= 0)
            return Result.Failure(Error.Validation("OrderedQuantity", "La quantité doit être supérieure à zéro"));

        OrderedQuantity = orderedQuantity;
        Notes = notes?.Trim();

        return Result.Success();
    }

    /// <summary>
    /// Records the actual delivery quantities for this line.
    /// Blocked if the parent delivery note is locked (Delivered, Invoiced, Refused).
    /// </summary>
    public Result RecordDelivery(
        decimal deliveredQuantity,
        decimal rejectedQuantity = 0,
        string? rejectionReason = null)
    {
        if (DeliveryNote.Status.IsLocked())
            return Result.Failure(Error.Validation("Status", 
                "Ce bon de livraison est verrouillé et ne peut plus être modifié"));

        if (deliveredQuantity < 0)
            return Result.Failure(Error.Validation("DeliveredQuantity", "La quantité livrée ne peut pas être négative"));

        if (rejectedQuantity < 0)
            return Result.Failure(Error.Validation("RejectedQuantity", "La quantité refusée ne peut pas être négative"));

        if (deliveredQuantity + rejectedQuantity > OrderedQuantity)
            return Result.Failure(Error.Validation("Quantity", 
                "La somme des quantités livrées et refusées ne peut pas dépasser la quantité commandée"));

        if (rejectedQuantity > 0 && string.IsNullOrWhiteSpace(rejectionReason))
            return Result.Failure(Error.Validation("RejectionReason", 
                "Le motif de refus est obligatoire si des articles sont refusés"));

        DeliveredQuantity = deliveredQuantity;
        RejectedQuantity = rejectedQuantity;
        RejectionReason = rejectionReason?.Trim();

        return Result.Success();
    }

    public void SetLineNumber(int lineNumber)
    {
        LineNumber = lineNumber;
    }

    /// <summary>
    /// Computed total HT for this line (UnitPriceHT × OrderedQuantity).
    /// </summary>
    public decimal TotalHT => Math.Round(UnitPriceHT * OrderedQuantity, 3);

    /// <summary>
    /// Computed total VAT for this line.
    /// </summary>
    public decimal TotalVAT => Math.Round(TotalHT * VatRatePercent / 100m, 3);

    /// <summary>
    /// Computed total TTC for this line (HT + VAT).
    /// </summary>
    public decimal TotalTTC => Math.Round(TotalHT + TotalVAT, 3);

    /// <summary>
    /// Total HT based on delivered quantity only — used for invoicing.
    /// </summary>
    public decimal DeliveredTotalHT => Math.Round(UnitPriceHT * DeliveredQuantity, 3);

    /// <summary>
    /// Total TTC based on delivered quantity only — used for invoicing.
    /// </summary>
    public decimal DeliveredTotalTTC => Math.Round(DeliveredTotalHT * (1 + VatRatePercent / 100m), 3);

    /// <summary>
    /// Returns true if all ordered quantity was delivered.
    /// </summary>
    public bool IsFullyDelivered => DeliveredQuantity >= OrderedQuantity;

    /// <summary>
    /// Returns the pending quantity (ordered - delivered - rejected).
    /// </summary>
    public decimal PendingQuantity => Math.Max(0, OrderedQuantity - DeliveredQuantity - RejectedQuantity);
}
