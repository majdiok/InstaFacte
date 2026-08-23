using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities;

public sealed class ProductSerial : Entity
{
    public Guid ProductId { get; private set; }
    public string SerialNumber { get; private set; } = null!;
    public Guid? ProductLotId { get; private set; }
    public Guid? WarehouseId { get; private set; }
    public SerialStatus Status { get; private set; }
    public DateTime? ExpiryDate { get; private set; }

    private ProductSerial() { }

    public static Result<ProductSerial> Create(
        Guid productId,
        string serialNumber,
        Guid warehouseId,
        Guid? productLotId = null,
        DateTime? expiryDate = null)
    {
        if (productId == Guid.Empty)
            return Result.Failure<ProductSerial>(Error.Validation("ProductId", "L'identifiant du produit est obligatoire"));
        if (warehouseId == Guid.Empty)
            return Result.Failure<ProductSerial>(Error.Validation("WarehouseId", "L'entrepôt est obligatoire"));
        if (string.IsNullOrWhiteSpace(serialNumber))
            return Result.Failure<ProductSerial>(Error.Validation("SerialNumber", "Le numéro de série est obligatoire"));

        var normalized = serialNumber.Trim().ToUpperInvariant();
        if (normalized.Length > 80)
            return Result.Failure<ProductSerial>(Error.Validation("SerialNumber", "Le numéro de série ne peut pas dépasser 80 caractères"));

        return Result.Success(new ProductSerial
        {
            ProductId = productId,
            SerialNumber = normalized,
            WarehouseId = warehouseId,
            ProductLotId = productLotId,
            Status = SerialStatus.InStock,
            ExpiryDate = expiryDate?.Date
        });
    }

    public Result MarkSold()
    {
        if (Status != SerialStatus.InStock)
            return Result.Failure(Error.Validation("Status", $"Le numéro de série {SerialNumber} n'est pas en stock"));
        Status = SerialStatus.Sold;
        WarehouseId = null;
        return Result.Success();
    }

    public Result MarkInTransit()
    {
        if (Status != SerialStatus.InStock)
            return Result.Failure(Error.Validation("Status", $"Le numéro de série {SerialNumber} n'est pas en stock"));
        Status = SerialStatus.InTransit;
        return Result.Success();
    }

    public Result MarkReceived(Guid warehouseId)
    {
        if (warehouseId == Guid.Empty)
            return Result.Failure(Error.Validation("WarehouseId", "L'entrepôt est obligatoire"));
        Status = SerialStatus.InStock;
        WarehouseId = warehouseId;
        return Result.Success();
    }

    public Result RestoreToStock(Guid warehouseId)
    {
        if (warehouseId == Guid.Empty)
            return Result.Failure(Error.Validation("WarehouseId", "L'entrepôt est obligatoire"));
        Status = SerialStatus.InStock;
        WarehouseId = warehouseId;
        return Result.Success();
    }

    public Result MarkScrapped()
    {
        Status = SerialStatus.Scrapped;
        WarehouseId = null;
        return Result.Success();
    }
}
