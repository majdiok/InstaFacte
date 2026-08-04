using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities.Honoraires;

/// <summary>Firm honoraires quote (devis) — parallel to commercial Quote.</summary>
public sealed class HonorairesQuote : AggregateRoot
{
    public string? Number { get; private set; }
    public DateTime IssueDate { get; private set; }
    public DateTime? ValidUntil { get; private set; }
    public HonorairesQuoteStatus Status { get; private set; }

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
    public string Currency { get; private set; } = Money.DefaultCurrency;

    public Guid? ConvertedInvoiceId { get; private set; }
    public DateTime? ConvertedAt { get; private set; }

    public Money SubTotal { get; private set; } = null!;
    public Money TotalVat { get; private set; } = null!;
    public Money TotalAmount { get; private set; } = null!;

    private readonly List<HonorairesQuoteLine> _lines = new();
    public IReadOnlyCollection<HonorairesQuoteLine> Lines => _lines.AsReadOnly();

    private HonorairesQuote() { }

    public static Result<HonorairesQuote> CreateDraft(
        Guid firmClientAssignmentId,
        string clientName,
        DateTime issueDate,
        DateTime? validUntil = null,
        string currency = Money.DefaultCurrency)
    {
        if (firmClientAssignmentId == Guid.Empty)
            return Result.Failure<HonorairesQuote>(Error.Validation("FirmClientAssignmentId", "Le dossier client est obligatoire"));
        if (string.IsNullOrWhiteSpace(clientName))
            return Result.Failure<HonorairesQuote>(Error.Validation("ClientName", "Le nom du client est obligatoire"));

        var currencyCode = string.IsNullOrWhiteSpace(currency) ? Money.DefaultCurrency : currency.Trim().ToUpperInvariant();
        return Result.Success(new HonorairesQuote
        {
            FirmClientAssignmentId = firmClientAssignmentId,
            ClientName = clientName.Trim(),
            IssueDate = issueDate,
            ValidUntil = validUntil,
            Status = HonorairesQuoteStatus.Draft,
            Currency = currencyCode,
            SubTotal = Money.Zero(currencyCode),
            TotalVat = Money.Zero(currencyCode),
            TotalAmount = Money.Zero(currencyCode)
        });
    }

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

    public void SetDates(DateTime issueDate, DateTime? validUntil)
    {
        if (!Status.CanBeEdited()) return;
        IssueDate = issueDate;
        ValidUntil = validUntil;
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

    public Result AssignNumber(string number)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce devis ne peut plus être modifié"));
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
            return Result.Failure(Error.Validation("Status", "Ce devis ne peut plus être modifié"));

        var lineResult = HonorairesQuoteLine.Create(
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
            return Result.Failure(Error.Validation("Status", "Ce devis ne peut plus être modifié"));

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            return Result.Failure(Error.NotFound("HonorairesQuoteLine", lineId));

        var result = line.Update(designation, description, quantity, unitPrice, vatRate, discountPercent, activityCode);
        if (result.IsFailure) return result;
        RecalculateTotals();
        return Result.Success();
    }

    public Result RemoveLine(Guid lineId)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce devis ne peut plus être modifié"));

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            return Result.Failure(Error.NotFound("HonorairesQuoteLine", lineId));

        _lines.Remove(line);
        for (var i = 0; i < _lines.Count; i++)
            _lines[i].SetLineNumber(i + 1);
        RecalculateTotals();
        return Result.Success();
    }

    public Result ReplaceLines(IEnumerable<(string? ActivityCode, string Designation, string? Description, decimal Quantity, Money UnitPrice, VatRate VatRate, decimal? DiscountPercent)> lines)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce devis ne peut plus être modifié"));

        _lines.Clear();
        foreach (var l in lines)
        {
            var add = AddLine(l.Designation, l.Description, l.Quantity, l.UnitPrice, l.VatRate, l.DiscountPercent, l.ActivityCode);
            if (add.IsFailure) return add;
        }
        return Result.Success();
    }

    public Result MarkSent()
    {
        if (!Status.CanBeSent())
            return Result.Failure(Error.Validation("Status", "Seuls les brouillons peuvent être envoyés"));
        if (_lines.Count == 0)
            return Result.Failure(Error.Validation("Lines", "Le devis doit contenir au moins une ligne"));
        if (string.IsNullOrWhiteSpace(Number))
            return Result.Failure(Error.Validation("Number", "Le numéro doit être réservé avant envoi"));

        Status = HonorairesQuoteStatus.Sent;
        return Result.Success();
    }

    public Result Accept()
    {
        if (!Status.CanBeAccepted())
            return Result.Failure(Error.Validation("Status", "Seuls les devis envoyés peuvent être acceptés"));
        Status = HonorairesQuoteStatus.Accepted;
        return Result.Success();
    }

    public Result Reject(string? reason = null)
    {
        if (!Status.CanBeRejected())
            return Result.Failure(Error.Validation("Status", "Seuls les devis envoyés peuvent être refusés"));
        Status = HonorairesQuoteStatus.Rejected;
        Notes = string.IsNullOrWhiteSpace(reason) ? Notes : $"{Notes}\nRefus: {reason}".Trim();
        return Result.Success();
    }

    public Result Cancel(string? reason = null)
    {
        if (!Status.CanBeCancelled())
            return Result.Failure(Error.Validation("Status", "Ce devis ne peut pas être annulé"));
        Status = HonorairesQuoteStatus.Cancelled;
        if (!string.IsNullOrWhiteSpace(reason))
            Notes = TrimOrNull(reason);
        return Result.Success();
    }

    public Result MarkAsConverted(Guid invoiceId)
    {
        if (!Status.CanBeConverted())
            return Result.Failure(Error.Validation("Status", "Seuls les devis acceptés peuvent être convertis"));
        if (ConvertedInvoiceId.HasValue)
            return Result.Failure(Error.Validation("ConvertedInvoiceId", "Ce devis a déjà été converti"));
        if (invoiceId == Guid.Empty)
            return Result.Failure(Error.Validation("InvoiceId", "Facture invalide"));

        ConvertedInvoiceId = invoiceId;
        ConvertedAt = DateTime.UtcNow;
        Status = HonorairesQuoteStatus.Converted;
        return Result.Success();
    }

    private void RecalculateTotals()
    {
        var currency = Currency;
        var sumHt = _lines.Sum(l => l.SubTotal.Amount);
        var sumVat = _lines.Sum(l => l.VatAmount.Amount);
        SubTotal = Money.Create(sumHt, currency);
        TotalVat = Money.Create(sumVat, currency);
        TotalAmount = Money.Create(sumHt + sumVat, currency);
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
