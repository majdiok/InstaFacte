using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities.Honoraires;

/// <summary>Firm honoraires invoice or credit note (parallel to commercial Invoice).</summary>
public sealed class HonorairesInvoice : AggregateRoot
{
    public string? Number { get; private set; }
    public DateTime IssueDate { get; private set; }
    public DateTime? DueDate { get; private set; }
    public HonorairesInvoiceStatus Status { get; private set; }
    public HonorairesDocumentType Type { get; private set; }
    public bool IsCreditNote => Type == HonorairesDocumentType.CreditNote;

    public Guid FirmClientAssignmentId { get; private set; }
    public string ClientName { get; private set; } = null!;
    public string? ClientNif { get; private set; }
    public string? ClientAddress { get; private set; }
    public string? ContactName { get; private set; }
    public string? ContactEmail { get; private set; }
    public string? ContactPhone { get; private set; }

    public string? Reference { get; private set; }
    public string? Notes { get; private set; }
    public string? PaymentTerms { get; private set; }
    public string? PaymentMethod { get; private set; }
    public string? BankAccountLabel { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;

    public Guid? SourceQuoteId { get; private set; }
    public Guid? LinkedInvoiceId { get; private set; }

    public Money SubTotal { get; private set; } = null!;
    public Money TotalVat { get; private set; } = null!;
    public Money WithholdingAmount { get; private set; } = null!;
    public Money TotalAmount { get; private set; } = null!;
    public Money AmountPaid { get; private set; } = null!;

    public bool IsRecurring { get; private set; }
    public BillingFrequency? RecurrenceFrequency { get; private set; }

    public DateTime? PaidAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    private readonly List<HonorairesInvoiceLine> _lines = new();
    public IReadOnlyCollection<HonorairesInvoiceLine> Lines => _lines.AsReadOnly();

    private readonly List<HonorairesPayment> _payments = new();
    public IReadOnlyCollection<HonorairesPayment> Payments => _payments.AsReadOnly();

    public decimal AmountDue => Math.Max(0m, Math.Abs(TotalAmount.Amount) - AmountPaid.Amount);

    private HonorairesInvoice() { }

    public static Result<HonorairesInvoice> CreateDraft(
        Guid firmClientAssignmentId,
        string clientName,
        DateTime issueDate,
        DateTime? dueDate = null,
        string currency = Money.DefaultCurrency,
        HonorairesDocumentType type = HonorairesDocumentType.Invoice,
        Guid? linkedInvoiceId = null)
    {
        if (firmClientAssignmentId == Guid.Empty)
            return Result.Failure<HonorairesInvoice>(Error.Validation("FirmClientAssignmentId", "Le dossier client est obligatoire"));
        if (string.IsNullOrWhiteSpace(clientName))
            return Result.Failure<HonorairesInvoice>(Error.Validation("ClientName", "Le nom du client est obligatoire"));
        if (type == HonorairesDocumentType.CreditNote && (linkedInvoiceId is null || linkedInvoiceId == Guid.Empty))
            return Result.Failure<HonorairesInvoice>(Error.Validation("LinkedInvoiceId", "La facture d'origine est obligatoire pour un avoir"));

        var currencyCode = string.IsNullOrWhiteSpace(currency) ? Money.DefaultCurrency : currency.Trim().ToUpperInvariant();
        return Result.Success(new HonorairesInvoice
        {
            FirmClientAssignmentId = firmClientAssignmentId,
            ClientName = clientName.Trim(),
            IssueDate = issueDate,
            DueDate = dueDate,
            Status = HonorairesInvoiceStatus.Draft,
            Type = type,
            LinkedInvoiceId = linkedInvoiceId,
            Currency = currencyCode,
            SubTotal = Money.Zero(currencyCode),
            TotalVat = Money.Zero(currencyCode),
            WithholdingAmount = Money.Zero(currencyCode),
            TotalAmount = Money.Zero(currencyCode),
            AmountPaid = Money.Zero(currencyCode)
        });
    }

    public static Result<HonorairesInvoice> CreateCreditNoteDraft(
        Guid firmClientAssignmentId,
        string clientName,
        Guid linkedInvoiceId,
        DateTime issueDate,
        DateTime? dueDate = null,
        string currency = Money.DefaultCurrency) =>
        CreateDraft(firmClientAssignmentId, clientName, issueDate, dueDate, currency, HonorairesDocumentType.CreditNote, linkedInvoiceId);

    public void SetClientSnapshot(
        string clientName,
        string? clientNif,
        string? clientAddress,
        string? contactName,
        string? contactEmail,
        string? contactPhone)
    {
        if (!Status.CanBeEdited()) return;
        if (!string.IsNullOrWhiteSpace(clientName))
            ClientName = clientName.Trim();
        ClientNif = TrimOrNull(clientNif);
        ClientAddress = TrimOrNull(clientAddress);
        ContactName = TrimOrNull(contactName);
        ContactEmail = TrimOrNull(contactEmail);
        ContactPhone = TrimOrNull(contactPhone);
    }

    public void SetDates(DateTime issueDate, DateTime? dueDate)
    {
        if (!Status.CanBeEdited()) return;
        IssueDate = issueDate;
        DueDate = dueDate;
    }

    public void SetReference(string? reference)
    {
        if (!Status.CanBeEdited()) return;
        Reference = TrimOrNull(reference);
    }

    public void SetNotes(string? notes)
    {
        if (!Status.CanBeEdited()) return;
        Notes = TrimOrNull(notes);
    }

    public void SetPaymentTerms(string? paymentTerms)
    {
        if (!Status.CanBeEdited()) return;
        PaymentTerms = TrimOrNull(paymentTerms);
    }

    public void SetPaymentInfo(string? paymentMethod, string? bankAccountLabel)
    {
        if (!Status.CanBeEdited()) return;
        PaymentMethod = TrimOrNull(paymentMethod);
        BankAccountLabel = TrimOrNull(bankAccountLabel);
    }

    public Result SetWithholdingAmount(Money amount)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce document ne peut plus être modifié"));
        WithholdingAmount = amount;
        return Result.Success();
    }

    public void SetRecurring(bool isRecurring, BillingFrequency? frequency)
    {
        if (!Status.CanBeEdited()) return;
        IsRecurring = isRecurring;
        RecurrenceFrequency = isRecurring ? frequency : null;
    }

    public void SetSourceQuoteId(Guid quoteId)
    {
        if (!Status.CanBeEdited()) return;
        SourceQuoteId = quoteId;
    }

    public Result AssignNumber(string number)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce document ne peut plus être modifié"));
        if (string.IsNullOrWhiteSpace(number))
            return Result.Failure(Error.Validation("Number", "Le numéro est obligatoire"));
        Number = number.Trim();
        return Result.Success();
    }

    public Result AddLine(
        string designation,
        string? description,
        decimal quantity,
        Money unitPrice,
        VatRate vatRate,
        decimal? discountPercent = null,
        string? activityCode = null)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce document ne peut plus être modifié"));

        var lineResult = HonorairesInvoiceLine.Create(
            this, _lines.Count + 1, designation, description, quantity, unitPrice, vatRate, discountPercent, activityCode);
        if (lineResult.IsFailure)
            return Result.Failure(lineResult.Error);

        _lines.Add(lineResult.Value);
        RecalculateTotals();
        return Result.Success();
    }

    public Result UpdateLine(
        Guid lineId,
        string designation,
        string? description,
        decimal quantity,
        Money unitPrice,
        VatRate vatRate,
        decimal? discountPercent,
        string? activityCode = null)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce document ne peut plus être modifié"));

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            return Result.Failure(Error.NotFound("HonorairesInvoiceLine", lineId));

        var result = line.Update(designation, description, quantity, unitPrice, vatRate, discountPercent, activityCode);
        if (result.IsFailure) return result;
        RecalculateTotals();
        return Result.Success();
    }

    public Result RemoveLine(Guid lineId)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce document ne peut plus être modifié"));

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            return Result.Failure(Error.NotFound("HonorairesInvoiceLine", lineId));

        _lines.Remove(line);
        RenumberLines();
        RecalculateTotals();
        return Result.Success();
    }

    public Result ReplaceLines(IEnumerable<(string? ActivityCode, string Designation, string? Description, decimal Quantity, Money UnitPrice, VatRate VatRate, decimal? DiscountPercent)> lines)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce document ne peut plus être modifié"));

        _lines.Clear();
        foreach (var l in lines)
        {
            var add = AddLine(l.Designation, l.Description, l.Quantity, l.UnitPrice, l.VatRate, l.DiscountPercent, l.ActivityCode);
            if (add.IsFailure) return add;
        }
        IncrementVersion();
        return Result.Success();
    }

    public Result Validate()
    {
        if (Status != HonorairesInvoiceStatus.Draft)
            return Result.Failure(Error.Validation("Status", "Seuls les brouillons peuvent être validés"));
        if (_lines.Count == 0)
            return Result.Failure(Error.Validation("Lines", "La facture doit contenir au moins une ligne"));
        if (string.IsNullOrWhiteSpace(Number))
            return Result.Failure(Error.Validation("Number", "Le numéro doit être réservé avant validation"));
        if (FirmClientAssignmentId == Guid.Empty || string.IsNullOrWhiteSpace(ClientName))
            return Result.Failure(Error.Validation("Client", "Le dossier client est obligatoire"));

        Status = HonorairesInvoiceStatus.Validated;
        IncrementVersion();
        return Result.Success();
    }

    public Result Cancel(string? reason = null)
    {
        if (!Status.CanBeCancelled())
            return Result.Failure(Error.Validation("Status", "Ce document ne peut pas être annulé"));
        Status = HonorairesInvoiceStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        CancellationReason = TrimOrNull(reason);
        IncrementVersion();
        return Result.Success();
    }

    public Result RecordPayment(HonorairesPayment payment)
    {
        if (!Status.CanBePaymentRecorded())
            return Result.Failure(Error.Validation("Status", "Aucun encaissement n'est possible sur ce document"));
        if (IsCreditNote)
            return Result.Failure(Error.Validation("Type", "Les avoirs ne reçoivent pas d'encaissement"));

        var nextPaid = AmountPaid.Amount + payment.AppliedAmount;
        if (nextPaid > Math.Abs(TotalAmount.Amount) + 0.0005m)
            return Result.Failure(Error.Validation("Amount", "Le montant dépasse le reste dû"));

        _payments.Add(payment);
        ReconcilePaymentStatus(nextPaid, payment.PaymentDate);
        IncrementVersion();
        return Result.Success();
    }

    public void ReconcilePaymentStatus(decimal totalPaidApplied, DateTime? lastPaymentDate)
    {
        if (totalPaidApplied <= 0) return;
        var dueMagnitude = Math.Abs(TotalAmount.Amount);
        if (totalPaidApplied > dueMagnitude + 0.0005m) return;

        AmountPaid = Money.Create(totalPaidApplied, Currency);
        PaidAt = lastPaymentDate;

        Status = totalPaidApplied >= dueMagnitude
            ? HonorairesInvoiceStatus.Paid
            : HonorairesInvoiceStatus.PartiallyPaid;
    }

    private void RecalculateTotals()
    {
        var currency = Currency;
        var sumHt = _lines.Sum(l => l.SubTotal.Amount);
        var sumVat = _lines.Sum(l => l.VatAmount.Amount);
        var sign = IsCreditNote ? -1m : 1m;
        SubTotal = Money.FromSignedAmount(sign * sumHt, currency);
        TotalVat = Money.FromSignedAmount(sign * sumVat, currency);
        TotalAmount = Money.FromSignedAmount(sign * (sumHt + sumVat), currency);
    }

    private void RenumberLines()
    {
        for (var i = 0; i < _lines.Count; i++)
            _lines[i].SetLineNumber(i + 1);
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
