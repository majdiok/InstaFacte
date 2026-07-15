using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Represents a single line in a stock transfer between warehouses.
/// </summary>
public sealed class StockTransferLine : Entity
{
    public Guid StockTransferId { get; private set; }
    public StockTransfer StockTransfer { get; private set; } = null!;

    public int LineNumber { get; private set; }

    public Guid ProductId { get; private set; }
    public string ProductCode { get; private set; } = null!;
    public string ProductName { get; private set; } = null!;

    public decimal RequestedQuantity { get; private set; }
    public decimal TransferredQuantity { get; private set; }

    public string? Notes { get; private set; }

    private StockTransferLine() { }

    internal static Result<StockTransferLine> Create(
        StockTransfer transfer,
        int lineNumber,
        Product product,
        decimal requestedQuantity,
        string? notes = null)
    {
        if (requestedQuantity <= 0)
            return Result.Failure<StockTransferLine>(Error.Validation("RequestedQuantity",
                "La quantité demandée doit être supérieure à zéro"));

        var line = new StockTransferLine
        {
            StockTransferId = transfer.Id,
            StockTransfer = transfer,
            LineNumber = lineNumber,
            ProductId = product.Id,
            ProductCode = product.Code,
            ProductName = product.Name,
            RequestedQuantity = requestedQuantity,
            TransferredQuantity = 0,
            Notes = notes?.Trim()
        };

        return Result.Success(line);
    }

    internal void SetLineNumber(int lineNumber)
    {
        LineNumber = lineNumber;
    }

    internal Result RecordTransfer(decimal quantity)
    {
        if (quantity <= 0)
            return Result.Failure(Error.Validation("Quantity", "La quantité transférée doit être positive"));

        if (quantity > RequestedQuantity)
            return Result.Failure(Error.Validation("Quantity",
                $"La quantité transférée ({quantity}) ne peut pas dépasser la quantité demandée ({RequestedQuantity})"));

        TransferredQuantity = quantity;
        return Result.Success();
    }

    public bool IsFullyTransferred => TransferredQuantity >= RequestedQuantity;
}
