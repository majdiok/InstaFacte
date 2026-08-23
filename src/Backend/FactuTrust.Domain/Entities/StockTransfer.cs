using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Events;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Represents a stock transfer between two warehouses.
/// Manages the full lifecycle: Draft -> Confirmed -> InTransit -> Completed.
/// </summary>
public sealed class StockTransfer : AggregateRoot
{
    public StockTransferNumber Number { get; private set; } = null!;
    public DateTime TransferDate { get; private set; }
    public StockTransferStatus Status { get; private set; }

    public Guid SourceWarehouseId { get; private set; }
    public Warehouse SourceWarehouse { get; private set; } = null!;

    public Guid DestinationWarehouseId { get; private set; }
    public Warehouse DestinationWarehouse { get; private set; } = null!;

    public string? Reference { get; private set; }
    public string? Notes { get; private set; }

    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    private readonly List<StockTransferLine> _lines = new();
    public IReadOnlyCollection<StockTransferLine> Lines => _lines.AsReadOnly();

    private StockTransfer() { }

    public static Result<StockTransfer> Create(
        StockTransferNumber number,
        Guid sourceWarehouseId,
        Guid destinationWarehouseId,
        DateTime transferDate,
        string? reference = null,
        string? notes = null)
    {
        if (sourceWarehouseId == Guid.Empty)
            return Result.Failure<StockTransfer>(Error.Validation("SourceWarehouseId",
                "L'entrepôt source est obligatoire"));

        if (destinationWarehouseId == Guid.Empty)
            return Result.Failure<StockTransfer>(Error.Validation("DestinationWarehouseId",
                "L'entrepôt de destination est obligatoire"));

        if (sourceWarehouseId == destinationWarehouseId)
            return Result.Failure<StockTransfer>(Error.Validation("DestinationWarehouseId",
                "L'entrepôt de destination doit être différent de l'entrepôt source"));

        var transfer = new StockTransfer
        {
            Number = number,
            TransferDate = transferDate.Date,
            Status = StockTransferStatus.Draft,
            SourceWarehouseId = sourceWarehouseId,
            DestinationWarehouseId = destinationWarehouseId,
            Reference = reference?.Trim(),
            Notes = notes?.Trim()
        };

        transfer.AddDomainEvent(new StockTransferCreatedEvent(
            transfer.Id, transfer.Number.Value, sourceWarehouseId, destinationWarehouseId));

        return Result.Success(transfer);
    }

    public Result AddLine(Product product, decimal requestedQuantity, string? notes = null)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce transfert ne peut plus être modifié"));

        var sellable = ProductCommercialGuards.EnsureCanAppearOnDocument(product);
        if (sellable.IsFailure)
            return sellable;

        if (_lines.Any(l => l.ProductId == product.Id))
            return Result.Failure(Error.Validation("ProductId",
                $"Le produit '{product.Name}' est déjà dans ce transfert"));

        var lineNumber = _lines.Count + 1;
        var lineResult = StockTransferLine.Create(this, lineNumber, product, requestedQuantity, notes);

        if (lineResult.IsFailure)
            return Result.Failure(lineResult.Error);

        _lines.Add(lineResult.Value);
        return Result.Success();
    }

    public Result RemoveLine(Guid lineId)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce transfert ne peut plus être modifié"));

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            return Result.Failure(Error.NotFound("StockTransferLine", lineId));

        _lines.Remove(line);
        RenumberLines();
        return Result.Success();
    }

    public Result Confirm()
    {
        if (!Status.CanBeConfirmed())
            return Result.Failure(Error.Validation("Status",
                "Ce transfert ne peut pas être confirmé dans son état actuel"));

        if (_lines.Count == 0)
            return Result.Failure(Error.Validation("Lines",
                "Le transfert doit contenir au moins une ligne"));

        Status = StockTransferStatus.Confirmed;
        ConfirmedAt = DateTime.UtcNow;

        AddDomainEvent(new StockTransferConfirmedEvent(Id, Number.Value));
        return Result.Success();
    }

    public Result StartTransit()
    {
        if (!Status.CanStartTransit())
            return Result.Failure(Error.Validation("Status",
                "Ce transfert ne peut pas passer en transit dans son état actuel"));

        Status = StockTransferStatus.InTransit;
        return Result.Success();
    }

    /// <summary>
    /// Marks the transfer as completed. Each line gets its RequestedQuantity as TransferredQuantity.
    /// The actual stock movements (exit source + entry destination) are handled by the event handler.
    /// </summary>
    public Result Complete()
    {
        if (!Status.CanBeCompleted())
            return Result.Failure(Error.Validation("Status",
                "Ce transfert ne peut pas être terminé dans son état actuel"));

        foreach (var line in _lines)
        {
            var result = line.RecordTransfer(line.RequestedQuantity);
            if (result.IsFailure)
                return result;
        }

        Status = StockTransferStatus.Completed;
        CompletedAt = DateTime.UtcNow;

        AddDomainEvent(new StockTransferCompletedEvent(
            Id, Number.Value, SourceWarehouseId, DestinationWarehouseId, _lines.Count));

        return Result.Success();
    }

    public Result Cancel(string reason)
    {
        if (!Status.CanBeCancelled())
            return Result.Failure(Error.Validation("Status",
                "Ce transfert ne peut pas être annulé dans son état actuel"));

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(Error.Validation("CancellationReason",
                "Le motif d'annulation est obligatoire"));

        Status = StockTransferStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        CancellationReason = reason.Trim();

        AddDomainEvent(new StockTransferCancelledEvent(Id, Number.Value, reason));
        return Result.Success();
    }

    public void UpdateNotes(string? notes)
    {
        if (Status.CanBeEdited())
        {
            Notes = notes?.Trim();
        }
    }

    private void RenumberLines()
    {
        for (int i = 0; i < _lines.Count; i++)
        {
            _lines[i].SetLineNumber(i + 1);
        }
    }

    public bool IsFullyTransferred => _lines.All(l => l.IsFullyTransferred);
    public decimal TotalRequestedQuantity => _lines.Sum(l => l.RequestedQuantity);
    public decimal TotalTransferredQuantity => _lines.Sum(l => l.TransferredQuantity);
}
