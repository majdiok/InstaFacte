using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Events;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Represents a purchase order (commande fournisseur).
/// Follows the same lifecycle pattern as Quote.
/// </summary>
public sealed class PurchaseOrder : AggregateRoot
{
    public PurchaseOrderNumber Number { get; private set; } = null!;
    public DateTime OrderDate { get; private set; }
    public DateTime? ExpectedDeliveryDate { get; private set; }
    public PurchaseOrderStatus Status { get; private set; }

    public Guid SupplierId { get; private set; }
    public Supplier Supplier { get; private set; } = null!;

    public string? Reference { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>
    /// Optional destination warehouse for goods reception. When null, the default warehouse is used.
    /// </summary>
    public Guid? WarehouseId { get; private set; }
    public Warehouse? Warehouse { get; private set; }

    /// <summary>Optional BTP/ESN project this purchase order is charged to. Additive nullable FK.</summary>
    public Guid? ProjectId { get; private set; }

    private readonly List<PurchaseOrderLine> _lines = new();
    public IReadOnlyCollection<PurchaseOrderLine> Lines => _lines.AsReadOnly();

    public Money SubTotal { get; private set; } = null!;
    public Money TotalVat { get; private set; } = null!;
    public Money TotalAmount { get; private set; } = null!;

    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? ReceivedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTime? InvoicedAt { get; private set; }

    /// <summary>Sum of received-but-not-invoiced quantities across all lines.</summary>
    public decimal TotalReceivedNotInvoicedQuantity =>
        _lines.Sum(l => l.ReceivedNotInvoicedQuantity);

    public bool HasReceivedNotInvoiced => TotalReceivedNotInvoicedQuantity > 0;

    /// <summary>
    /// Records invoicing imputation and recalculates order status.
    /// </summary>
    public Result ApplyInvoicing(IEnumerable<(Guid LineId, decimal Quantity)> imputations, Guid supplierInvoiceId)
    {
        foreach (var (lineId, quantity) in imputations)
        {
            var line = _lines.FirstOrDefault(l => l.Id == lineId);
            if (line is null)
                return Result.Failure(Error.NotFound("PurchaseOrderLine", lineId));

            var result = line.RecordInvoiced(quantity);
            if (result.IsFailure)
                return result;
        }

        RecalculateStatus();
        AddDomainEvent(new PurchaseOrderInvoicedEvent(Id, Number.Value, supplierInvoiceId));
        return Result.Success();
    }

    /// <summary>
    /// Reverses invoicing imputation (e.g. cancelled supplier invoice).
    /// </summary>
    public Result ReverseInvoicing(IEnumerable<(Guid LineId, decimal Quantity)> reversals)
    {
        foreach (var (lineId, quantity) in reversals)
        {
            var line = _lines.FirstOrDefault(l => l.Id == lineId);
            if (line is null)
                return Result.Failure(Error.NotFound("PurchaseOrderLine", lineId));

            var result = line.ReverseInvoiced(quantity);
            if (result.IsFailure)
                return result;
        }

        RecalculateStatus();
        return Result.Success();
    }

    [Obsolete("Use ApplyInvoicing and RecalculateStatus for partial invoicing.")]
    public Result MarkAsInvoiced(Guid supplierInvoiceId)
    {
        if (Status == PurchaseOrderStatus.Invoiced)
            return Result.Success();

        if (!Status.CanBeInvoiced() && Status != PurchaseOrderStatus.Confirmed)
            return Result.Failure(Error.Validation("Status",
                "La commande doit être reçue pour être facturée."));

        foreach (var line in _lines.Where(l => l.ReceivedNotInvoicedQuantity > 0))
        {
            var result = line.RecordInvoiced(line.ReceivedNotInvoicedQuantity);
            if (result.IsFailure)
                return result;
        }

        RecalculateStatus();
        AddDomainEvent(new PurchaseOrderInvoicedEvent(Id, Number.Value, supplierInvoiceId));
        return Result.Success();
    }

    private PurchaseOrder() { }

    public static Result<PurchaseOrder> Create(
        PurchaseOrderNumber number,
        Supplier supplier,
        DateTime orderDate,
        DateTime? expectedDeliveryDate = null,
        string? reference = null,
        string? notes = null,
        Guid? warehouseId = null)
    {
        if (expectedDeliveryDate.HasValue && expectedDeliveryDate.Value < orderDate)
            return Result.Failure<PurchaseOrder>(Error.Validation("ExpectedDeliveryDate",
                "La date de livraison prévue doit être postérieure à la date de commande"));

        var order = new PurchaseOrder
        {
            Number = number,
            SupplierId = supplier.Id,
            Supplier = supplier,
            OrderDate = orderDate.Date,
            ExpectedDeliveryDate = expectedDeliveryDate?.Date,
            Status = PurchaseOrderStatus.Draft,
            Reference = reference?.Trim(),
            Notes = notes?.Trim(),
            SubTotal = Money.Zero(),
            TotalVat = Money.Zero(),
            TotalAmount = Money.Zero(),
            WarehouseId = warehouseId
        };

        return Result.Success(order);
    }

    public Result AssignToProject(Guid? projectId)
    {
        if (projectId == Guid.Empty)
            projectId = null;
        ProjectId = projectId;
        return Result.Success();
    }

    public Result AddLine(Product product, decimal quantity, Money? customUnitPrice = null)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Cette commande ne peut plus être modifiée"));

        var sellable = ProductCommercialGuards.EnsureCanAppearOnDocument(product);
        if (sellable.IsFailure)
            return sellable;

        if (quantity <= 0)
            return Result.Failure(Error.Validation("Quantity", "La quantité doit être supérieure à zéro"));

        var unitPrice = customUnitPrice ?? product.GetPurchasePrice();
        var lineNumber = _lines.Count + 1;

        var lineResult = PurchaseOrderLine.Create(this, lineNumber, product, quantity, unitPrice);

        if (lineResult.IsFailure)
            return Result.Failure(lineResult.Error);

        _lines.Add(lineResult.Value);
        RecalculateTotals();

        return Result.Success();
    }

    public Result RemoveLine(Guid lineId)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Cette commande ne peut plus être modifiée"));

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            return Result.Failure(Error.NotFound("PurchaseOrderLine", lineId));

        _lines.Remove(line);
        RenumberLines();
        RecalculateTotals();

        return Result.Success();
    }

    public Result UpdateLine(Guid lineId, decimal quantity, Money? customUnitPrice = null)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Cette commande ne peut plus être modifiée"));

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            return Result.Failure(Error.NotFound("PurchaseOrderLine", lineId));

        var result = line.Update(quantity, customUnitPrice);
        if (result.IsFailure)
            return result;

        RecalculateTotals();

        return Result.Success();
    }

    public Result Confirm()
    {
        if (!Status.CanBeConfirmed())
            return Result.Failure(Error.Validation("Status", "Cette commande ne peut pas être confirmée dans son état actuel"));

        if (_lines.Count == 0)
            return Result.Failure(Error.Validation("Lines", "La commande doit contenir au moins une ligne"));

        ConfirmedAt = DateTime.UtcNow;
        Status = PurchaseOrderStatus.Confirmed;

        AddDomainEvent(new PurchaseOrderConfirmedEvent(Id, Number.Value, SupplierId));

        return Result.Success();
    }

    /// <summary>
    /// Records the reception of goods for specified lines.
    /// This is the key integration point with the stock module.
    /// </summary>
    public Result ReceiveGoods(IEnumerable<(Guid LineId, decimal ReceivedQuantity)> receptions)
    {
        if (!Status.CanReceiveGoods())
            return Result.Failure(Error.Validation("Status", "Cette commande ne peut pas recevoir de marchandise dans son état actuel"));

        foreach (var (lineId, receivedQty) in receptions)
        {
            var line = _lines.FirstOrDefault(l => l.Id == lineId);
            if (line is null)
                return Result.Failure(Error.NotFound("PurchaseOrderLine", lineId));

            var result = line.RecordReception(receivedQty);
            if (result.IsFailure)
                return result;
        }

        // Determine reception status then recalculate combined status
        var allReceived = _lines.All(l => l.IsFullyReceived);
        if (allReceived)
            ReceivedAt = DateTime.UtcNow;

        RecalculateStatus();

        var receivedCount = receptions.Count();
        AddDomainEvent(new GoodsReceivedEvent(Id, Number.Value, receivedCount, allReceived));

        return Result.Success();
    }

    /// <summary>
    /// Reverses previously recorded reception quantities (e.g. cancelling a validated BR).
    /// Recalculates PartiallyReceived / Confirmed / Received status.
    /// Not allowed when the order is already invoiced or cancelled.
    /// </summary>
    public Result ReverseGoodsReception(IEnumerable<(Guid LineId, decimal Quantity)> reversals)
    {
        if (Status is PurchaseOrderStatus.Invoiced or PurchaseOrderStatus.Cancelled or PurchaseOrderStatus.Draft)
            return Result.Failure(Error.Validation("Status",
                "Cette commande ne peut pas annuler de réception dans son état actuel"));

        foreach (var (lineId, quantity) in reversals)
        {
            var line = _lines.FirstOrDefault(l => l.Id == lineId);
            if (line is null)
                return Result.Failure(Error.NotFound("PurchaseOrderLine", lineId));

            var result = line.ReverseReception(quantity);
            if (result.IsFailure)
                return result;
        }

        var anyReceived = _lines.Any(l => l.ReceivedQuantity > 0);
        if (!anyReceived)
            ReceivedAt = null;

        RecalculateStatus();

        return Result.Success();
    }

    internal void RecalculateStatus()
    {
        if (Status is PurchaseOrderStatus.Cancelled or PurchaseOrderStatus.Draft)
            return;

        var anyReceived = _lines.Any(l => l.ReceivedQuantity > 0);
        var allReceived = _lines.All(l => l.IsFullyReceived);
        var anyInvoiced = _lines.Any(l => l.InvoicedQuantity > 0);
        var anyReceivedNotInvoiced = _lines.Any(l => l.ReceivedNotInvoicedQuantity > 0);

        if (anyInvoiced && !anyReceivedNotInvoiced)
        {
            Status = PurchaseOrderStatus.Invoiced;
            InvoicedAt ??= DateTime.UtcNow;
            return;
        }

        if (anyInvoiced)
        {
            Status = PurchaseOrderStatus.PartiallyInvoiced;
            return;
        }

        InvoicedAt = null;

        if (allReceived && anyReceived)
        {
            Status = PurchaseOrderStatus.Received;
            ReceivedAt ??= DateTime.UtcNow;
        }
        else if (anyReceived)
        {
            Status = PurchaseOrderStatus.PartiallyReceived;
        }
        else
        {
            Status = PurchaseOrderStatus.Confirmed;
            ReceivedAt = null;
        }
    }

    public Result Cancel(string reason)
    {
        if (!Status.CanBeCancelled())
            return Result.Failure(Error.Validation("Status", "Cette commande ne peut pas être annulée"));

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(Error.Validation("CancellationReason", "Le motif d'annulation est obligatoire"));

        CancelledAt = DateTime.UtcNow;
        CancellationReason = reason.Trim();
        Status = PurchaseOrderStatus.Cancelled;

        AddDomainEvent(new PurchaseOrderCancelledEvent(Id, Number.Value, CancellationReason));

        return Result.Success();
    }

    /// <summary>
    /// Updates header fields (expected delivery date, reference, notes).
    /// Only applicable when the order is in draft and can be edited.
    /// </summary>
    public void UpdateHeader(DateTime? expectedDeliveryDate, string? reference, string? notes)
    {
        if (!Status.CanBeEdited())
            return;

        ExpectedDeliveryDate = expectedDeliveryDate?.Date;
        Reference = reference?.Trim();
        Notes = notes?.Trim();
    }

    public void UpdateNotes(string? notes)
    {
        if (Status.CanBeEdited())
        {
            Notes = notes?.Trim();
        }
    }

    private void RecalculateTotals()
    {
        var currency = Money.DefaultCurrency;

        SubTotal = _lines.Aggregate(
            Money.Zero(currency),
            (sum, line) => sum.Add(line.SubTotal));

        TotalVat = _lines.Aggregate(
            Money.Zero(currency),
            (sum, line) => sum.Add(line.VatAmount));

        TotalAmount = SubTotal.Add(TotalVat);
    }

    private void RenumberLines()
    {
        for (int i = 0; i < _lines.Count; i++)
        {
            _lines[i].SetLineNumber(i + 1);
        }
    }

    public Dictionary<VatRate, Money> GetVatBreakdown()
    {
        return _lines
            .GroupBy(l => l.VatRate)
            .ToDictionary(
                g => g.Key,
                g => g.Aggregate(Money.Zero(), (sum, line) => sum.Add(line.VatAmount)));
    }
}
