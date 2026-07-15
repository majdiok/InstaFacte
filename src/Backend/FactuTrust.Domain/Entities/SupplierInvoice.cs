using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Represents a supplier invoice (facture fournisseur) created from a received purchase order.
/// Tracks amounts owed to suppliers and payment status.
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

    public string? ExternalReference { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>
    /// Mode de paiement prévu (informatif : « Effet de commerce », « Virement bancaire »…). Le mode
    /// réel qui pilote la comptabilité est porté par chaque <see cref="SupplierPayment"/>. Miroir de
    /// <see cref="Invoice.PaymentMethod"/> (chaîne libre).
    /// </summary>
    public string? PaymentMethod { get; private set; }

    /// <summary>
    /// Inherited from the source PurchaseOrder. Indicates the warehouse where goods were received.
    /// </summary>
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

    // Withholding tax (retenue à la source)
    public bool IsSubjectToWithholding { get; private set; }
    public decimal? WithholdingRate { get; private set; }
    public decimal? WithholdingAmount { get; private set; }
    public Guid? WithholdingTaxTypeId { get; private set; }
    public decimal? NetAmountAfterWithholding { get; private set; }

    private SupplierInvoice() { }

    /// <summary>
    /// Creates a supplier invoice from a received purchase order.
    /// </summary>
    public static Result<SupplierInvoice> CreateFromPurchaseOrder(
        PurchaseOrder purchaseOrder,
        string invoiceNumber,
        DateTime invoiceDate,
        int paymentTermDays = 30,
        string? externalReference = null,
        string? notes = null,
        string? paymentMethod = null)
    {
        if (purchaseOrder.Status != PurchaseOrderStatus.Received &&
            purchaseOrder.Status != PurchaseOrderStatus.PartiallyReceived)
            return Result.Failure<SupplierInvoice>(Error.Validation("Status",
                "Une facture fournisseur ne peut être créée qu'à partir d'une commande reçue"));

        if (string.IsNullOrWhiteSpace(invoiceNumber))
            return Result.Failure<SupplierInvoice>(Error.Validation("InvoiceNumber",
                "Le numéro de facture fournisseur est obligatoire"));

        var invoice = new SupplierInvoice
        {
            InvoiceNumber = invoiceNumber.Trim(),
            InvoiceDate = invoiceDate.Date,
            DueDate = invoiceDate.Date.AddDays(paymentTermDays),
            Status = SupplierInvoiceStatus.Pending,
            SupplierId = purchaseOrder.SupplierId,
            Supplier = purchaseOrder.Supplier,
            PurchaseOrderId = purchaseOrder.Id,
            PurchaseOrder = purchaseOrder,
            ExternalReference = externalReference?.Trim(),
            Notes = notes?.Trim(),
            PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? null : paymentMethod.Trim(),
            WarehouseId = purchaseOrder.WarehouseId,
            SubTotal = Money.Create(purchaseOrder.SubTotal.Amount, purchaseOrder.SubTotal.Currency),
            TotalVat = Money.Create(purchaseOrder.TotalVat.Amount, purchaseOrder.TotalVat.Currency),
            FiscalStampAmount = Money.Create(0, purchaseOrder.SubTotal.Currency),
            TotalAmount = Money.Create(purchaseOrder.TotalAmount.Amount, purchaseOrder.TotalAmount.Currency)
        };

        // Copy lines from purchase order
        var lineNumber = 1;
        foreach (var poLine in purchaseOrder.Lines)
        {
            var line = SupplierInvoiceLine.Create(
                invoice,
                lineNumber++,
                poLine.ProductId,
                poLine.ProductCode,
                poLine.ProductName,
                poLine.ProductDescription,
                poLine.ReceivedQuantity > 0 ? poLine.ReceivedQuantity : poLine.Quantity,
                poLine.Unit,
                Money.Create(poLine.UnitPrice.Amount, poLine.UnitPrice.Currency),
                poLine.VatRate,
                Money.Create(poLine.SubTotal.Amount, poLine.SubTotal.Currency),
                Money.Create(poLine.VatAmount.Amount, poLine.VatAmount.Currency),
                Money.Create(poLine.Total.Amount, poLine.Total.Currency));

            invoice._lines.Add(line);
        }

        return Result.Success(invoice);
    }

    /// <summary>Définit/actualise le mode de paiement prévu (informatif) de la facture fournisseur.</summary>
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

    /// <summary>
    /// Records a payment (tranche) on the invoice. Supports multiple partial payments.
    /// </summary>
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

    /// <summary>
    /// Reconciles the invoice status based on total amount paid and last payment date.
    /// Called after a new payment is recorded.
    /// </summary>
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

    /// <summary>
    /// Cancel the supplier invoice.
    /// </summary>
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
