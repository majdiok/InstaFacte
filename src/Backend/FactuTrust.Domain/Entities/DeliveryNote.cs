using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Events;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Represents a delivery note (Bon de Livraison) - the core aggregate for tracking deliveries.
/// A delivery note must be created before invoicing, and can be linked to an invoice.
/// </summary>
public sealed class DeliveryNote : AggregateRoot
{
    public DeliveryNoteNumber Number { get; private set; } = null!;
    public DateTime IssueDate { get; private set; }
    public DateTime? DeliveryDate { get; private set; }
    public DeliveryNoteStatus Status { get; private set; }

    public Guid ClientId { get; private set; }
    public Client Client { get; private set; } = null!;

    public string? Reference { get; private set; }
    public string? Notes { get; private set; }

    // Delivery address (can differ from client address)
    public string DeliveryAddress { get; private set; } = null!;
    public string? DeliveryCity { get; private set; }
    public string? DeliveryPostalCode { get; private set; }

    // Recipient information
    public string? RecipientName { get; private set; }
    public string? RecipientSignature { get; private set; }
    public DateTime? SignedAt { get; private set; }

    // Failure tracking
    public string? FailureReason { get; private set; }
    public DateTime? FailedAt { get; private set; }

    // Cancellation
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    // Invoice link - populated when invoiced
    public Guid? InvoiceId { get; private set; }
    public Invoice? Invoice { get; private set; }
    public DateTime? InvoicedAt { get; private set; }

    /// <summary>
    /// If true, this BL can be grouped with other BLs into a single invoice.
    /// Controlled at client level, defaults to false.
    /// </summary>
    public bool AllowGroupInvoicing { get; private set; }

    /// <summary>
    /// Optional source warehouse for stock deduction. When null, the default warehouse is used.
    /// </summary>
    public Guid? WarehouseId { get; private set; }
    public Warehouse? Warehouse { get; private set; }

    /// <summary>
    /// Commande client à l'origine de ce bon de livraison.
    ///
    /// C'est ce lien qui fait vivre le reliquat : sans lui, le « reste à livrer » d'un bon
    /// mourait avec lui et rien ne permettait d'émettre un bon complémentaire rattaché au même
    /// engagement. Avec lui, chaque livraison s'impute sur la commande, qui sait ce qui reste.
    /// </summary>
    public Guid? SourceSalesOrderId { get; private set; }

    /// <summary>Rattache ce bon de livraison à une commande client. Idempotent.</summary>
    public void AttachSalesOrderOrigin(Guid salesOrderId)
    {
        if (salesOrderId == Guid.Empty)
            throw new ArgumentException("SalesOrderId invalide", nameof(salesOrderId));

        SourceSalesOrderId ??= salesOrderId;
    }

    private readonly List<DeliveryNoteLine> _lines = new();
    public IReadOnlyCollection<DeliveryNoteLine> Lines => _lines.AsReadOnly();

    private DeliveryNote() { }

    public static Result<DeliveryNote> Create(
        DeliveryNoteNumber number,
        Client client,
        DateTime issueDate,
        string deliveryAddress,
        string? deliveryCity = null,
        string? deliveryPostalCode = null,
        string? reference = null,
        string? notes = null,
        bool allowGroupInvoicing = false,
        Guid? warehouseId = null)
    {
        if (string.IsNullOrWhiteSpace(deliveryAddress))
            return Result.Failure<DeliveryNote>(
                Error.Validation("DeliveryAddress", "L'adresse de livraison est obligatoire"));

        var deliveryNote = new DeliveryNote
        {
            Number = number,
            ClientId = client.Id,
            Client = client,
            IssueDate = issueDate.Date,
            Status = DeliveryNoteStatus.Draft,
            DeliveryAddress = deliveryAddress.Trim(),
            DeliveryCity = deliveryCity?.Trim(),
            DeliveryPostalCode = deliveryPostalCode?.Trim(),
            Reference = reference?.Trim(),
            Notes = notes?.Trim(),
            AllowGroupInvoicing = allowGroupInvoicing,
            WarehouseId = warehouseId
        };

        deliveryNote.AddDomainEvent(new DeliveryNoteCreatedEvent(
            deliveryNote.Id, 
            deliveryNote.Number.Value, 
            client.Id));

        return Result.Success(deliveryNote);
    }

    public Result AddLine(
        Product product,
        decimal orderedQuantity,
        string? notes = null,
        decimal? discountPercent = null,
        decimal fodecRatePercent = DeliveryNoteLine.DefaultFodecRatePercent,
        Money? unitPriceOverride = null,
        Guid? appliedPromotionId = null,
        string? appliedPromotionName = null)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce bon de livraison ne peut plus être modifié"));

        var sellable = ProductCommercialGuards.EnsureCanAppearOnDocument(product);
        if (sellable.IsFailure)
            return sellable;

        if (orderedQuantity <= 0)
            return Result.Failure(Error.Validation("OrderedQuantity", "La quantité doit être supérieure à zéro"));

        var lineNumber = _lines.Count + 1;

        var lineResult = DeliveryNoteLine.Create(
            this, lineNumber, product, orderedQuantity, notes, discountPercent, fodecRatePercent, unitPriceOverride,
            appliedPromotionId, appliedPromotionName);
        if (lineResult.IsFailure)
            return Result.Failure(lineResult.Error);

        _lines.Add(lineResult.Value);

        return Result.Success();
    }

    public Result RemoveLine(Guid lineId)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce bon de livraison ne peut plus être modifié"));

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            return Result.Failure(Error.NotFound("DeliveryNoteLine", lineId));

        _lines.Remove(line);
        RenumberLines();

        return Result.Success();
    }

    public Result UpdateLine(
        Guid lineId,
        decimal orderedQuantity,
        string? notes = null,
        decimal? discountPercent = null)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce bon de livraison ne peut plus être modifié"));

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            return Result.Failure(Error.NotFound("DeliveryNoteLine", lineId));

        return line.Update(orderedQuantity, notes, discountPercent);
    }

    /// <summary>
    /// Confirms the delivery note, making it ready for delivery.
    /// After confirmation, lines cannot be modified.
    /// </summary>
    public Result Confirm()
    {
        if (Status == DeliveryNoteStatus.Confirmed)
            return Result.Failure(Error.Validation("Status", "Ce bon de livraison est déjà confirmé. Utilisez l'action « Démarrer livraison » pour passer à l'étape suivante."));

        if (!Status.CanBeConfirmed())
            return Result.Failure(Error.Validation("Status", "Ce bon de livraison ne peut pas être confirmé"));

        if (_lines.Count == 0)
            return Result.Failure(Error.Validation("Lines", "Le bon de livraison doit contenir au moins une ligne"));

        Status = DeliveryNoteStatus.Confirmed;
        AddDomainEvent(new DeliveryNoteConfirmedEvent(Id, Number.Value));

        return Result.Success();
    }

    /// <summary>
    /// Marks the delivery as in progress.
    /// </summary>
    public Result StartDelivery()
    {
        if (!Status.CanStartDelivery())
            return Result.Failure(Error.Validation("Status", "La livraison ne peut pas démarrer dans cet état"));

        Status = DeliveryNoteStatus.InTransit;
        AddDomainEvent(new DeliveryNoteInTransitEvent(Id, Number.Value));

        return Result.Success();
    }

    /// <summary>
    /// Records delivery completion with signature.
    /// Auto-calculates status based on line quantities:
    /// - All delivered = Delivered
    /// - All rejected (0 delivered) = Refused
    /// - Mixed = PartiallyDelivered
    /// </summary>
    public Result RecordDelivery(
        DateTime deliveryDate,
        string recipientName,
        string? recipientSignature = null)
    {
        if (!Status.CanBeDelivered())
            return Result.Failure(Error.Validation("Status", "Ce bon de livraison ne peut pas être marqué comme livré"));

        if (string.IsNullOrWhiteSpace(recipientName))
            return Result.Failure(Error.Validation("RecipientName", "Le nom du réceptionnaire est obligatoire"));

        DeliveryDate = deliveryDate;
        RecipientName = recipientName.Trim();
        RecipientSignature = recipientSignature;
        SignedAt = DateTime.UtcNow;

        // Auto-calculate status based on line quantities
        var allFullyDelivered = _lines.All(l => l.DeliveredQuantity >= l.OrderedQuantity);
        var allRefused = _lines.All(l => l.DeliveredQuantity == 0 && l.RejectedQuantity >= l.OrderedQuantity);
        var anyRejected = _lines.Any(l => l.RejectedQuantity > 0);

        if (allRefused)
        {
            Status = DeliveryNoteStatus.Refused;
        }
        else if (allFullyDelivered)
        {
            Status = DeliveryNoteStatus.Delivered;
        }
        else
        {
            Status = DeliveryNoteStatus.PartiallyDelivered;
        }

        var isPartial = Status == DeliveryNoteStatus.PartiallyDelivered;
        AddDomainEvent(new DeliveryNoteDeliveredEvent(Id, Number.Value, recipientName, isPartial));

        return Result.Success();
    }

    /// <summary>
    /// Records a delivery failure.
    /// </summary>
    public Result RecordFailure(string reason)
    {
        if (!Status.CanBeDelivered())
            return Result.Failure(Error.Validation("Status", "Ce bon de livraison ne peut pas être marqué comme échoué"));

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(Error.Validation("FailureReason", "Le motif d'échec est obligatoire"));

        Status = DeliveryNoteStatus.Failed;
        FailureReason = reason.Trim();
        FailedAt = DateTime.UtcNow;

        AddDomainEvent(new DeliveryNoteFailedEvent(Id, Number.Value, reason));

        return Result.Success();
    }

    /// <summary>
    /// Cancels the delivery note.
    /// </summary>
    public Result Cancel(string reason)
    {
        if (!Status.CanBeCancelled())
            return Result.Failure(Error.Validation("Status", "Ce bon de livraison ne peut pas être annulé"));

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(Error.Validation("CancellationReason", "Le motif d'annulation est obligatoire"));

        Status = DeliveryNoteStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        CancellationReason = reason.Trim();

        AddDomainEvent(new DeliveryNoteCancelledEvent(Id, Number.Value, reason));

        return Result.Success();
    }

    /// <summary>
    /// Links this delivery note to an invoice.
    /// </summary>
    public Result MarkAsInvoiced(Invoice invoice)
    {
        if (!Status.CanBeInvoiced())
            return Result.Failure(Error.Validation("Status", "Ce bon de livraison ne peut pas être facturé"));

        InvoiceId = invoice.Id;
        Invoice = invoice;
        InvoicedAt = DateTime.UtcNow;
        Status = DeliveryNoteStatus.Invoiced;

        IncrementVersion();
        AddDomainEvent(new DeliveryNoteInvoicedEvent(Id, Number.Value, invoice.Id, invoice.Number.Value));

        return Result.Success();
    }

    /// <summary>
    /// Records a confirmed return against a line. Increments <see cref="Version"/> for concurrency
    /// against a simultaneous invoice generation.
    /// </summary>
    public Result RecordReturn(Guid lineId, decimal quantity)
    {
        if (InvoiceId.HasValue)
            return Result.Failure(Error.Validation("Invoice",
                "Ce bon de livraison a déjà été facturé — utilisez un avoir"));

        if (!Status.CanBeInvoiced())
            return Result.Failure(Error.Validation("Status",
                "Un retour n'est possible que sur un bon livré et non facturé"));

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            return Result.Failure(Error.NotFound("DeliveryNoteLine", lineId));

        var result = line.RecordReturn(quantity);
        if (result.IsFailure)
            return result;

        IncrementVersion();
        return Result.Success();
    }

    /// <summary>
    /// Retries a failed delivery by resetting status to Confirmed.
    /// </summary>
    public Result RetryDelivery()
    {
        if (Status != DeliveryNoteStatus.Failed)
            return Result.Failure(Error.Validation("Status", "Seuls les bons en échec peuvent être relancés"));

        Status = DeliveryNoteStatus.Confirmed;
        FailureReason = null;
        FailedAt = null;

        return Result.Success();
    }

    public void UpdateNotes(string? notes)
    {
        if (Status.CanBeEdited())
        {
            Notes = notes?.Trim();
        }
    }

    public void UpdateDeliveryAddress(string address, string? city = null, string? postalCode = null)
    {
        if (Status.CanBeEdited() && !string.IsNullOrWhiteSpace(address))
        {
            DeliveryAddress = address.Trim();
            DeliveryCity = city?.Trim();
            DeliveryPostalCode = postalCode?.Trim();
        }
    }

    private void RenumberLines()
    {
        for (int i = 0; i < _lines.Count; i++)
        {
            _lines[i].SetLineNumber(i + 1);
        }
    }

    /// <summary>
    /// Returns true if all lines are fully delivered.
    /// </summary>
    public bool IsFullyDelivered => _lines.All(l => l.IsFullyDelivered);

    /// <summary>
    /// Returns total number of ordered items across all lines.
    /// </summary>
    public decimal TotalOrderedQuantity => _lines.Sum(l => l.OrderedQuantity);

    /// <summary>
    /// Returns total number of delivered items across all lines.
    /// </summary>
    public decimal TotalDeliveredQuantity => _lines.Sum(l => l.DeliveredQuantity);

    /// <summary>Confirmed returned quantity across all lines.</summary>
    public decimal TotalReturnedQuantity => _lines.Sum(l => l.ReturnedQuantity);

    /// <summary>Delivered minus returned — the quantity a generated invoice may bill.</summary>
    public decimal TotalInvoiceableQuantity => _lines.Sum(l => l.InvoiceableQuantity);

    /// <summary>True when at least one line still has invoiceable quantity.</summary>
    public bool HasInvoiceableQuantity => _lines.Any(l => l.InvoiceableQuantity > 0);

    /// <summary>
    /// Aggregate total HT (sum of all line TotalHT, remises déduites).
    /// </summary>
    public decimal TotalHT => _lines.Sum(l => l.TotalHT);

    /// <summary>
    /// Aggregate FODEC (sum of all line FodecAmount).
    /// </summary>
    public decimal TotalFodec => _lines.Sum(l => l.FodecAmount);

    /// <summary>
    /// Aggregate total VAT (sum of all line TotalVAT).
    /// </summary>
    public decimal TotalVAT => _lines.Sum(l => l.TotalVAT);

    /// <summary>
    /// Aggregate total TTC (sum of all line TotalTTC, FODEC inclus).
    /// </summary>
    public decimal TotalTTC => _lines.Sum(l => l.TotalTTC);
}
