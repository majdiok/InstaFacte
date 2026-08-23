using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Line on a generic stock voucher. Product identity is snapshotted for PDF/history.
/// </summary>
public sealed class StockVoucherLine : Entity
{
    public Guid StockVoucherId { get; private set; }
    public StockVoucher StockVoucher { get; private set; } = null!;

    public int LineNumber { get; private set; }

    public Guid ProductId { get; private set; }
    public string ProductCode { get; private set; } = null!;
    public string ProductName { get; private set; } = null!;
    public string? Unit { get; private set; }

    public decimal Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public string? Notes { get; private set; }

    public decimal LineValue => Quantity * UnitCost;

    private StockVoucherLine() { }

    internal static Result<StockVoucherLine> Create(
        StockVoucher voucher,
        int lineNumber,
        Product product,
        decimal quantity,
        decimal unitCost,
        string? notes = null)
    {
        if (product is null)
            return Result.Failure<StockVoucherLine>(Error.Validation("Product", "Le produit est obligatoire"));

        if (!product.IsStockManaged)
            return Result.Failure<StockVoucherLine>(Error.Validation("Product",
                $"Le produit '{product.Name}' n'a pas la gestion de stock activée"));

        if (quantity <= 0)
            return Result.Failure<StockVoucherLine>(Error.Validation("Quantity",
                "La quantité doit être supérieure à zéro"));

        if (unitCost < 0)
            return Result.Failure<StockVoucherLine>(Error.Validation("UnitCost",
                "Le coût unitaire ne peut pas être négatif"));

        var line = new StockVoucherLine
        {
            StockVoucherId = voucher.Id,
            StockVoucher = voucher,
            LineNumber = lineNumber,
            ProductId = product.Id,
            ProductCode = product.Code,
            ProductName = product.Name,
            Unit = product.Unit,
            Quantity = quantity,
            UnitCost = unitCost,
            Notes = notes?.Trim()
        };

        return Result.Success(line);
    }

    internal Result Update(decimal quantity, decimal unitCost, string? notes)
    {
        if (quantity <= 0)
            return Result.Failure(Error.Validation("Quantity", "La quantité doit être supérieure à zéro"));

        if (unitCost < 0)
            return Result.Failure(Error.Validation("UnitCost", "Le coût unitaire ne peut pas être négatif"));

        Quantity = quantity;
        UnitCost = unitCost;
        Notes = notes?.Trim();
        return Result.Success();
    }

    internal void SetLineNumber(int lineNumber) => LineNumber = lineNumber;
}
