using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Line of a sales return note. Product and pricing are snapshotted from the source BL line.
/// </summary>
public sealed class SalesReturnNoteLine : Entity
{
    public Guid SalesReturnNoteId { get; private set; }
    public SalesReturnNote SalesReturnNote { get; private set; } = null!;

    public Guid DeliveryNoteLineId { get; private set; }

    public int LineNumber { get; private set; }

    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;

    public string ProductCode { get; private set; } = null!;
    public string Designation { get; private set; } = null!;
    public string? Description { get; private set; }
    public string Unit { get; private set; } = null!;
    public decimal UnitPriceHT { get; private set; }
    public int VatRatePercent { get; private set; }
    public decimal? DiscountPercent { get; private set; }
    public bool IsFodecApplicable { get; private set; }
    public decimal FodecRatePercent { get; private set; }

    public decimal ReturnedQuantity { get; private set; }
    public string? Notes { get; private set; }

    private SalesReturnNoteLine() { }

    internal static Result<SalesReturnNoteLine> Create(
        SalesReturnNote parent,
        int lineNumber,
        DeliveryNoteLine source,
        decimal returnedQuantity,
        string? notes = null)
    {
        if (source is null)
            return Result.Failure<SalesReturnNoteLine>(
                Error.Validation("DeliveryNoteLine", "La ligne de bon de livraison est obligatoire"));

        if (returnedQuantity <= 0)
            return Result.Failure<SalesReturnNoteLine>(
                Error.Validation("ReturnedQuantity", "La quantité retournée doit être supérieure à zéro"));

        var line = new SalesReturnNoteLine
        {
            SalesReturnNoteId = parent.Id,
            SalesReturnNote = parent,
            DeliveryNoteLineId = source.Id,
            LineNumber = lineNumber,
            ProductId = source.ProductId,
            Product = source.Product,
            ProductCode = source.ProductCode,
            Designation = source.Designation,
            Description = source.Description,
            Unit = source.Unit,
            UnitPriceHT = source.UnitPriceHT,
            VatRatePercent = source.VatRatePercent,
            DiscountPercent = source.DiscountPercent,
            IsFodecApplicable = source.IsFodecApplicable,
            FodecRatePercent = source.FodecRatePercent,
            ReturnedQuantity = returnedQuantity,
            Notes = notes?.Trim()
        };

        return Result.Success(line);
    }

    internal Result Update(decimal returnedQuantity, string? notes = null)
    {
        if (!SalesReturnNote.Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce bon de retour ne peut plus être modifié"));

        if (returnedQuantity <= 0)
            return Result.Failure(Error.Validation("ReturnedQuantity", "La quantité retournée doit être supérieure à zéro"));

        ReturnedQuantity = returnedQuantity;
        Notes = notes?.Trim();
        return Result.Success();
    }

    internal void SetLineNumber(int lineNumber) => LineNumber = lineNumber;

    public void ClearParentNavigationsForPersistence()
    {
        SalesReturnNote = null!;
    }

    public void ReplaceProductForPersistence(Product product) => Product = product;

    public decimal DiscountAmount => DiscountFor(ReturnedQuantity);
    public decimal TotalHT => SubTotalFor(ReturnedQuantity);
    public decimal FodecAmount => FodecFor(ReturnedQuantity);
    public decimal TotalVAT => VatFor(ReturnedQuantity);
    public decimal TotalTTC => Math.Round(TotalHT + FodecAmount + TotalVAT, 3);

    private decimal GrossFor(decimal quantity) => Math.Round(UnitPriceHT * quantity, 3);

    private decimal DiscountFor(decimal quantity) =>
        DiscountPercent is > 0
            ? Math.Round(GrossFor(quantity) * DiscountPercent.Value / 100m, 3)
            : 0m;

    private decimal SubTotalFor(decimal quantity) =>
        Math.Round(GrossFor(quantity) - DiscountFor(quantity), 3);

    private decimal FodecFor(decimal quantity) =>
        IsFodecApplicable && FodecRatePercent > 0
            ? Math.Round(SubTotalFor(quantity) * FodecRatePercent / 100m, 3)
            : 0m;

    private decimal VatFor(decimal quantity) =>
        Math.Round(Math.Round(SubTotalFor(quantity) + FodecFor(quantity), 3) * VatRatePercent / 100m, 3);
}
