using FactuTrust.Domain.Common;
using FactuTrust.Domain.Services;
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
    /// Source delivery note ID when the invoice was generated from a bon de livraison.
    /// Lien permanent BL ↔ facture, symétrique de <see cref="SourceQuoteId"/>.
    /// C'est la seule source de vérité pour savoir si le stock a déjà été sorti à la
    /// livraison : ne jamais redéduire cette information d'un champ texte libre
    /// (<see cref="Reference"/> est saisissable par l'utilisateur).
    /// </summary>
    public Guid? SourceDeliveryNoteId { get; private set; }

    /// <summary>
    /// Commande client à l'origine de cette facture, quand elle a été émise directement
    /// depuis la commande (facturation d'avance, vente sur commande sans bon de livraison).
    /// Complète <see cref="SourceQuoteId"/> et <see cref="SourceDeliveryNoteId"/> : les trois
    /// documents amont possibles ont désormais chacun leur lien typé.
    ///
    /// ⚠️ Ne dispense PAS de déduire le stock : contrairement à une facture issue d'un bon de
    /// livraison, une facture émise directement depuis la commande n'a donné lieu à aucune
    /// sortie. Seul <see cref="SourceDeliveryNoteId"/> vaut garde-fou anti-double-déduction.
    /// </summary>
    public Guid? SourceSalesOrderId { get; private set; }

    /// <summary>
    /// Facture rectifiée par cet avoir. Obligatoire à la création d'un nouvel avoir : une
    /// facture rectificative doit référencer la facture d'origine.
    /// Reste nullable pour ne pas invalider les avoirs historiques, émis avant que le lien
    /// ne soit persisté (il ne vivait alors que dans le JSON du brouillon).
    /// </summary>
    public Guid? LinkedInvoiceId { get; private set; }

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

    /// <summary>
    /// Creates an invoice from a delivered bon de livraison. Sets <see cref="SourceDeliveryNoteId"/>,
    /// qui signale aux abonnés de <c>InvoiceValidatedEvent</c> que le stock a déjà été sorti à la
    /// livraison et ne doit pas l'être une seconde fois à la validation de la facture.
    /// </summary>
    public static Result<Invoice> CreateFromDeliveryNote(
        InvoiceNumber number,
        Client client,
        DateTime issueDate,
        Guid sourceDeliveryNoteId,
        DateTime? dueDate = null,
        string? reference = null,
        string? notes = null,
        string? paymentTerms = null,
        Guid? warehouseId = null)
    {
        if (sourceDeliveryNoteId == Guid.Empty)
            return Result.Failure<Invoice>(Error.Validation("SourceDeliveryNoteId", "Le bon de livraison source est obligatoire"));

        var result = Create(number, client, issueDate, dueDate, reference, notes, paymentTerms, warehouseId);
        if (result.IsFailure)
            return result;

        result.Value.SourceDeliveryNoteId = sourceDeliveryNoteId;
        return result;
    }

    /// <summary>
    /// Crée une facture émise DIRECTEMENT depuis une commande client, sans bon de livraison
    /// intermédiaire — facturation d'avance ou vente sur commande.
    ///
    /// ⚠️ <see cref="SourceDeliveryNoteId"/> reste nul : le stock n'a donc pas encore été
    /// sorti, et la validation de cette facture doit le déduire normalement. C'est la
    /// différence essentielle avec <see cref="CreateFromDeliveryNote"/>.
    /// </summary>
    public static Result<Invoice> CreateFromSalesOrder(
        InvoiceNumber number,
        Client client,
        DateTime issueDate,
        Guid sourceSalesOrderId,
        DateTime? dueDate = null,
        string? reference = null,
        string? notes = null,
        string? paymentTerms = null,
        Guid? warehouseId = null)
    {
        if (sourceSalesOrderId == Guid.Empty)
            return Result.Failure<Invoice>(Error.Validation("SourceSalesOrderId", "La commande source est obligatoire"));

        var result = Create(number, client, issueDate, dueDate, reference, notes, paymentTerms, warehouseId);
        if (result.IsFailure)
            return result;

        result.Value.SourceSalesOrderId = sourceSalesOrderId;
        return result;
    }

    /// <summary>
    /// Déclare que cette facture regroupe PLUSIEURS bons de livraison (facturation
    /// périodique). Le stock a déjà été sorti à chaque livraison : sa validation ne doit donc
    /// rien redéduire.
    ///
    /// <see cref="SourceDeliveryNoteId"/> ne peut porter qu'un seul bon ; on y place le
    /// premier, ce qui suffit au garde-fou anti-double-déduction — il ne demande que
    /// « le stock est-il déjà sorti ? ». La liste complète reste lisible dans l'autre sens,
    /// chaque bon portant <c>DeliveryNote.InvoiceId</c>.
    ///
    /// Passer par cette méthode plutôt que d'affecter directement la clé rend l'intention
    /// explicite à l'appel, et évite qu'on lise « facture issue d'un bon » là où il y en a N.
    /// </summary>
    public Result MarkGeneratedFromDeliveryNotes(IReadOnlyList<Guid> deliveryNoteIds)
    {
        if (deliveryNoteIds is null || deliveryNoteIds.Count == 0)
            return Result.Failure(Error.Validation("DeliveryNotes", "Au moins un bon de livraison est requis"));

        if (deliveryNoteIds.Any(id => id == Guid.Empty))
            return Result.Failure(Error.Validation("DeliveryNotes", "Identifiant de bon de livraison invalide"));

        SourceDeliveryNoteId = deliveryNoteIds[0];
        return Result.Success();
    }

    /// <summary>
    /// Rattache la facture à une commande client sans en changer l'origine de stock.
    /// Utilisé quand la facture provient d'un bon de livraison lui-même issu d'une commande :
    /// la traçabilité remonte alors jusqu'à l'engagement, mais le garde-fou de stock reste
    /// porté par <see cref="SourceDeliveryNoteId"/>.
    /// </summary>
    public void AttachSalesOrderOrigin(Guid salesOrderId)
    {
        if (salesOrderId == Guid.Empty)
            throw new ArgumentException("SalesOrderId invalide", nameof(salesOrderId));

        SourceSalesOrderId ??= salesOrderId;
    }

    /// <summary>
    /// Creates a credit note (facture d'avoir) rectifying <paramref name="linkedInvoiceId"/>.
    /// Le lien vers la facture d'origine est obligatoire : c'est une exigence de traçabilité
    /// fiscale, et le PDF de l'avoir l'imprime.
    /// </summary>
    public static Result<Invoice> CreateCreditNote(
        InvoiceNumber number,
        Client client,
        DateTime issueDate,
        Guid linkedInvoiceId,
        DateTime? dueDate = null,
        string? reference = null,
        string? notes = null,
        string? paymentTerms = null,
        Guid? warehouseId = null)
    {
        if (linkedInvoiceId == Guid.Empty)
            return Result.Failure<Invoice>(Error.Validation("LinkedInvoiceId", "La facture d'origine est obligatoire pour un avoir"));

        var result = Create(
            number, client, issueDate, dueDate, reference, notes, paymentTerms, warehouseId,
            type: InvoiceType.CreditNote);

        if (result.IsFailure)
            return result;

        result.Value.LinkedInvoiceId = linkedInvoiceId;
        return result;
    }

    public Result AddLine(
        Product product,
        decimal quantity,
        Money? customUnitPrice = null,
        decimal? discountPercent = null,
        decimal fodecRatePercent = 1.0m,
        Guid? appliedPromotionId = null,
        string? appliedPromotionName = null)
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
            fodecRatePercent,
            appliedPromotionId,
            appliedPromotionName);

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

    // Note : le dépassement d'échéance n'est PAS un changement d'état persisté. Il est calculé
    // à la volée côté requête (GetInvoicesQuery.IsOverdue), ce qui évite d'avoir à balayer
    // périodiquement les factures pour basculer un statut. L'ancienne méthode CheckOverdue(),
    // jamais appelée et dont l'événement était explicitement ignoré par le DbContext, a été
    // retirée. InvoiceStatus.Overdue reste dans l'énumération : il est lu par
    // CanBePaymentRecorded() et par les libellés d'affichage, côté serveur comme côté client.


    /// <summary>
    /// Remise de pied de document, en pourcentage de la base HT. Exclusive du montant fixe.
    /// </summary>
    public decimal? GlobalDiscountPercent { get; private set; }

    /// <summary>
    /// Remise de pied effectivement appliquée, en montant. Renseignée dans les deux cas : saisie
    /// directe, ou dérivée du pourcentage. C'est ce montant qui figure au pied du document.
    /// </summary>
    public Money GlobalDiscountAmount { get; private set; } = Money.Zero();

    /// <summary>Total HT AVANT remise de pied — le « sous-total » affiché au-dessus de la remise.</summary>
    public Money SubTotalBeforeGlobalDiscount => SubTotal.Add(GlobalDiscountAmount);

    /// <summary>
    /// Pose (ou retire) la remise de pied. Elle est répartie sur les lignes au prorata de leur
    /// base HT, de sorte que le FODEC et la TVA portent sur ce qui est réellement facturé.
    ///
    /// Un seul mode à la fois : passer un pourcentage écrase un montant, et inversement. Les
    /// deux à la fois est refusé plutôt que départagé en silence.
    /// </summary>
    public Result SetGlobalDiscount(decimal? percent, Money? amount)
    {
        if (percent.HasValue && amount is { Amount: > 0 })
        {
            return Result.Failure(Error.Validation("GlobalDiscount",
                "Choisissez un pourcentage OU un montant de remise, pas les deux"));
        }

        if (percent is < 0 or > 100)
            return Result.Failure(Error.Validation("GlobalDiscountPercent", "La remise doit être comprise entre 0 % et 100 %"));

        if (amount is { Amount: < 0 })
            return Result.Failure(Error.Validation("GlobalDiscountAmount", "La remise ne peut pas être négative"));

        GlobalDiscountPercent = percent is > 0 ? percent : null;

        // Le montant demandé est stocké dans GlobalDiscountAmount, qui EST persisté : au
        // rechargement du document, la remise se rejoue donc à l'identique. Un champ privé non
        // persisté aurait été effacé au premier recalcul après relecture.
        GlobalDiscountAmount = amount is { Amount: > 0 } ? amount : Money.Zero();

        RecalculateTotals();
        return Result.Success();
    }

    /// <summary>
    /// Répartit la remise de pied sur les lignes. Rejouée à chaque recalcul : ajouter une ligne
    /// change la base, donc les parts. Sans remise, chaque ligne reçoit zéro et se calcule
    /// exactement comme avant la tranche 5B.
    /// </summary>
    private void ApplyGlobalDiscountToLines()
    {
        if (_lines.Count == 0)
        {
            GlobalDiscountAmount = Money.Zero();
            return;
        }

        var currency = _lines[0].SubTotal.Currency;
        var bases = _lines.Select(l => l.SubTotalBeforeGlobalDiscount.Amount).ToList();
        var totalBase = bases.Sum();

        var requested = GlobalDiscountPercent.HasValue
            ? GlobalDiscountAllocator.FromPercent(totalBase, GlobalDiscountPercent.Value)
            : GlobalDiscountAmount.Amount;

        var parts = GlobalDiscountAllocator.Allocate(bases, requested);

        for (var i = 0; i < _lines.Count; i++)
            _lines[i].SetAllocatedGlobalDiscount(Money.Create(parts[i], currency));

        GlobalDiscountAmount = Money.Create(parts.Sum(), currency);
    }

    private void RecalculateTotals()
    {
        ApplyGlobalDiscountToLines();

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
