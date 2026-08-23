using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Events;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Generic stock voucher (bon d'entrée / bon de sortie). Stock is applied only on validation.
/// </summary>
public sealed class StockVoucher : AggregateRoot
{
    public StockVoucherNumber Number { get; private set; } = null!;
    public StockVoucherKind Kind { get; private set; }
    public StockVoucherStatus Status { get; private set; }
    public DateTime VoucherDate { get; private set; }

    public Guid WarehouseId { get; private set; }
    public Warehouse Warehouse { get; private set; } = null!;

    public MovementReason Reason { get; private set; }
    public string? ExternalReference { get; private set; }
    public string? Notes { get; private set; }

    public DateTime? ValidatedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    private readonly List<StockVoucherLine> _lines = new();
    public IReadOnlyCollection<StockVoucherLine> Lines => _lines.AsReadOnly();

    public decimal TotalQuantity => _lines.Sum(l => l.Quantity);
    public decimal TotalValue => _lines.Sum(l => l.LineValue);

    public string StockMovementReference => $"{Kind.DefaultPrefix()} {Number.Value}";

    public string StockReversalReference => $"ANNUL {Kind.DefaultPrefix()} {Number.Value}";

    private StockVoucher() { }

    public static Result<StockVoucher> Create(
        StockVoucherNumber number,
        StockVoucherKind kind,
        Warehouse warehouse,
        DateTime voucherDate,
        MovementReason reason,
        string? externalReference = null,
        string? notes = null)
    {
        if (number is null)
            return Result.Failure<StockVoucher>(Error.Validation("Number", "Le numéro est obligatoire"));

        if (warehouse is null)
            return Result.Failure<StockVoucher>(Error.Validation("Warehouse", "L'entrepôt est obligatoire"));

        if (!warehouse.IsActive)
            return Result.Failure<StockVoucher>(Error.Validation("Warehouse",
                "L'entrepôt sélectionné n'est pas actif"));

        if (!kind.IsAllowedReason(reason))
            return Result.Failure<StockVoucher>(Error.Validation("Reason",
                kind == StockVoucherKind.Entry
                    ? "Motif invalide pour un bon d'entrée (utilisez Achats > Bons de réception, Transferts ou Inventaire pour les autres cas)"
                    : "Motif invalide pour un bon de sortie (utilisez la facturation, le bon de livraison, les transferts ou l'inventaire pour les autres cas)"));

        if (externalReference is { Length: > 100 })
            return Result.Failure<StockVoucher>(Error.Validation("ExternalReference",
                "La référence externe ne peut pas dépasser 100 caractères"));

        var voucher = new StockVoucher
        {
            Number = number,
            Kind = kind,
            Status = StockVoucherStatus.Draft,
            VoucherDate = voucherDate.Date,
            WarehouseId = warehouse.Id,
            Reason = reason,
            ExternalReference = TrimToNull(externalReference),
            Notes = notes?.Trim()
        };

        voucher.AddDomainEvent(new StockVoucherCreatedEvent(voucher.Id, number.Value, kind));
        return Result.Success(voucher);
    }

    public Result AddLine(Product product, decimal quantity, decimal unitCost, string? notes = null)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce bon de stock ne peut plus être modifié"));

        var sellable = ProductCommercialGuards.EnsureCanAppearOnDocument(product);
        if (sellable.IsFailure)
            return sellable;

        if (_lines.Any(l => l.ProductId == product.Id))
            return Result.Failure(Error.Validation("ProductId",
                $"Le produit '{product.Name}' est déjà sur ce bon"));

        var lineResult = StockVoucherLine.Create(this, _lines.Count + 1, product, quantity, unitCost, notes);
        if (lineResult.IsFailure)
            return Result.Failure(lineResult.Error);

        _lines.Add(lineResult.Value);
        return Result.Success();
    }

    public Result UpdateLine(Guid lineId, decimal quantity, decimal unitCost, string? notes)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce bon de stock ne peut plus être modifié"));

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            return Result.Failure(Error.NotFound("StockVoucherLine", lineId));

        return line.Update(quantity, unitCost, notes);
    }

    public Result RemoveLine(Guid lineId)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce bon de stock ne peut plus être modifié"));

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            return Result.Failure(Error.NotFound("StockVoucherLine", lineId));

        _lines.Remove(line);
        RenumberLines();
        return Result.Success();
    }

    public Result ClearLines()
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce bon de stock ne peut plus être modifié"));

        _lines.Clear();
        return Result.Success();
    }

    public Result UpdateHeader(
        DateTime voucherDate,
        Guid warehouseId,
        MovementReason reason,
        string? externalReference,
        string? notes)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce bon de stock ne peut plus être modifié"));

        if (!Kind.IsAllowedReason(reason))
            return Result.Failure(Error.Validation("Reason", "Motif invalide pour ce type de bon"));

        if (externalReference is { Length: > 100 })
            return Result.Failure(Error.Validation("ExternalReference",
                "La référence externe ne peut pas dépasser 100 caractères"));

        if (warehouseId == Guid.Empty)
            return Result.Failure(Error.Validation("WarehouseId", "L'entrepôt est obligatoire"));

        VoucherDate = voucherDate.Date;
        WarehouseId = warehouseId;
        Reason = reason;
        ExternalReference = TrimToNull(externalReference);
        Notes = notes?.Trim();
        return Result.Success();
    }

    /// <summary>
    /// Domain-level validation. Stock application is done by the application service afterwards.
    /// </summary>
    public Result MarkValidated()
    {
        if (!Status.CanBeValidated())
            return Result.Failure(Error.Validation("Status",
                "Ce bon de stock ne peut pas être validé dans son état actuel"));

        if (WarehouseId == Guid.Empty)
            return Result.Failure(Error.Validation("WarehouseId", "L'entrepôt est obligatoire"));

        if (_lines.Count == 0 || _lines.All(l => l.Quantity <= 0))
            return Result.Failure(Error.Validation("Lines", "Aucune quantité à enregistrer"));

        Status = StockVoucherStatus.Validated;
        ValidatedAt = DateTime.UtcNow;

        AddDomainEvent(new StockVoucherValidatedEvent(
            Id, Number.Value, Kind, WarehouseId, _lines.Count));

        return Result.Success();
    }

    /// <summary>
    /// Domain-level cancel. Stock reversal for a previously validated voucher is handled by the application layer.
    /// </summary>
    public Result Cancel(string reason)
    {
        if (!Status.CanBeCancelled())
            return Result.Failure(Error.Validation("Status", "Ce bon de stock ne peut pas être annulé"));

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(Error.Validation("CancellationReason",
                "Le motif d'annulation est obligatoire"));

        var wasValidated = Status == StockVoucherStatus.Validated;
        Status = StockVoucherStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        CancellationReason = reason.Trim();

        AddDomainEvent(new StockVoucherCancelledEvent(Id, Number.Value, CancellationReason, wasValidated));
        return Result.Success();
    }

    private void RenumberLines()
    {
        for (var i = 0; i < _lines.Count; i++)
            _lines[i].SetLineNumber(i + 1);
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
