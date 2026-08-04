using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Represents a supplier invoice (facture fournisseur) created from a received purchase order
/// and/or purchase receipt. Tracks amounts owed to suppliers and payment status.
/// </summary>
public sealed class SupplierInvoice : AggregateRoot
{
    public string InvoiceNumber { get; private set; } = null!;
    public DateTime InvoiceDate { get; private set; }
    public DateTime DueDate { get; private set; }
    public SupplierInvoiceStatus Status { get; private set; }

    public Guid SupplierId { get; private set; }
    public Supplier Supplier { get; private set; } = null!;

    public Guid PurchaseOrderId { get; private set; }
    public PurchaseOrder PurchaseOrder { get; private set; } = null!;

    public Guid? SourcePurchaseReceiptId { get; private set; }
    public PurchaseReceipt? SourcePurchaseReceipt { get; private set; }

    public string? ExternalReference { get; private set; }
    public string? Notes { get; private set; }

    public string? PaymentMethod { get; private set; }

    public Guid? WarehouseId { get; private set; }
    public Warehouse? Warehouse { get; private set; }

    private readonly List<SupplierInvoiceLine> _lines = new();
    public IReadOnlyCollection<SupplierInvoiceLine> Lines => _lines.AsReadOnly();

    private readonly List<SupplierPayment> _payments = new();
    public IReadOnlyCollection<SupplierPayment> Payments => _payments.AsReadOnly();

    public Money SubTotal { get; private set; } = null!;
    public Money TotalVat { get; private set; } = null!;
    public Money FiscalStampAmount { get; private set; } = null!;
    public Money TotalAmount { get; private set; } = null!;

    public DateTime? PaidAt { get; private set; }
    public string? PaymentReference { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    public bool IsSubjectToWithholding { get; private set; }
    public decimal? WithholdingRate { get; private set; }
    public decimal? WithholdingAmount { get; private set; }
    public Guid? WithholdingTaxTypeId { get; private set; }
    public decimal? NetAmountAfterWithholding { get; private set; }

    private SupplierInvoice() { }

    public static Result<SupplierInvoice> CreateFromPurchaseOrder(
        PurchaseOrder purchaseOrder,
        string invoiceNumber,
        DateTime invoiceDate,
        IReadOnlyList<(Guid PurchaseOrderLineId, decimal Quantity)> lineSelections,
        int paymentTermDays = 30,
        string? externalReference = null,
        string? notes = null,
        string? paymentMethod = null)
    {
        if (!purchaseOrder.Status.CanBeInvoiced())
            return Result.Failure<SupplierInvoice>(Error.Validation("Status",
                "Une facture fournisseur ne peut être créée qu'à partir d'une commande reçue"));

        if (lineSelections.Count == 0)
            return Result.Failure<SupplierInvoice>(Error.Validation("Lines",
                "Sélectionnez au moins une ligne à facturer"));

        if (string.IsNullOrWhiteSpace(invoiceNumber))
            return Result.Failure<SupplierInvoice>(Error.Validation("InvoiceNumber",
                "Le numéro de facture fournisseur est obligatoire"));

        var invoice = CreateShell(
            purchaseOrder.Supplier,
            purchaseOrder,
            null,
            invoiceNumber,
            invoiceDate,
            paymentTermDays,
            externalReference,
            notes,
            paymentMethod,
            purchaseOrder.WarehouseId);

        var lineNumber = 1;
        foreach (var (poLineId, quantity) in lineSelections)
        {
            if (quantity <= 0)
                continue;

            var poLine = purchaseOrder.Lines.FirstOrDefault(l => l.Id == poLineId);
            if (poLine is null)
                return Result.Failure<SupplierInvoice>(Error.NotFound("PurchaseOrderLine", poLineId));

            if (quantity > poLine.ReceivedNotInvoicedQuantity)
                return Result.Failure<SupplierInvoice>(Error.Validation("Quantity",
                    $"Quantité à facturer invalide sur la ligne {poLine.LineNumber}"));

            var lineResult = BuildLineFromPurchaseOrderLine(invoice, lineNumber++, poLine, quantity);
            if (lineResult.IsFailure)
                return Result.Failure<SupplierInvoice>(lineResult.Error);

            invoice._lines.Add(lineResult.Value);
        }

        if (invoice._lines.Count == 0)
            return Result.Failure<SupplierInvoice>(Error.Validation("Lines",
                "Aucune ligne avec une quantité à facturer"));

        invoice.RecalculateTotals();
        return Result.Success(invoice);
    }

    public static Result<SupplierInvoice> CreateFromPurchaseReceipt(
        PurchaseReceipt receipt,
        PurchaseOrder purchaseOrder,
        string invoiceNumber,
        DateTime invoiceDate,
        IReadOnlyList<(Guid PurchaseReceiptLineId, decimal Quantity)> lineSelections,
        int paymentTermDays = 30,
        string? externalReference = null,
        string? notes = null,
        string? paymentMethod = null)
    {
        if (!receipt.Status.CanBeInvoiced())
            return Result.Failure<SupplierInvoice>(Error.Validation("Status",
                "Ce bon de réception ne peut pas être facturé dans son état actuel"));

        if (receipt.PurchaseOrderId is { } poId && poId != purchaseOrder.Id)
            return Result.Failure<SupplierInvoice>(Error.Validation("PurchaseOrder",
                "Le bon de commande ne correspond pas au bon de réception"));

        if (lineSelections.Count == 0)
            return Result.Failure<SupplierInvoice>(Error.Validation("Lines",
                "Sélectionnez au moins une ligne à facturer"));

        if (string.IsNullOrWhiteSpace(invoiceNumber))
            return Result.Failure<SupplierInvoice>(Error.Validation("InvoiceNumber",
                "Le numéro de facture fournisseur est obligatoire"));

        var invoice = CreateShell(
            receipt.Supplier,
            purchaseOrder,
            receipt,
            invoiceNumber,
            invoiceDate,
            paymentTermDays,
            externalReference,
            notes,
            paymentMethod,
            receipt.WarehouseId);

        var lineNumber = 1;
        foreach (var (prLineId, quantity) in lineSelections)
        {
            if (quantity <= 0)
                continue;

            var prLine = receipt.Lines.FirstOrDefault(l => l.Id == prLineId);
            if (prLine is null)
                return Result.Failure<SupplierInvoice>(Error.NotFound("PurchaseReceiptLine", prLineId));

            if (quantity > prLine.ReceivedNotInvoicedQuantity)
                return Result.Failure<SupplierInvoice>(Error.Validation("Quantity",
                    $"Quantité à facturer invalide sur la ligne {prLine.LineNumber}"));

            var lineResult = BuildLineFromPurchaseReceiptLine(invoice, lineNumber++, prLine, quantity);
            if (lineResult.IsFailure)
                return Result.Failure<SupplierInvoice>(lineResult.Error);

            invoice._lines.Add(lineResult.Value);
        }

        if (invoice._lines.Count == 0)
            return Result.Failure<SupplierInvoice>(Error.Validation("Lines",
                "Aucune ligne avec une quantité à facturer"));

        invoice.RecalculateTotals();
        return Result.Success(invoice);
    }

    /// <summary>
    /// Reassigns the invoice number. Reserved for the persistence pipeline when a duplicate
    /// key exception forces an atomic re-reservation (see SupplierInvoiceCreationHelper retry loop).
    /// Must be called before the aggregate is persisted; the new number is validated non-empty.
    /// </summary>
    public Result ReassignInvoiceNumberForRetry(string newInvoiceNumber)
    {
        if (string.IsNullOrWhiteSpace(newInvoiceNumber))
            return Result.Failure(Error.Validation("InvoiceNumber",
                "Le numéro de facture fournisseur est obligatoire"));

        InvoiceNumber = newInvoiceNumber.Trim();
        return Result.Success();
    }

    public IReadOnlyList<(Guid PurchaseOrderLineId, decimal Quantity)> GetPurchaseOrderImputations() =>
        _lines
            .Where(l => l.PurchaseOrderLineId.HasValue)
            .GroupBy(l => l.PurchaseOrderLineId!.Value)
            .Select(g => (g.Key, g.Sum(l => l.Quantity)))
            .ToList();

    public IReadOnlyList<(Guid PurchaseReceiptLineId, decimal Quantity)> GetPurchaseReceiptImputations() =>
        _lines
            .Where(l => l.PurchaseReceiptLineId.HasValue)
            .GroupBy(l => l.PurchaseReceiptLineId!.Value)
            .Select(g => (g.Key, g.Sum(l => l.Quantity)))
            .ToList();

    private static SupplierInvoice CreateShell(
        Supplier supplier,
        PurchaseOrder purchaseOrder,
        PurchaseReceipt? sourceReceipt,
        string invoiceNumber,
        DateTime invoiceDate,
        int paymentTermDays,
        string? externalReference,
        string? notes,
        string? paymentMethod,
        Guid? warehouseId)
    {
        return new SupplierInvoice
        {
            InvoiceNumber = invoiceNumber.Trim(),
            InvoiceDate = invoiceDate.Date,
            DueDate = invoiceDate.Date.AddDays(paymentTermDays),
            Status = SupplierInvoiceStatus.Pending,
            SupplierId = supplier.Id,
            Supplier = supplier,
            PurchaseOrderId = purchaseOrder.Id,
            PurchaseOrder = purchaseOrder,
            SourcePurchaseReceiptId = sourceReceipt?.Id,
            SourcePurchaseReceipt = sourceReceipt,
            ExternalReference = externalReference?.Trim(),
            Notes = notes?.Trim(),
            PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? null : paymentMethod.Trim(),
            WarehouseId = warehouseId,
            SubTotal = Money.Zero(),
            TotalVat = Money.Zero(),
            FiscalStampAmount = Money.Zero(),
            TotalAmount = Money.Zero()
        };
    }

    private static Result<SupplierInvoiceLine> BuildLineFromPurchaseOrderLine(
        SupplierInvoice invoice,
        int lineNumber,
        PurchaseOrderLine poLine,
        decimal quantity)
    {
        var unitPrice = poLine.UnitPrice;
        var subTotal = unitPrice.Multiply(quantity);
        var vatAmount = subTotal.ApplyPercentage(poLine.VatRate.ToDecimal());
        var total = subTotal.Add(vatAmount);

        return Result.Success(SupplierInvoiceLine.Create(
            invoice,
            lineNumber,
            poLine.ProductId,
            poLine.ProductCode,
            poLine.ProductName,
            poLine.ProductDescription,
            quantity,
            poLine.Unit,
            unitPrice,
            poLine.VatRate,
            subTotal,
            vatAmount,
            total,
            purchaseOrderLineId: poLine.Id));
    }

    private static Result<SupplierInvoiceLine> BuildLineFromPurchaseReceiptLine(
        SupplierInvoice invoice,
        int lineNumber,
        PurchaseReceiptLine prLine,
        decimal quantity)
    {
        var gross = prLine.UnitPrice.Multiply(quantity);
        var discountFactor = 1m - ((prLine.DiscountPercent ?? 0m) / 100m);
        var subTotal = gross.Multiply(discountFactor);
        var vatAmount = subTotal.ApplyPercentage(prLine.VatRate.ToDecimal());
        var total = subTotal.Add(vatAmount);

        return Result.Success(SupplierInvoiceLine.Create(
            invoice,
            lineNumber,
            prLine.ProductId,
            prLine.ProductCode,
            prLine.ProductName,
            prLine.ProductDescription,
            quantity,
            prLine.Unit,
            prLine.UnitPrice,
            prLine.VatRate,
            subTotal,
            vatAmount,
            total,
            purchaseOrderLineId: prLine.PurchaseOrderLineId,
            purchaseReceiptLineId: prLine.Id));
    }

    private void RecalculateTotals()
    {
        var currency = _lines.First().SubTotal.Currency;
        SubTotal = _lines.Aggregate(Money.Zero(currency), (sum, line) => sum.Add(line.SubTotal));
        TotalVat = _lines.Aggregate(Money.Zero(currency), (sum, line) => sum.Add(line.VatAmount));
        FiscalStampAmount = Money.Create(0, currency);
        TotalAmount = SubTotal.Add(TotalVat).Add(FiscalStampAmount);
    }

    public void SetPaymentMethod(string? paymentMethod)
    {
        PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? null : paymentMethod.Trim();
    }

    public void ApplyLineAssetClassifications(IReadOnlyList<(int LineNumber, bool IsFixedAsset, string? AssetAccountNumber, Guid? DepreciationRateCategoryId)> classifications)
    {
        if (classifications.Count == 0)
            return;

        foreach (var c in classifications)
        {
            var line = _lines.FirstOrDefault(l => l.LineNumber == c.LineNumber);
            line?.SetFixedAssetClassification(c.IsFixedAsset, c.AssetAccountNumber, c.DepreciationRateCategoryId);
        }
    }

    public Result<SupplierPayment> RecordPayment(
        Money amount,
        DateTime paymentDate,
        PaymentMethod method,
        string? reference = null,
        string? notes = null,
        DateTime? effetDueDate = null)
    {
        if (Status != SupplierInvoiceStatus.Pending && Status != SupplierInvoiceStatus.PartiallyPaid)
            return Result.Failure<SupplierPayment>(Error.Validation("Status", "Seule une facture en attente ou partiellement payée peut recevoir un paiement"));

        var totalPaid = _payments.Sum(p => p.Amount.Amount);
        var remainingAmount = TotalAmount.Amount - totalPaid;

        if (amount.Amount <= 0)
            return Result.Failure<SupplierPayment>(Error.Validation("Amount", "Le montant doit être positif"));

        if (amount.Amount > remainingAmount)
            return Result.Failure<SupplierPayment>(Error.Validation("Amount", $"Le montant ne peut pas dépasser le restant dû ({remainingAmount:N3} {TotalAmount.Currency})"));

        var paymentResult = SupplierPayment.Create(this, amount, paymentDate, method, reference, notes, effetDueDate);
        if (paymentResult.IsFailure)
            return paymentResult;

        var payment = paymentResult.Value;
        _payments.Add(payment);

        var newTotalPaid = totalPaid + amount.Amount;
        var lastPaymentDate = _payments.Max(p => p.PaymentDate);
        ReconcilePaymentStatus(newTotalPaid, lastPaymentDate, payment.Reference);

        IncrementVersion();

        return Result.Success(payment);
    }

    public void ReconcilePaymentStatus(decimal totalPaid, DateTime lastPaymentDate, string? lastPaymentReference = null)
    {
        if (totalPaid <= 0)
            return;

        if (totalPaid > TotalAmount.Amount)
            return;

        PaidAt = lastPaymentDate;
        PaymentReference = lastPaymentReference?.Trim();

        if (totalPaid >= TotalAmount.Amount)
            Status = SupplierInvoiceStatus.Paid;
        else
            Status = SupplierInvoiceStatus.PartiallyPaid;
    }

    public Result Cancel(string reason)
    {
        if (Status == SupplierInvoiceStatus.Cancelled)
            return Result.Failure(Error.Validation("Status", "Cette facture est déjà annulée"));

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(Error.Validation("Reason", "Le motif d'annulation est obligatoire"));

        Status = SupplierInvoiceStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        CancellationReason = reason.Trim();

        return Result.Success();
    }

    public void SetFiscalStampAmount(decimal amount)
    {
        FiscalStampAmount = Money.Create(amount, TotalAmount.Currency);
    }

    public void SetWithholdingInfo(
        bool isSubjectToWithholding,
        decimal? withholdingRate,
        decimal? withholdingAmount,
        Guid? withholdingTaxTypeId)
    {
        IsSubjectToWithholding = isSubjectToWithholding;
        WithholdingRate = withholdingRate;
        WithholdingAmount = withholdingAmount;
        WithholdingTaxTypeId = withholdingTaxTypeId;
        NetAmountAfterWithholding = isSubjectToWithholding && withholdingAmount.HasValue
            ? TotalAmount.Amount - withholdingAmount.Value
            : TotalAmount.Amount;
    }
}
