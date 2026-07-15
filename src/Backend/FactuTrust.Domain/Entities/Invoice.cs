using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Events;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Represents an invoice (facture) - the core aggregate of the system.
/// </summary>
public sealed class Invoice : AggregateRoot
{
    public InvoiceNumber Number { get; private set; } = null!;
    public DateTime IssueDate { get; private set; }
    public DateTime? DueDate { get; private set; }
    public InvoiceStatus Status { get; private set; }

    /// <summary>
    /// Discriminates a regular sales invoice from a credit note (facture d'avoir).
    /// Drives totals signing, stock direction, accounting routing and PDF rendering.
    /// </summary>
    public InvoiceType Type { get; private set; }

    /// <summary>True when this document is a credit note (AVO) — totals are signed negative.</summary>
    public bool IsCreditNote => Type == InvoiceType.CreditNote;
    
    public Guid ClientId { get; private set; }
    public Client Client { get; private set; } = null!;

    public string? Reference { get; private set; }
    public string? Notes { get; private set; }
    public string? PaymentTerms { get; private set; }
    
    // Payment info
    public string? PaymentMethod { get; private set; }
    public string? BankName { get; private set; }
    public string? Iban { get; private set; }
    public string? Rib { get; private set; }
    
    private readonly List<string> _legalMentions = new();
    public IReadOnlyCollection<string> LegalMentions => _legalMentions.AsReadOnly();

    private readonly List<InvoiceLine> _lines = new();
    public IReadOnlyCollection<InvoiceLine> Lines => _lines.AsReadOnly();

    public Money SubTotal { get; private set; } = null!;
    public Money FodecAmount { get; private set; } = null!;
    public Money TotalVat { get; private set; } = null!;
    /// <summary>
    /// Timbre fiscal (document-level). Signed: positive on sales invoices, negative on credit notes when applicable.
    /// </summary>
    public Money FiscalStampAmount { get; private set; } = null!;
    public Money TotalAmount { get; private set; } = null!;

    public string? SignatureHash { get; private set; }
    public DateTime? SignedAt { get; private set; }
    public string? SignedBy { get; private set; }

    public DateTime? SentAt { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    /// <summary>
    /// Source quote ID when invoice was created from an accepted quote.
    /// Ensures permanent link quote ↔ invoice.
    /// </summary>
    public Guid? SourceQuoteId { get; private set; }

    /// <summary>
    /// Issuing company (seller) for PDF/branding. When null, the tenant default company is used at render time.
    /// </summary>
    public Guid? IssuerCompanyId { get; private set; }

    /// <summary>
    /// E-invoice platform reference (e.g. TTN) when submitted.
    /// </summary>
    public string? ElectronicInvoiceTtn { get; private set; }

    /// <summary>
    /// When the e-invoice was sent to the platform (display only).
    /// </summary>
    public DateTime? ElectronicInvoiceSentAt { get; private set; }

    /// <summary>
    /// Optional warehouse for stock deduction. When null, the default warehouse is used.
    /// </summary>
    public Guid? WarehouseId { get; private set; }
    public Warehouse? Warehouse { get; private set; }

    private Invoice() { }

    public static Result<Invoice> Create(
        InvoiceNumber number,
        Client client,
        DateTime issueDate,
        DateTime? dueDate = null,
        string? reference = null,
        string? notes = null,
        string? paymentTerms = null,
        Guid? warehouseId = null,
        InvoiceType type = InvoiceType.Standard)
    {
        if (dueDate.HasValue && dueDate.Value < issueDate)
            return Result.Failure<Invoice>(Error.Validation("DueDate", "La date d'échéance ne peut pas être antérieure à la date d'émission"));

        var invoice = new Invoice
        {
            Number = number,
            ClientId = client.Id,
            Client = client,
            IssueDate = issueDate.Date,
            DueDate = dueDate?.Date,
            Status = InvoiceStatus.Draft,
            Type = type,
            Reference = reference?.Trim(),
            Notes = notes?.Trim(),
            PaymentTerms = paymentTerms?.Trim(),
            SubTotal = Money.Zero(),
            FodecAmount = Money.Zero(),
            TotalVat = Money.Zero(),
            FiscalStampAmount = Money.Zero(),
            TotalAmount = Money.Zero(),
            WarehouseId = warehouseId
        };

        invoice.AddDomainEvent(new InvoiceCreatedEvent(invoice.Id, invoice.Number.Value, client.Id));

        return Result.Success(invoice);
    }

    /// <summary>
    /// Creates an invoice from an accepted quote. Sets SourceQuoteId for permanent link.
    /// </summary>
    public static Result<Invoice> CreateFromQuote(
        InvoiceNumber number,
        Client client,
        DateTime issueDate,
        Guid sourceQuoteId,
        DateTime? dueDate = null,
        string? reference = null,
        string? notes = null,
        string? paymentTerms = null,
        Guid? warehouseId = null)
    {
        if (dueDate.HasValue && dueDate.Value < issueDate)
            return Result.Failure<Invoice>(Error.Validation("DueDate", "La date d'échéance ne peut pas être antérieure à la date d'émission"));

        var invoice = new Invoice
        {
            Number = number,
            ClientId = client.Id,
            Client = client,
            IssueDate = issueDate.Date,
            DueDate = dueDate?.Date,
            Status = InvoiceStatus.Draft,
            Reference = reference?.Trim(),
            Notes = notes?.Trim(),
            PaymentTerms = paymentTerms?.Trim(),
            SubTotal = Money.Zero(),
            FodecAmount = Money.Zero(),
            TotalVat = Money.Zero(),
            FiscalStampAmount = Money.Zero(),
            TotalAmount = Money.Zero(),
            SourceQuoteId = sourceQuoteId,
            WarehouseId = warehouseId
        };

        invoice.AddDomainEvent(new InvoiceCreatedEvent(invoice.Id, invoice.Number.Value, client.Id));
        return Result.Success(invoice);
    }

    public Result AddLine(
        Product product,
        decimal quantity,
        Money? customUnitPrice = null,
        decimal? discountPercent = null,
        decimal fodecRatePercent = 1.0m)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Cette facture ne peut plus être modifiée"));

        if (quantity <= 0)
            return Result.Failure(Error.Validation("Quantity", "La quantité doit être supérieure à zéro"));

        var unitPrice = customUnitPrice ?? product.UnitPrice;
        var lineNumber = _lines.Count + 1;

        var lineResult = InvoiceLine.Create(
            this,
            lineNumber,
            product,
            quantity,
            unitPrice,
            discountPercent,
            fodecRatePercent);

        if (lineResult.IsFailure)
            return Result.Failure(lineResult.Error);

        _lines.Add(lineResult.Value);
        RecalculateTotals();

        return Result.Success();
    }

    public Result RemoveLine(Guid lineId)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Cette facture ne peut plus être modifiée"));

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            return Result.Failure(Error.NotFound("InvoiceLine", lineId));

        _lines.Remove(line);
        RenumberLines();
        RecalculateTotals();

        return Result.Success();
    }

    public Result UpdateLine(
        Guid lineId,
        decimal quantity,
        Money? customUnitPrice = null,
        decimal? discountPercent = null,
        decimal? fodecRatePercent = null)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Cette facture ne peut plus être modifiée"));

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            return Result.Failure(Error.NotFound("InvoiceLine", lineId));

        var result = line.Update(quantity, customUnitPrice, discountPercent, fodecRatePercent);
        if (result.IsFailure)
            return result;

        RecalculateTotals();

        return Result.Success();
    }

    public Result Validate()
    {
        if (Status != InvoiceStatus.Draft)
            return Result.Failure(Error.Validation("Status", "Seuls les brouillons peuvent être validés"));

        if (_lines.Count == 0)
            return Result.Failure(Error.Validation("Lines", "La facture doit contenir au moins une ligne"));

        Status = InvoiceStatus.Validated;
        AddDomainEvent(new InvoiceValidatedEvent(Id, Number.Value, IsCreditNote));

        return Result.Success();
    }

    public Result Sign(string signatureHash, string signedBy)
    {
        if (!Status.CanBeSigned())
            return Result.Failure(Error.Validation("Status", "Cette facture ne peut pas être signée dans son état actuel"));

        if (string.IsNullOrWhiteSpace(signatureHash))
            return Result.Failure(Error.Validation("SignatureHash", "La signature est invalide"));

        SignatureHash = signatureHash;
        SignedAt = DateTime.UtcNow;
        SignedBy = signedBy;
        Status = InvoiceStatus.Signed;

        AddDomainEvent(new InvoiceSignedEvent(Id, Number.Value, signatureHash));

        return Result.Success();
    }

    /// <summary>
    /// Records the date when the invoice was sent by email (for audit trail).
    /// Does not change the invoice status.
    /// </summary>
    public void SetSentAt(DateTime sentAt)
    {
        SentAt = sentAt;
    }

    /// <summary>
    /// Reconciles the invoice status based on the total amount paid and the last payment date.
    /// Called after a new payment is recorded to update Status and PaidAt.
    /// For credit notes, totalAmount is negative; we compare on absolute magnitudes since
    /// payment rows are themselves stored as positive amounts (refunds use the same scalar).
    /// </summary>
    public void ReconcilePaymentStatus(decimal totalPaid, DateTime? lastPaymentDate)
    {
        if (totalPaid <= 0)
            return;

        var dueMagnitude = Math.Abs(TotalAmount.Amount);
        if (totalPaid > dueMagnitude)
            return; // Overpayment not supported; validation should prevent this

        PaidAt = lastPaymentDate;

        if (totalPaid >= dueMagnitude)
        {
            Status = InvoiceStatus.Paid;
            AddDomainEvent(new InvoicePaidEvent(Id, Number.Value, totalPaid));
        }
        else
        {
            Status = InvoiceStatus.PartiallyPaid;
        }
    }

    /// <summary>
    /// Réouvre une facture dont un effet (traite) est revenu impayé : recalcule le statut à partir du
    /// total réellement encaissé (l'effet impayé, marqué remboursé, en est déjà exclu). Retour à
    /// « Validée » si plus rien n'est réglé, sinon « Partiellement payée » / « Payée ». La facture reste
    /// du chiffre d'affaires réalisé (Payée comme Validée), le produit ayant déjà été constaté.
    /// </summary>
    public void ReopenAfterEffetUnpaid(decimal remainingPaidTotal, DateTime? lastPaymentDate)
    {
        var dueMagnitude = Math.Abs(TotalAmount.Amount);
        if (remainingPaidTotal <= 0)
        {
            Status = InvoiceStatus.Validated;
            PaidAt = null;
        }
        else if (remainingPaidTotal >= dueMagnitude)
        {
            Status = InvoiceStatus.Paid;
            PaidAt = lastPaymentDate;
        }
        else
        {
            Status = InvoiceStatus.PartiallyPaid;
            PaidAt = lastPaymentDate;
        }
    }

    public Result Cancel(string reason)
    {
        if (!Status.CanBeCancelled())
            return Result.Failure(Error.Validation("Status", "Cette facture ne peut pas être annulée"));

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(Error.Validation("CancellationReason", "Le motif d'annulation est obligatoire"));

        CancelledAt = DateTime.UtcNow;
        CancellationReason = reason.Trim();
        Status = InvoiceStatus.Cancelled;

        AddDomainEvent(new InvoiceCancelledEvent(Id, Number.Value, reason));

        return Result.Success();
    }

    public Result Archive()
    {
        if (Status is not InvoiceStatus.Paid and not InvoiceStatus.Cancelled)
            return Result.Failure(Error.Validation("Status", "Seules les factures payées ou annulées peuvent être archivées"));

        Status = InvoiceStatus.Archived;
        AddDomainEvent(new InvoiceArchivedEvent(Id, Number.Value));

        return Result.Success();
    }

    public void UpdateNotes(string? notes, string? paymentTerms)
    {
        if (Status.CanBeEdited())
        {
            Notes = notes?.Trim();
            PaymentTerms = paymentTerms?.Trim();
        }
    }

    /// <summary>
    /// Adds a custom invoice line without product reference.
    /// </summary>
    public Result AddCustomLine(
        string designation,
        string? description,
        decimal quantity,
        string unit,
        Money unitPrice,
        VatRate vatRate,
        decimal? discountPercent = null,
        bool isFodecApplicable = false,
        decimal fodecRatePercent = 1.0m)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Cette facture ne peut plus être modifiée"));

        if (quantity <= 0)
            return Result.Failure(Error.Validation("Quantity", "La quantité doit être supérieure à zéro"));

        if (string.IsNullOrWhiteSpace(designation))
            return Result.Failure(Error.Validation("Designation", "La désignation est obligatoire"));

        var lineNumber = _lines.Count + 1;

        var lineResult = InvoiceLine.CreateCustom(
            this,
            lineNumber,
            designation.Trim(),
            description?.Trim(),
            quantity,
            unit,
            unitPrice,
            vatRate,
            discountPercent,
            isFodecApplicable,
            fodecRatePercent);

        if (lineResult.IsFailure)
            return Result.Failure(lineResult.Error);

        _lines.Add(lineResult.Value);
        RecalculateTotals();

        return Result.Success();
    }

    /// <summary>
    /// Sets payment information on the invoice.
    /// </summary>
    public void SetPaymentInfo(string? method, string? bankName, string? iban, string? rib)
    {
        if (Status.CanBeEdited())
        {
            PaymentMethod = method?.Trim();
            BankName = bankName?.Trim();
            Iban = iban?.Trim();
            Rib = rib?.Trim();
        }
    }

    /// <summary>
    /// Sets the issuing company for this invoice (typically at creation from the wizard).
    /// </summary>
    public void SetIssuerCompanyId(Guid? issuerCompanyId)
    {
        IssuerCompanyId = issuerCompanyId;
    }

    /// <summary>
    /// Sets the fiscal stamp amount for the document (from tenant tax catalog). Only while editable.
    /// </summary>
    public Result SetFiscalStampAmount(Money signedStamp)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Cette facture ne peut plus être modifiée"));

        if (signedStamp.Currency != SubTotal.Currency)
            return Result.Failure(Error.Validation("FiscalStampAmount", "La devise du timbre ne correspond pas à la facture"));

        FiscalStampAmount = signedStamp;
        RecalculateTotals();
        return Result.Success();
    }

    /// <summary>
    /// Sets e-invoice submission metadata for PDF QR block (optional).
    /// </summary>
    public void SetElectronicInvoiceInfo(string? ttn, DateTime? sentAt)
    {
        ElectronicInvoiceTtn = string.IsNullOrWhiteSpace(ttn) ? null : ttn.Trim();
        ElectronicInvoiceSentAt = sentAt;
    }

    /// <summary>
    /// Adds a legal mention to the invoice.
    /// </summary>
    public void AddLegalMention(string mention)
    {
        if (!string.IsNullOrWhiteSpace(mention) && Status.CanBeEdited())
        {
            _legalMentions.Add(mention.Trim());
        }
    }

    public void CheckOverdue()
    {
        if (DueDate.HasValue && 
            DueDate.Value < DateTime.UtcNow.Date && 
            (Status == InvoiceStatus.Signed || Status == InvoiceStatus.Validated))
        {
            Status = InvoiceStatus.Overdue;
            AddDomainEvent(new InvoiceOverdueEvent(Id, Number.Value, DueDate.Value));
        }
    }

    private void RecalculateTotals()
    {
        var currency = Money.DefaultCurrency;
        if (_lines.Count > 0)
            currency = _lines[0].SubTotal.Currency;

        // Lines stay positive (5 units of "bureau" remain 5 units regardless of direction).
        // The header carries the sign: negative for credit notes, positive for regular sales.
        var sumLinesHt = _lines.Aggregate(0m, (sum, line) => sum + line.SubTotal.Amount);
        var sumLinesFodec = _lines.Aggregate(0m, (sum, line) => sum + line.FodecAmount.Amount);
        var sumLinesVat = _lines.Aggregate(0m, (sum, line) => sum + line.VatAmount.Amount);
        var sign = Type == InvoiceType.CreditNote ? -1m : 1m;

        SubTotal = Money.FromSignedAmount(sign * sumLinesHt, currency);
        FodecAmount = Money.FromSignedAmount(sign * sumLinesFodec, currency);
        TotalVat = Money.FromSignedAmount(sign * sumLinesVat, currency);

        // FiscalStampAmount is already signed by FiscalStampResolver (negative for AVO);
        // do not re-apply the sign here.
        var stamp = FiscalStampAmount.Currency == currency
            ? FiscalStampAmount.Amount
            : 0m;
        TotalAmount = Money.FromSignedAmount(SubTotal.Amount + FodecAmount.Amount + TotalVat.Amount + stamp, currency);
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
