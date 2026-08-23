using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>Identity of a product lot/batch (number + optional expiry), independent of warehouse qty.</summary>
public sealed class ProductLot : Entity
{
    public Guid ProductId { get; private set; }
    public string LotNumber { get; private set; } = null!;
    public DateTime? ExpiryDate { get; private set; }
    public DateTime? ManufacturedOn { get; private set; }
    public string? Notes { get; private set; }

    private ProductLot() { }

    public static Result<ProductLot> Create(
        Guid productId,
        string lotNumber,
        DateTime? expiryDate = null,
        DateTime? manufacturedOn = null,
        string? notes = null)
    {
        if (productId == Guid.Empty)
            return Result.Failure<ProductLot>(Error.Validation("ProductId", "L'identifiant du produit est obligatoire"));

        if (string.IsNullOrWhiteSpace(lotNumber))
            return Result.Failure<ProductLot>(Error.Validation("LotNumber", "Le numéro de lot est obligatoire"));

        var normalized = lotNumber.Trim().ToUpperInvariant();
        if (normalized.Length > 50)
            return Result.Failure<ProductLot>(Error.Validation("LotNumber", "Le numéro de lot ne peut pas dépasser 50 caractères"));

        if (manufacturedOn.HasValue && expiryDate.HasValue && manufacturedOn.Value.Date > expiryDate.Value.Date)
            return Result.Failure<ProductLot>(Error.Validation("ExpiryDate", "La date de péremption ne peut pas précéder la fabrication"));

        var lot = new ProductLot
        {
            ProductId = productId,
            LotNumber = normalized,
            ExpiryDate = expiryDate?.Date,
            ManufacturedOn = manufacturedOn?.Date,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };
        return Result.Success(lot);
    }

    public Result UpdateDates(DateTime? expiryDate, DateTime? manufacturedOn)
    {
        if (manufacturedOn.HasValue && expiryDate.HasValue && manufacturedOn.Value.Date > expiryDate.Value.Date)
            return Result.Failure(Error.Validation("ExpiryDate", "La date de péremption ne peut pas précéder la fabrication"));

        ExpiryDate = expiryDate?.Date;
        ManufacturedOn = manufacturedOn?.Date;
        return Result.Success();
    }

    public bool IsExpired(DateTime utcNow) =>
        ExpiryDate.HasValue && ExpiryDate.Value.Date < utcNow.Date;
}
