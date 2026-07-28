using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Commande client (bon de commande client) — l'engagement contractuel qui manquait au cycle
/// de vente.
///
/// Le cycle était Devis → BL → Facture : aucun document ne portait l'engagement, si bien que le
/// carnet de commandes, les reliquats, les livraisons partielles successives, les acomptes et la
/// réservation de stock étaient tous hors système. Le bon de livraison porte bien un
/// « reste à livrer », mais ce reliquat meurt avec lui : rien ne permettait d'émettre un BL
/// complémentaire rattaché au même engagement.
///
/// La commande est le seul document à suivre DEUX avancements distincts — livré et facturé —
/// qui ne progressent pas au même rythme. Son statut porte le moins avancé des deux, de sorte
/// qu'une commande « soldée » ne puisse jamais masquer un reliquat.
///
/// Totaux alignés sur <see cref="Invoice"/> et <see cref="Quote"/> : HT + FODEC + TVA + timbre.
/// </summary>
public sealed class SalesOrder : AggregateRoot
{
    public SalesOrderNumber Number { get; private set; } = null!;
    public DateTime OrderDate { get; private set; }
    public DateTime? ExpectedDeliveryDate { get; private set; }
    public SalesOrderStatus Status { get; private set; }

    public Guid ClientId { get; private set; }
    public Client Client { get; private set; } = null!;

    public string? Reference { get; private set; }
    public string? Notes { get; private set; }
    public string? PaymentTerms { get; private set; }

    /// <summary>Entrepôt de préparation. Détermine où le stock est réservé.</summary>
    public Guid? WarehouseId { get; private set; }
    public Warehouse? Warehouse { get; private set; }

    /// <summary>Devis à l'origine de la commande. Complète la chaîne Devis → Commande → BL → Facture.</summary>
    public Guid? SourceQuoteId { get; private set; }

    private readonly List<SalesOrderLine> _lines = new();
    public IReadOnlyCollection<SalesOrderLine> Lines => _lines.AsReadOnly();

    public Money SubTotal { get; private set; } = null!;
    public Money FodecAmount { get; private set; } = null!;
    public Money TotalVat { get; private set; } = null!;
    public Money FiscalStampAmount { get; private set; } = null!;
    public Money TotalAmount { get; private set; } = null!;

    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public string? ClosureReason { get; private set; }

    /// <summary>
    /// Vrai lorsque le stock a été réservé à la confirmation. Empêche une double réservation
    /// comme une double libération.
    /// </summary>
    public bool IsStockReserved { get; private set; }

    private SalesOrder() { }

    public static Result<SalesOrder> Create(
        SalesOrderNumber number,
        Client client,
        DateTime orderDate,
        DateTime? expectedDeliveryDate = null,
        string? reference = null,
        string? notes = null,
        string? paymentTerms = null,
        Guid? warehouseId = null,
        Guid? sourceQuoteId = null)
    {
        if (client is null)
            return Result.Failure<SalesOrder>(Error.Validation("Client", "Le client est obligatoire"));

        if (expectedDeliveryDate.HasValue && expectedDeliveryDate.Value.Date < orderDate.Date)
            return Result.Failure<SalesOrder>(Error.Validation("ExpectedDeliveryDate",
                "La date de livraison prévue ne peut pas être antérieure à la date de commande"));

        var order = new SalesOrder
        {
            Number = number,
            ClientId = client.Id,
            Client = client,
            OrderDate = orderDate.Date,
            ExpectedDeliveryDate = expectedDeliveryDate?.Date,
            Status = SalesOrderStatus.Draft,
            Reference = reference?.Trim(),
            Notes = notes?.Trim(),
            PaymentTerms = paymentTerms?.Trim(),
            WarehouseId = warehouseId,
            SourceQuoteId = sourceQuoteId,
            SubTotal = Money.Zero(),
            FodecAmount = Money.Zero(),
            TotalVat = Money.Zero(),
            FiscalStampAmount = Money.Zero(),
            TotalAmount = Money.Zero(),
            IsStockReserved = false
        };

        return Result.Success(order);
    }

    // ─────────────────────────────── Édition des lignes ───────────────────────────────

    public Result AddLine(
        Product product,
        decimal quantity,
        Money? customUnitPrice = null,
        decimal? discountPercent = null,
        decimal fodecRatePercent = SalesOrderLine.DefaultFodecRatePercent,
        string? notes = null)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Cette commande ne peut plus être modifiée"));

        var unitPrice = customUnitPrice ?? product.UnitPrice;
        var lineResult = SalesOrderLine.Create(
            this, _lines.Count + 1, product, quantity, unitPrice, discountPercent, fodecRatePercent, notes);

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
            return Result.Failure(Error.NotFound("SalesOrderLine", lineId));

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
        string? notes = null)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Cette commande ne peut plus être modifiée"));

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            return Result.Failure(Error.NotFound("SalesOrderLine", lineId));

        var result = line.Update(quantity, customUnitPrice, discountPercent, notes);
        if (result.IsFailure)
            return result;

        RecalculateTotals();
        return Result.Success();
    }

    /// <summary>Fixe le timbre fiscal annoncé, comme sur le devis et la facture.</summary>
    public Result SetFiscalStampAmount(Money stamp)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Cette commande ne peut plus être modifiée"));

        if (stamp.Currency != SubTotal.Currency)
            return Result.Failure(Error.Validation("FiscalStampAmount", "La devise du timbre ne correspond pas à la commande"));

        FiscalStampAmount = stamp;
        RecalculateTotals();
        return Result.Success();
    }

    public void UpdateHeader(DateTime? expectedDeliveryDate, string? reference, string? notes, string? paymentTerms)
    {
        if (!Status.CanBeEdited())
            return;

        ExpectedDeliveryDate = expectedDeliveryDate?.Date;
        Reference = reference?.Trim();
        Notes = notes?.Trim();
        PaymentTerms = paymentTerms?.Trim();
    }

    // ─────────────────────────────── Machine à états ───────────────────────────────

    /// <summary>
    /// Confirme la commande : elle devient un engagement ferme et entre au carnet de commandes.
    /// La réservation de stock est déclenchée par la couche applicative, qui appelle ensuite
    /// <see cref="MarkStockReserved"/> — le domaine ne connaît pas les entrepôts.
    /// </summary>
    public Result Confirm()
    {
        if (!Status.CanBeConfirmed())
            return Result.Failure(Error.Validation("Status", "Cette commande ne peut pas être confirmée dans son état actuel"));

        if (_lines.Count == 0)
            return Result.Failure(Error.Validation("Lines", "La commande doit contenir au moins une ligne"));

        Status = SalesOrderStatus.Confirmed;
        ConfirmedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Impute une livraison sur les lignes désignées, puis réévalue l'avancement.</summary>
    public Result RecordDeliveries(IEnumerable<(Guid LineId, decimal Quantity)> deliveries)
    {
        if (!Status.CanBeDelivered())
            return Result.Failure(Error.Validation("Status", "Cette commande ne peut pas recevoir de livraison dans son état actuel"));

        var items = deliveries?.ToList() ?? new List<(Guid, decimal)>();
        if (items.Count == 0)
            return Result.Failure(Error.Validation("Deliveries", "Aucune ligne à livrer"));

        foreach (var (lineId, quantity) in items)
        {
            var line = _lines.FirstOrDefault(l => l.Id == lineId);
            if (line is null)
                return Result.Failure(Error.NotFound("SalesOrderLine", lineId));

            var result = line.RecordDelivery(quantity);
            if (result.IsFailure)
                return result;
        }

        AdvanceStatus();
        return Result.Success();
    }

    /// <summary>Impute une facturation sur les lignes désignées, puis réévalue l'avancement.</summary>
    public Result RecordInvoiced(IEnumerable<(Guid LineId, decimal Quantity)> invoiced)
    {
        if (!Status.CanBeInvoiced())
            return Result.Failure(Error.Validation("Status", "Cette commande ne peut pas être facturée dans son état actuel"));

        var items = invoiced?.ToList() ?? new List<(Guid, decimal)>();
        if (items.Count == 0)
            return Result.Failure(Error.Validation("Invoiced", "Aucune ligne à facturer"));

        foreach (var (lineId, quantity) in items)
        {
            var line = _lines.FirstOrDefault(l => l.Id == lineId);
            if (line is null)
                return Result.Failure(Error.NotFound("SalesOrderLine", lineId));

            var result = line.RecordInvoiced(quantity);
            if (result.IsFailure)
                return result;
        }

        AdvanceStatus();
        return Result.Success();
    }

    /// <summary>Annule les imputations d'un bon de livraison annulé, et rouvre le reliquat.</summary>
    public Result ReverseDeliveries(IEnumerable<(Guid LineId, decimal Quantity)> deliveries)
    {
        if (Status.IsFinalized() && Status != SalesOrderStatus.Completed)
            return Result.Failure(Error.Validation("Status", "Cette commande est close : aucune livraison ne peut être reprise"));

        foreach (var (lineId, quantity) in deliveries ?? Enumerable.Empty<(Guid, decimal)>())
        {
            var line = _lines.FirstOrDefault(l => l.Id == lineId);
            if (line is null)
                return Result.Failure(Error.NotFound("SalesOrderLine", lineId));

            var result = line.ReverseDelivery(quantity);
            if (result.IsFailure)
                return result;
        }

        CompletedAt = null;
        AdvanceStatus();
        return Result.Success();
    }

    /// <summary>Annule les imputations d'une facture annulée, et rouvre le reste à facturer.</summary>
    public Result ReverseInvoiced(IEnumerable<(Guid LineId, decimal Quantity)> invoiced)
    {
        if (Status is SalesOrderStatus.Cancelled)
            return Result.Failure(Error.Validation("Status", "Cette commande est annulée"));

        foreach (var (lineId, quantity) in invoiced ?? Enumerable.Empty<(Guid, decimal)>())
        {
            var line = _lines.FirstOrDefault(l => l.Id == lineId);
            if (line is null)
                return Result.Failure(Error.NotFound("SalesOrderLine", lineId));

            var result = line.ReverseInvoiced(quantity);
            if (result.IsFailure)
                return result;
        }

        CompletedAt = null;
        AdvanceStatus();
        return Result.Success();
    }

    public Result Cancel(string reason)
    {
        if (!Status.CanBeCancelled())
            return Result.Failure(Error.Validation("Status",
                "Cette commande a déjà donné lieu à des mouvements : utilisez la clôture avec abandon du reliquat"));

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(Error.Validation("CancellationReason", "Le motif d'annulation est obligatoire"));

        Status = SalesOrderStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        CancellationReason = reason.Trim();
        return Result.Success();
    }

    /// <summary>
    /// Solde une commande entamée en abandonnant le reste à livrer : le client renonce au
    /// reliquat. La commande sort du carnet de commandes sans effacer ce qui a été livré.
    /// </summary>
    public Result Close(string reason)
    {
        if (!Status.CanBeClosed())
            return Result.Failure(Error.Validation("Status", "Cette commande ne peut pas être clôturée dans son état actuel"));

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(Error.Validation("ClosureReason", "Le motif de clôture est obligatoire"));

        Status = SalesOrderStatus.Closed;
        ClosedAt = DateTime.UtcNow;
        ClosureReason = reason.Trim();
        return Result.Success();
    }

    // ─────────────────────────── Réservation de stock ───────────────────────────

    /// <summary>Marque le stock comme réservé. Idempotent.</summary>
    public Result MarkStockReserved()
    {
        if (!Status.HoldsStockReservation())
            return Result.Failure(Error.Validation("Status", "Cette commande ne peut pas porter de réservation de stock"));

        IsStockReserved = true;
        return Result.Success();
    }

    /// <summary>Marque le stock comme libéré. Idempotent.</summary>
    public void MarkStockReleased() => IsStockReserved = false;

    // ─────────────────────────── Avancement et agrégats ───────────────────────────

    /// <summary>
    /// Réévalue le statut à partir des deux avancements. Le statut retenu est toujours le MOINS
    /// avancé des deux, pour qu'une commande intégralement facturée mais partiellement livrée ne
    /// puisse pas apparaître comme soldée.
    /// </summary>
    private void AdvanceStatus()
    {
        if (Status is SalesOrderStatus.Cancelled or SalesOrderStatus.Closed)
            return;

        var allDelivered = _lines.All(l => l.IsFullyDelivered);
        var allInvoiced = _lines.All(l => l.IsFullyInvoiced);
        var anyMovement = _lines.Any(l => l.DeliveredQuantity > 0 || l.InvoicedQuantity > 0);

        if (allDelivered && allInvoiced)
        {
            Status = SalesOrderStatus.Completed;
            CompletedAt ??= DateTime.UtcNow;
            return;
        }

        if (allDelivered)
        {
            Status = SalesOrderStatus.Delivered;
            return;
        }

        Status = anyMovement ? SalesOrderStatus.PartiallyDelivered : SalesOrderStatus.Confirmed;
    }

    private void RecalculateTotals()
    {
        var currency = Money.DefaultCurrency;

        SubTotal = _lines.Aggregate(Money.Zero(currency), (sum, l) => sum.Add(l.SubTotal));
        FodecAmount = _lines.Aggregate(Money.Zero(currency), (sum, l) => sum.Add(l.FodecAmount));
        TotalVat = _lines.Aggregate(Money.Zero(currency), (sum, l) => sum.Add(l.VatAmount));

        var stamp = FiscalStampAmount.Currency == currency ? FiscalStampAmount.Amount : 0m;

        TotalAmount = Money.FromSignedAmount(
            SubTotal.Amount + FodecAmount.Amount + TotalVat.Amount + stamp, currency);
    }

    private void RenumberLines()
    {
        for (var i = 0; i < _lines.Count; i++)
            _lines[i].SetLineNumber(i + 1);
    }

    /// <summary>Vrai si tout est livré.</summary>
    public bool IsFullyDelivered => _lines.Count > 0 && _lines.All(l => l.IsFullyDelivered);

    /// <summary>Vrai si tout est facturé.</summary>
    public bool IsFullyInvoiced => _lines.Count > 0 && _lines.All(l => l.IsFullyInvoiced);

    /// <summary>Reste à livrer, toutes lignes confondues — l'indicateur du carnet de commandes.</summary>
    public decimal TotalPendingDeliveryQuantity => _lines.Sum(l => l.PendingDeliveryQuantity);

    /// <summary>Reste à facturer, toutes lignes confondues.</summary>
    public decimal TotalPendingInvoiceQuantity => _lines.Sum(l => l.PendingInvoiceQuantity);

    /// <summary>
    /// Valeur HT du reste à livrer — le carnet de commandes en montant.
    /// Calculé au prorata de la ligne, remise incluse.
    /// </summary>
    public decimal BacklogAmountHt => _lines.Sum(l =>
        l.Quantity == 0
            ? 0m
            : Math.Round(l.SubTotal.Amount * l.PendingDeliveryQuantity / l.Quantity, 3));

    public Dictionary<VatRate, Money> GetVatBreakdown() =>
        _lines.GroupBy(l => l.VatRate)
              .ToDictionary(g => g.Key, g => g.Aggregate(Money.Zero(), (sum, l) => sum.Add(l.VatAmount)));
}
