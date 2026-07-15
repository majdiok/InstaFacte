using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Billing;

/// <summary>Type de facturation côté plateforme.</summary>
public enum PlatformInvoiceBillingType
{
    /// <summary>Abonnement (renouvellement plan SaaS).</summary>
    Subscription = 0,
    /// <summary>Frais de mise en place ponctuels.</summary>
    SetupFee = 1,
    /// <summary>Facture manuelle (consulting, hors plan).</summary>
    Manual = 2,
    /// <summary>Avoir / remboursement.</summary>
    Refund = 3
}

/// <summary>Statut courant d'une facture plateforme.</summary>
public enum PlatformInvoiceStatus
{
    /// <summary>Brouillon — modifiable, non comptabilisé, sans numéro définitif.</summary>
    Draft = 0,
    /// <summary>Émise — numéro attribué, comptabilisée, immuable.</summary>
    Issued = 1,
    /// <summary>Réglée intégralement (somme reçus = TTC).</summary>
    Paid = 2,
    /// <summary>Réglée partiellement (somme reçus &gt; 0 et &lt; TTC).</summary>
    PartiallyPaid = 3,
    /// <summary>Échue impayée (DueDate dépassée).</summary>
    Overdue = 4,
    /// <summary>Annulée (avec avoir).</summary>
    Cancelled = 5,
    /// <summary>Remboursée (avoir entièrement appliqué).</summary>
    Refunded = 6
}

/// <summary>
/// Lot C4 — Facture plateforme émise par FactuTrust à un tenant.
///
/// Conforme à la fiscalité tunisienne (DGI) :
/// <list type="bullet">
///   <item>Numérotation séquentielle par année (<c>FT-2026-000123</c>) garantie sans saut.</item>
///   <item>Mentions obligatoires : NIF émetteur, raison sociale, adresse, code TVA.</item>
///   <item>TVA 19% par défaut + timbre fiscal 1 TND.</item>
///   <item>QR code DGI encodant <c>NIF|Number|Date|TTC</c>.</item>
///   <item>État émis = immuable (sauf annulation par avoir).</item>
/// </list>
///
/// Le PDF est rendu une seule fois à l'émission (idempotent) et stocké via
/// <c>PdfStorageKey</c>. Les lignes sont figées au moment de l'émission.
/// </summary>
public sealed class PlatformInvoice : Entity
{
    public Guid TenantId { get; private set; }

    /// <summary>Numéro DGI au format <c>FT-YYYY-NNNNNN</c>. Null tant que Draft.</summary>
    public string? Number { get; private set; }

    /// <summary>Année de séquence (figée à l'émission).</summary>
    public int? SequenceYear { get; private set; }

    public DateTime InvoiceDate { get; private set; }
    public DateTime? DueDate { get; private set; }

    /// <summary>Période d'abonnement couverte (optionnel pour Manual/SetupFee).</summary>
    public DateTime? PeriodFrom { get; private set; }
    public DateTime? PeriodTo { get; private set; }

    public PlatformInvoiceBillingType BillingType { get; private set; }
    public PlatformInvoiceStatus Status { get; private set; }

    /// <summary>Sous-total HT (avant remise/coupon/crédits/timbre).</summary>
    public decimal SubtotalHT { get; private set; }
    public decimal VatAmount { get; private set; }

    /// <summary>Remise commerciale appliquée en TND.</summary>
    public decimal DiscountAmount { get; private set; }

    /// <summary>Crédits tenant consommés en TND.</summary>
    public decimal CreditsApplied { get; private set; }

    /// <summary>Timbre fiscal appliqué (1 TND par facture en TN par défaut).</summary>
    public decimal StampDuty { get; private set; }

    /// <summary>Total TTC final (= subtotal HT + TVA + timbre - remises - crédits).</summary>
    public decimal TotalTTC { get; private set; }

    public Guid? CouponRedemptionId { get; private set; }

    /// <summary>Clé de stockage du PDF rendu (chemin relatif ou storage key blob).</summary>
    public string? PdfStorageKey { get; private set; }

    /// <summary>Mentions légales figées à l'émission.</summary>
    public string? LegalMentions { get; private set; }

    /// <summary>Données fiscales figées à l'émission (snapshot des PlatformFiscalSettings).</summary>
    public string? FiscalSnapshotJson { get; private set; }

    public DateTime? IssuedAt { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancelledReason { get; private set; }
    public Guid? RelatedInvoiceId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    private readonly List<PlatformInvoiceLine> _lines = new();
    public IReadOnlyCollection<PlatformInvoiceLine> Lines => _lines.AsReadOnly();

    private readonly List<PlatformReceipt> _receipts = new();
    public IReadOnlyCollection<PlatformReceipt> Receipts => _receipts.AsReadOnly();

    private PlatformInvoice() { }

    public static PlatformInvoice CreateDraft(
        Guid tenantId,
        DateTime invoiceDate,
        DateTime? dueDate,
        PlatformInvoiceBillingType billingType,
        Guid createdByUserId,
        DateTime? periodFrom = null,
        DateTime? periodTo = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId requis", nameof(tenantId));
        return new PlatformInvoice
        {
            TenantId = tenantId,
            InvoiceDate = DateTime.SpecifyKind(invoiceDate.Date, DateTimeKind.Utc),
            DueDate = dueDate is null ? null : DateTime.SpecifyKind(dueDate.Value.Date, DateTimeKind.Utc),
            PeriodFrom = periodFrom is null ? null : DateTime.SpecifyKind(periodFrom.Value.Date, DateTimeKind.Utc),
            PeriodTo = periodTo is null ? null : DateTime.SpecifyKind(periodTo.Value.Date, DateTimeKind.Utc),
            BillingType = billingType,
            Status = PlatformInvoiceStatus.Draft,
            CreatedByUserId = createdByUserId,
            StampDuty = 0m,
            CreditsApplied = 0m,
            DiscountAmount = 0m,
            SubtotalHT = 0m,
            VatAmount = 0m,
            TotalTTC = 0m
        };
    }

    public void AddLine(string description, decimal quantity, decimal unitPriceHT, decimal vatRate,
        DateTime? relatedPeriodFrom = null, DateTime? relatedPeriodTo = null)
    {
        EnsureMutable();
        var line = PlatformInvoiceLine.Create(Id, description, quantity, unitPriceHT, vatRate, relatedPeriodFrom, relatedPeriodTo);
        _lines.Add(line);
    }

    public void ClearLines()
    {
        EnsureMutable();
        _lines.Clear();
    }

    /// <summary>Recalcule subtotalHT / VAT / TTC à partir des lignes + remise + crédits + timbre.</summary>
    public void Recalculate(decimal stampDuty)
    {
        var subtotal = 0m;
        var vat = 0m;
        foreach (var l in _lines)
        {
            subtotal += l.LineTotalHT;
            vat += l.LineTotalHT * (l.VatRate / 100m);
        }
        SubtotalHT = Math.Round(subtotal, 3);
        VatAmount = Math.Round(vat, 3);
        StampDuty = Math.Round(stampDuty, 3);

        var afterDiscount = SubtotalHT - DiscountAmount;
        if (afterDiscount < 0m) afterDiscount = 0m;
        var ttc = afterDiscount + VatAmount + StampDuty - CreditsApplied;
        if (ttc < 0m) ttc = 0m;
        TotalTTC = Math.Round(ttc, 3);
    }

    public void ApplyDiscount(decimal amount)
    {
        EnsureMutable();
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        DiscountAmount = Math.Round(amount, 3);
    }

    public void ApplyCredits(decimal amount)
    {
        EnsureMutable();
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        CreditsApplied = Math.Round(amount, 3);
    }

    public void AssociateCouponRedemption(Guid couponRedemptionId)
    {
        EnsureMutable();
        CouponRedemptionId = couponRedemptionId;
    }

    /// <summary>Émet la facture : attribue le numéro, fige la date d'émission, snapshot fiscal.</summary>
    public void Issue(string number, int sequenceYear, string? legalMentions, string? fiscalSnapshotJson)
    {
        if (Status != PlatformInvoiceStatus.Draft)
            throw new InvalidOperationException($"Une facture {Status} ne peut pas être émise.");
        if (_lines.Count == 0)
            throw new InvalidOperationException("Une facture doit comporter au moins une ligne.");
        if (string.IsNullOrWhiteSpace(number))
            throw new ArgumentException("Numéro requis", nameof(number));

        Number = number;
        SequenceYear = sequenceYear;
        Status = PlatformInvoiceStatus.Issued;
        IssuedAt = DateTime.UtcNow;
        LegalMentions = legalMentions;
        FiscalSnapshotJson = fiscalSnapshotJson;
    }

    public void AttachPdf(string pdfStorageKey) => PdfStorageKey = pdfStorageKey;

    /// <summary>Marque la facture comme totalement payée (somme des reçus = TTC).</summary>
    public void MarkPaid()
    {
        if (Status is not (PlatformInvoiceStatus.Issued or PlatformInvoiceStatus.PartiallyPaid or PlatformInvoiceStatus.Overdue))
            throw new InvalidOperationException($"Le statut {Status} n'autorise pas le paiement intégral.");
        Status = PlatformInvoiceStatus.Paid;
        PaidAt = DateTime.UtcNow;
    }

    /// <summary>Marque la facture comme partiellement réglée (un reçu insuffisant).</summary>
    public void MarkPartiallyPaid()
    {
        if (Status is not (PlatformInvoiceStatus.Issued or PlatformInvoiceStatus.Overdue or PlatformInvoiceStatus.PartiallyPaid))
            throw new InvalidOperationException($"Le statut {Status} n'autorise pas le paiement partiel.");
        Status = PlatformInvoiceStatus.PartiallyPaid;
    }

    public void MarkOverdue()
    {
        if (Status is not (PlatformInvoiceStatus.Issued or PlatformInvoiceStatus.PartiallyPaid))
            return;
        Status = PlatformInvoiceStatus.Overdue;
    }

    /// <summary>Annule la facture (génère un avoir de référence).</summary>
    public void Cancel(string reason, Guid creditNoteInvoiceId)
    {
        if (Status is PlatformInvoiceStatus.Draft)
            throw new InvalidOperationException("Une facture brouillon ne s'annule pas, elle se supprime.");
        if (Status is PlatformInvoiceStatus.Cancelled or PlatformInvoiceStatus.Refunded)
            throw new InvalidOperationException("Facture déjà annulée.");
        Status = PlatformInvoiceStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        CancelledReason = (reason ?? string.Empty).Trim();
        RelatedInvoiceId = creditNoteInvoiceId;
    }

    public decimal TotalReceived()
    {
        var sum = 0m;
        foreach (var r in _receipts)
        {
            if (r.Status == PlatformReceiptStatus.Confirmed) sum += r.AmountTND;
        }
        return Math.Round(sum, 3);
    }

    public void AddReceipt(PlatformReceipt receipt) => _receipts.Add(receipt);

    private void EnsureMutable()
    {
        if (Status != PlatformInvoiceStatus.Draft)
            throw new InvalidOperationException($"Une facture {Status} est immuable.");
    }
}

/// <summary>Lot C4 — Ligne d'une facture plateforme.</summary>
public sealed class PlatformInvoiceLine : Entity
{
    public Guid InvoiceId { get; private set; }
    public string Description { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public decimal UnitPriceHT { get; private set; }
    public decimal VatRate { get; private set; }
    public decimal LineTotalHT { get; private set; }
    public decimal LineTotalTTC { get; private set; }
    public DateTime? RelatedPeriodFrom { get; private set; }
    public DateTime? RelatedPeriodTo { get; private set; }
    public int SortOrder { get; private set; }

    private PlatformInvoiceLine() { }

    internal static PlatformInvoiceLine Create(
        Guid invoiceId,
        string description,
        decimal quantity,
        decimal unitPriceHT,
        decimal vatRate,
        DateTime? periodFrom,
        DateTime? periodTo)
    {
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description requise", nameof(description));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (unitPriceHT < 0) throw new ArgumentOutOfRangeException(nameof(unitPriceHT));
        if (vatRate < 0 || vatRate > 100) throw new ArgumentOutOfRangeException(nameof(vatRate));

        var totalHT = Math.Round(quantity * unitPriceHT, 3);
        var totalTTC = Math.Round(totalHT * (1 + (vatRate / 100m)), 3);

        return new PlatformInvoiceLine
        {
            InvoiceId = invoiceId,
            Description = description.Trim(),
            Quantity = quantity,
            UnitPriceHT = unitPriceHT,
            VatRate = vatRate,
            LineTotalHT = totalHT,
            LineTotalTTC = totalTTC,
            RelatedPeriodFrom = periodFrom is null ? null : DateTime.SpecifyKind(periodFrom.Value.Date, DateTimeKind.Utc),
            RelatedPeriodTo = periodTo is null ? null : DateTime.SpecifyKind(periodTo.Value.Date, DateTimeKind.Utc)
        };
    }

    public void SetSortOrder(int order) => SortOrder = order;
}
