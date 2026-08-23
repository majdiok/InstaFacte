using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Events;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Purchase receipt (bon de réception d'achat) — inbound counterpart of DeliveryNote.
/// Validation updates purchase-order received quantities and creates stock entries.
/// </summary>
public sealed class PurchaseReceipt : AggregateRoot
{
    public PurchaseReceiptNumber Number { get; private set; } = null!;
    public DateTime ReceiptDate { get; private set; }
    public PurchaseReceiptStatus Status { get; private set; }

    public Guid SupplierId { get; private set; }
    public Supplier Supplier { get; private set; } = null!;

    public Guid? PurchaseOrderId { get; private set; }
    public PurchaseOrder? PurchaseOrder { get; private set; }

    public Guid WarehouseId { get; private set; }
    public Warehouse Warehouse { get; private set; } = null!;

    public string? SupplierReference { get; private set; }
    public string? TransporterName { get; private set; }
    public string? DeliveryNoteNumber { get; private set; }
    public string? Notes { get; private set; }

    public DateTime? ValidatedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    public Money SubTotal { get; private set; } = null!;
    public Money TotalVat { get; private set; } = null!;
    public Money TotalAmount { get; private set; } = null!;

    private readonly List<PurchaseReceiptLine> _lines = new();
    public IReadOnlyCollection<PurchaseReceiptLine> Lines => _lines.AsReadOnly();

    private readonly List<PurchaseReceiptAttachment> _attachments = new();
    public IReadOnlyCollection<PurchaseReceiptAttachment> Attachments => _attachments.AsReadOnly();

    private PurchaseReceipt() { }

    public static Result<PurchaseReceipt> Create(
        PurchaseReceiptNumber number,
        Supplier supplier,
        Warehouse warehouse,
        DateTime receiptDate,
        Guid? purchaseOrderId = null,
        string? supplierReference = null,
        string? transporterName = null,
        string? deliveryNoteNumber = null,
        string? notes = null)
    {
        if (supplier is null)
            return Result.Failure<PurchaseReceipt>(Error.Validation("Supplier", "Le fournisseur est obligatoire"));

        if (warehouse is null)
            return Result.Failure<PurchaseReceipt>(Error.Validation("Warehouse", "L'entrepôt est obligatoire"));

        if (!warehouse.IsActive)
            return Result.Failure<PurchaseReceipt>(Error.Validation("Warehouse", "L'entrepôt sélectionné n'est pas actif"));

        var receipt = new PurchaseReceipt
        {
            Number = number,
            ReceiptDate = receiptDate.Date,
            Status = PurchaseReceiptStatus.Draft,
            SupplierId = supplier.Id,
            Supplier = supplier,
            PurchaseOrderId = purchaseOrderId,
            WarehouseId = warehouse.Id,
            Warehouse = warehouse,
            SupplierReference = supplierReference?.Trim(),
            TransporterName = transporterName?.Trim(),
            DeliveryNoteNumber = deliveryNoteNumber?.Trim(),
            Notes = notes?.Trim(),
            SubTotal = Money.Zero(),
            TotalVat = Money.Zero(),
            TotalAmount = Money.Zero()
        };

        return Result.Success(receipt);
    }

    public Result AddLine(
        Product product,
        decimal receivedQuantity,
        Money unitPrice,
        decimal orderedQuantity = 0,
        Guid? purchaseOrderLineId = null,
        decimal? discountPercent = null)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce bon de réception ne peut plus être modifié"));

        var sellable = ProductCommercialGuards.EnsureCanAppearOnDocument(product);
        if (sellable.IsFailure)
            return sellable;

        var lineResult = PurchaseReceiptLine.Create(
            this,
            _lines.Count + 1,
            product,
            receivedQuantity,
            unitPrice,
            orderedQuantity,
            purchaseOrderLineId,
            discountPercent);

        if (lineResult.IsFailure)
            return Result.Failure(lineResult.Error);

        _lines.Add(lineResult.Value);
        RecalculateTotals();
        return Result.Success();
    }

    public Result UpdateLine(
        Guid lineId,
        decimal receivedQuantity,
        Money? unitPrice = null,
        decimal? discountPercent = null)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce bon de réception ne peut plus être modifié"));

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            return Result.Failure(Error.NotFound("PurchaseReceiptLine", lineId));

        var result = line.Update(receivedQuantity, unitPrice, discountPercent);
        if (result.IsFailure)
            return result;

        RecalculateTotals();
        return Result.Success();
    }

    public Result RemoveLine(Guid lineId)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce bon de réception ne peut plus être modifié"));

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            return Result.Failure(Error.NotFound("PurchaseReceiptLine", lineId));

        _lines.Remove(line);
        RenumberLines();
        RecalculateTotals();
        return Result.Success();
    }

    public Result ClearLines()
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce bon de réception ne peut plus être modifié"));

        _lines.Clear();
        RecalculateTotals();
        return Result.Success();
    }

    public void UpdateHeader(
        DateTime receiptDate,
        Guid warehouseId,
        string? supplierReference,
        string? transporterName,
        string? deliveryNoteNumber,
        string? notes)
    {
        if (!Status.CanBeEdited())
            return;

        ReceiptDate = receiptDate.Date;
        WarehouseId = warehouseId;
        SupplierReference = supplierReference?.Trim();
        TransporterName = transporterName?.Trim();
        DeliveryNoteNumber = deliveryNoteNumber?.Trim();
        Notes = notes?.Trim();
    }

    /// <summary>
    /// Marks the receipt as validated at the domain level.
    /// Stock and PO imputation are applied by the application service after this succeeds.
    /// </summary>
    public Result MarkValidated()
    {
        if (!Status.CanBeValidated())
            return Result.Failure(Error.Validation("Status",
                "Ce bon de réception ne peut pas être validé dans son état actuel"));

        if (WarehouseId == Guid.Empty)
            return Result.Failure(Error.Validation("WarehouseId", "L'entrepôt est obligatoire"));

        var receivableLines = _lines.Where(l => l.ReceivedQuantity > 0).ToList();
        if (receivableLines.Count == 0)
            return Result.Failure(Error.Validation("Lines", "Aucune quantité à réceptionner."));

        Status = PurchaseReceiptStatus.Validated;
        ValidatedAt = DateTime.UtcNow;

        AddDomainEvent(new PurchaseReceiptValidatedEvent(
            Id, Number.Value, PurchaseOrderId, receivableLines.Count));

        return Result.Success();
    }

    /// <summary>
    /// Cancels a draft or validated receipt (domain status only).
    /// Stock/PO reversal for validated receipts is handled by the application layer.
    /// </summary>
    public Result Cancel(string reason)
    {
        if (!Status.CanBeCancelled())
            return Result.Failure(Error.Validation("Status",
                "Ce bon de réception ne peut pas être annulé"));

        if (_lines.Any(l => l.InvoicedQuantity > 0))
            return Result.Failure(Error.Validation("Status",
                "Impossible d'annuler un bon de réception déjà facturé"));

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(Error.Validation("CancellationReason",
                "Le motif d'annulation est obligatoire"));

        var wasValidated = Status == PurchaseReceiptStatus.Validated;
        Status = PurchaseReceiptStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        CancellationReason = reason.Trim();

        AddDomainEvent(new PurchaseReceiptCancelledEvent(
            Id, Number.Value, CancellationReason, wasValidated));

        return Result.Success();
    }

    public Result AddAttachment(PurchaseReceiptAttachment attachment)
    {
        if (attachment is null)
            return Result.Failure(Error.Validation("Attachment", "Pièce jointe invalide"));

        _attachments.Add(attachment);
        return Result.Success();
    }

    public Result RemoveAttachment(Guid attachmentId)
    {
        var attachment = _attachments.FirstOrDefault(a => a.Id == attachmentId);
        if (attachment is null)
            return Result.Failure(Error.NotFound("PurchaseReceiptAttachment", attachmentId));

        _attachments.Remove(attachment);
        return Result.Success();
    }

    /// <summary>
    /// True when received quantities on this receipt are strictly less than ordered quantities
    /// (display badge "Reçu partiel").
    /// </summary>
    public bool IsPartialRelativeToOrdered =>
        _lines.Count > 0 &&
        _lines.Sum(l => l.ReceivedQuantity) < _lines.Sum(l => l.OrderedQuantity);

    public decimal TotalReceivedNotInvoicedQuantity =>
        _lines.Sum(l => l.ReceivedNotInvoicedQuantity);

    public bool HasReceivedNotInvoiced => TotalReceivedNotInvoicedQuantity > 0;

    public Result ApplyInvoicing(IEnumerable<(Guid LineId, decimal Quantity)> imputations)
    {
        foreach (var (lineId, quantity) in imputations)
        {
            var line = _lines.FirstOrDefault(l => l.Id == lineId);
            if (line is null)
                return Result.Failure(Error.NotFound("PurchaseReceiptLine", lineId));

            var result = line.RecordInvoiced(quantity);
            if (result.IsFailure)
                return result;
        }

        RecalculateInvoicingStatus();
        return Result.Success();
    }

    public Result ReverseInvoicing(IEnumerable<(Guid LineId, decimal Quantity)> reversals)
    {
        foreach (var (lineId, quantity) in reversals)
        {
            var line = _lines.FirstOrDefault(l => l.Id == lineId);
            if (line is null)
                return Result.Failure(Error.NotFound("PurchaseReceiptLine", lineId));

            var result = line.ReverseInvoiced(quantity);
            if (result.IsFailure)
                return result;
        }

        RecalculateInvoicingStatus();
        return Result.Success();
    }

    internal void RecalculateInvoicingStatus()
    {
        if (Status is PurchaseReceiptStatus.Cancelled or PurchaseReceiptStatus.Draft)
            return;

        var anyInvoiced = _lines.Any(l => l.InvoicedQuantity > 0);
        var anyNotInvoiced = _lines.Any(l => l.ReceivedNotInvoicedQuantity > 0);

        if (anyInvoiced && !anyNotInvoiced)
            Status = PurchaseReceiptStatus.Invoiced;
        else if (anyInvoiced)
            Status = PurchaseReceiptStatus.PartiallyInvoiced;
        else
            Status = PurchaseReceiptStatus.Validated;
    }

    private void RecalculateTotals()
    {
        var currency = Money.DefaultCurrency;
        SubTotal = _lines.Aggregate(Money.Zero(currency), (sum, line) => sum.Add(line.SubTotal));
        TotalVat = _lines.Aggregate(Money.Zero(currency), (sum, line) => sum.Add(line.VatAmount));
        TotalAmount = SubTotal.Add(TotalVat);
    }

    private void RenumberLines()
    {
        for (var i = 0; i < _lines.Count; i++)
            _lines[i].SetLineNumber(i + 1);
    }
}
