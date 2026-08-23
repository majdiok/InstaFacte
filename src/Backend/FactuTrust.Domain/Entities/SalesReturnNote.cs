using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Events;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Bon de retour client for goods delivered and not yet invoiced.
/// Confirmation restores stock and increments <see cref="DeliveryNoteLine.ReturnedQuantity"/>.
/// </summary>
public sealed class SalesReturnNote : AggregateRoot
{
    public SalesReturnNoteNumber Number { get; private set; } = null!;
    public DateTime ReturnDate { get; private set; }
    public SalesReturnNoteStatus Status { get; private set; }

    public Guid ClientId { get; private set; }
    public Client Client { get; private set; } = null!;

    public Guid DeliveryNoteId { get; private set; }
    public DeliveryNote DeliveryNote { get; private set; } = null!;

    public Guid? WarehouseId { get; private set; }
    public Warehouse? Warehouse { get; private set; }

    public string Reason { get; private set; } = null!;
    public string? Notes { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }

    private readonly List<SalesReturnNoteLine> _lines = new();
    public IReadOnlyCollection<SalesReturnNoteLine> Lines => _lines.AsReadOnly();

    private SalesReturnNote() { }

    public static Result<SalesReturnNote> Create(
        SalesReturnNoteNumber number,
        DeliveryNote deliveryNote,
        DateTime returnDate,
        string reason,
        string? notes = null)
    {
        if (deliveryNote is null)
            return Result.Failure<SalesReturnNote>(
                Error.Validation("DeliveryNote", "Le bon de livraison est obligatoire"));

        if (deliveryNote.InvoiceId.HasValue)
            return Result.Failure<SalesReturnNote>(
                Error.Validation("DeliveryNote", "Ce bon de livraison a déjà été facturé — utilisez un avoir"));

        if (!deliveryNote.Status.CanBeInvoiced())
            return Result.Failure<SalesReturnNote>(
                Error.Validation("DeliveryNote",
                    $"Un bon de retour n'est possible que sur un bon livré et non facturé (statut : {deliveryNote.Status.ToDisplayString()})"));

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 3)
            return Result.Failure<SalesReturnNote>(
                Error.Validation("Reason", "Le motif de retour est obligatoire (3 caractères minimum)"));

        if (reason.Trim().Length > 500)
            return Result.Failure<SalesReturnNote>(
                Error.Validation("Reason", "Le motif de retour ne peut pas dépasser 500 caractères"));

        var note = new SalesReturnNote
        {
            Number = number,
            ReturnDate = returnDate.Date,
            Status = SalesReturnNoteStatus.Draft,
            ClientId = deliveryNote.ClientId,
            Client = deliveryNote.Client,
            DeliveryNoteId = deliveryNote.Id,
            DeliveryNote = deliveryNote,
            WarehouseId = deliveryNote.WarehouseId,
            Warehouse = deliveryNote.Warehouse,
            Reason = reason.Trim(),
            Notes = notes?.Trim()
        };

        note.AddDomainEvent(new SalesReturnNoteCreatedEvent(note.Id, note.Number.Value, deliveryNote.Id));
        return Result.Success(note);
    }

    public Result AddLine(DeliveryNoteLine source, decimal returnedQuantity, string? notes = null)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce bon de retour ne peut plus être modifié"));

        if (source.DeliveryNoteId != DeliveryNoteId)
            return Result.Failure(Error.Validation("DeliveryNoteLine",
                "Cette ligne n'appartient pas au bon de livraison source"));

        if (_lines.Any(l => l.DeliveryNoteLineId == source.Id))
            return Result.Failure(Error.Validation("DeliveryNoteLine",
                "Cette ligne de bon de livraison est déjà présente sur le bon de retour"));

        if (returnedQuantity > source.InvoiceableQuantity)
            return Result.Failure(Error.Validation("ReturnedQuantity",
                $"La quantité retournée ({returnedQuantity}) dépasse le restant facturable ({source.InvoiceableQuantity})"));

        var lineResult = SalesReturnNoteLine.Create(this, _lines.Count + 1, source, returnedQuantity, notes);
        if (lineResult.IsFailure)
            return Result.Failure(lineResult.Error);

        _lines.Add(lineResult.Value);
        return Result.Success();
    }

    public Result UpdateLine(Guid lineId, decimal returnedQuantity, string? notes = null)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce bon de retour ne peut plus être modifié"));

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            return Result.Failure(Error.NotFound("SalesReturnNoteLine", lineId));

        return line.Update(returnedQuantity, notes);
    }

    public Result RemoveLine(Guid lineId)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce bon de retour ne peut plus être modifié"));

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            return Result.Failure(Error.NotFound("SalesReturnNoteLine", lineId));

        _lines.Remove(line);
        RenumberLines();
        return Result.Success();
    }

    public Result ClearLines()
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce bon de retour ne peut plus être modifié"));

        _lines.Clear();
        return Result.Success();
    }

    public Result UpdateHeader(DateTime returnDate, string reason, string? notes = null)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce bon de retour ne peut plus être modifié"));

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 3)
            return Result.Failure(Error.Validation("Reason", "Le motif de retour est obligatoire (3 caractères minimum)"));

        if (reason.Trim().Length > 500)
            return Result.Failure(Error.Validation("Reason", "Le motif de retour ne peut pas dépasser 500 caractères"));

        ReturnDate = returnDate.Date;
        Reason = reason.Trim();
        Notes = notes?.Trim();
        return Result.Success();
    }

    /// <summary>
    /// Confirms the return at domain level. Stock and BL ReturnedQuantity are applied by the handler.
    /// </summary>
    public Result Confirm()
    {
        if (!Status.CanBeConfirmed())
            return Result.Failure(Error.Validation("Status", "Ce bon de retour ne peut pas être confirmé"));

        if (_lines.Count == 0)
            return Result.Failure(Error.Validation("Lines", "Le bon de retour doit contenir au moins une ligne"));

        Status = SalesReturnNoteStatus.Confirmed;
        ConfirmedAt = DateTime.UtcNow;
        IncrementVersion();
        AddDomainEvent(new SalesReturnNoteConfirmedEvent(Id, Number.Value, DeliveryNoteId));
        return Result.Success();
    }

    public decimal TotalHT => _lines.Sum(l => l.TotalHT);
    public decimal TotalFodec => _lines.Sum(l => l.FodecAmount);
    public decimal TotalVAT => _lines.Sum(l => l.TotalVAT);
    public decimal TotalTTC => _lines.Sum(l => l.TotalTTC);
    public decimal TotalReturnedQuantity => _lines.Sum(l => l.ReturnedQuantity);

    /// <summary>
    /// Drops parent navigations before EF insert/update so the BL graph is not persisted
    /// from this aggregate. Foreign keys are kept.
    /// </summary>
    public void ClearParentNavigationsForPersistence()
    {
        DeliveryNote = null!;
        Client = null!;
        Warehouse = null;
        foreach (var line in _lines)
            line.ClearParentNavigationsForPersistence();
    }

    private void RenumberLines()
    {
        for (var i = 0; i < _lines.Count; i++)
            _lines[i].SetLineNumber(i + 1);
    }
}
