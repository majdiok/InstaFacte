using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;

namespace FactuTrust.Domain.Entities.RecurringContracts;

public sealed class RecurringContract : AggregateRoot
{
    public string? Number { get; private set; }
    public Guid ClientId { get; private set; }
    public RecurringContractStatus Status { get; private set; }
    public BillingFrequency BillingFrequency { get; private set; }
    public int BillingDayOfMonth { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public DateTime? NextBillingDate { get; private set; }
    public DateTime? LastBilledPeriodEnd { get; private set; }
    public Guid? PaymentTermTemplateId { get; private set; }
    public Guid? PriceListId { get; private set; }
    public bool AutoRenew { get; private set; }
    public int NoticePeriodDays { get; private set; }
    public string Currency { get; private set; } = "TND";
    public Guid? SourceQuoteId { get; private set; }
    public string? Reference { get; private set; }
    public string? Notes { get; private set; }
    public bool SetupFeeBilled { get; private set; }

    private readonly List<RecurringContractLine> _lines = new();
    public IReadOnlyCollection<RecurringContractLine> Lines => _lines.AsReadOnly();

    private RecurringContract() { }

    public static Result<RecurringContract> CreateDraft(
        Guid clientId,
        BillingFrequency billingFrequency,
        int billingDayOfMonth,
        DateTime startDate,
        DateTime? endDate = null,
        bool autoRenew = true,
        int noticePeriodDays = 30,
        string currency = "TND",
        Guid? paymentTermTemplateId = null,
        Guid? priceListId = null,
        Guid? sourceQuoteId = null,
        string? reference = null,
        string? notes = null)
    {
        if (clientId == Guid.Empty)
            return Result.Failure<RecurringContract>(Error.Validation("ClientId", "Le client est obligatoire"));
        if (billingDayOfMonth is < 1 or > 31)
            return Result.Failure<RecurringContract>(Error.Validation("BillingDayOfMonth", "Le jour de facturation doit être entre 1 et 31"));
        if (billingFrequency == BillingFrequency.OneOff)
            return Result.Failure<RecurringContract>(Error.Validation("BillingFrequency", "La périodicité ponctuelle n'est pas autorisée pour un contrat récurrent"));
        if (endDate.HasValue && endDate.Value.Date < startDate.Date)
            return Result.Failure<RecurringContract>(Error.Validation("EndDate", "La date de fin doit être postérieure au début"));

        return Result.Success(new RecurringContract
        {
            ClientId = clientId,
            Status = RecurringContractStatus.Draft,
            BillingFrequency = billingFrequency,
            BillingDayOfMonth = billingDayOfMonth,
            StartDate = startDate.Date,
            EndDate = endDate?.Date,
            AutoRenew = autoRenew,
            NoticePeriodDays = Math.Max(0, noticePeriodDays),
            Currency = string.IsNullOrWhiteSpace(currency) ? "TND" : currency.Trim().ToUpperInvariant(),
            PaymentTermTemplateId = paymentTermTemplateId,
            PriceListId = priceListId,
            SourceQuoteId = sourceQuoteId,
            Reference = TrimOrNull(reference),
            Notes = TrimOrNull(notes)
        });
    }

    public Result AssignNumber(string number)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce contrat ne peut plus être modifié"));
        if (string.IsNullOrWhiteSpace(number))
            return Result.Failure(Error.Validation("Number", "Le numéro est obligatoire"));
        Number = number.Trim();
        return Result.Success();
    }

    public Result UpdateHeader(
        BillingFrequency billingFrequency,
        int billingDayOfMonth,
        DateTime startDate,
        DateTime? endDate,
        bool autoRenew,
        int noticePeriodDays,
        Guid? paymentTermTemplateId,
        Guid? priceListId,
        string? reference,
        string? notes)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce contrat ne peut plus être modifié"));
        if (billingDayOfMonth is < 1 or > 31)
            return Result.Failure(Error.Validation("BillingDayOfMonth", "Le jour de facturation doit être entre 1 et 31"));
        if (billingFrequency == BillingFrequency.OneOff)
            return Result.Failure(Error.Validation("BillingFrequency", "La périodicité ponctuelle n'est pas autorisée"));
        if (endDate.HasValue && endDate.Value.Date < startDate.Date)
            return Result.Failure(Error.Validation("EndDate", "La date de fin doit être postérieure au début"));

        BillingFrequency = billingFrequency;
        BillingDayOfMonth = billingDayOfMonth;
        StartDate = startDate.Date;
        EndDate = endDate?.Date;
        AutoRenew = autoRenew;
        NoticePeriodDays = Math.Max(0, noticePeriodDays);
        PaymentTermTemplateId = paymentTermTemplateId;
        PriceListId = priceListId;
        Reference = TrimOrNull(reference);
        Notes = TrimOrNull(notes);
        IncrementVersion();
        return Result.Success();
    }

    public Result<RecurringContractLine> AddLine(
        RecurringContractLineType lineType,
        string description,
        decimal quantity,
        decimal unitPriceHt,
        decimal vatRate,
        Guid? productId = null,
        Guid? usageMetricId = null,
        decimal? includedQuantity = null,
        decimal? overageUnitPriceHt = null)
    {
        if (!Status.CanBeEdited())
            return Result.Failure<RecurringContractLine>(Error.Validation("Status", "Ce contrat ne peut plus être modifié"));

        var lineResult = RecurringContractLine.Create(
            Id, lineType, description, quantity, unitPriceHt, vatRate, StartDate,
            productId, usageMetricId, includedQuantity, overageUnitPriceHt, _lines.Count);
        if (lineResult.IsFailure)
            return lineResult;

        _lines.Add(lineResult.Value);
        IncrementVersion();
        return lineResult;
    }

    public Result Activate()
    {
        if (Status != RecurringContractStatus.Draft && Status != RecurringContractStatus.Suspended)
            return Result.Failure(Error.Validation("Status", "Seul un contrat brouillon ou suspendu peut être activé"));
        if (_lines.Count == 0)
            return Result.Failure(Error.Validation("Lines", "Au moins une ligne de contrat est requise"));

        Status = RecurringContractStatus.Active;
        NextBillingDate ??= ComputeInitialBillingDate();
        IncrementVersion();
        return Result.Success();
    }

    public Result Suspend()
    {
        if (Status != RecurringContractStatus.Active)
            return Result.Failure(Error.Validation("Status", "Seul un contrat actif peut être suspendu"));
        Status = RecurringContractStatus.Suspended;
        IncrementVersion();
        return Result.Success();
    }

    public Result Resume()
    {
        if (Status != RecurringContractStatus.Suspended)
            return Result.Failure(Error.Validation("Status", "Seul un contrat suspendu peut être repris"));
        Status = RecurringContractStatus.Active;
        NextBillingDate ??= ComputeInitialBillingDate();
        IncrementVersion();
        return Result.Success();
    }

    public Result Cancel(DateTime? cancellationDate = null)
    {
        if (Status is RecurringContractStatus.Cancelled or RecurringContractStatus.Expired)
            return Result.Failure(Error.Validation("Status", "Ce contrat est déjà clos"));
        Status = RecurringContractStatus.Cancelled;
        EndDate = cancellationDate?.Date ?? DateTime.UtcNow.Date;
        IncrementVersion();
        return Result.Success();
    }

    public Result MarkExpired()
    {
        Status = RecurringContractStatus.Expired;
        IncrementVersion();
        return Result.Success();
    }

    /// <summary>
    /// Renouvelle le contrat d'une durée identique à la durée initiale (EndDate - StartDate).
    /// - Active : prolonge EndDate (renouvellement anticipé).
    /// - Expired : réactive, EndDate = max(aujourd'hui, EndDate) + durée, NextBillingDate recalculée.
    /// Refus : Cancelled, Draft, Suspended, ou contrat sans EndDate.
    /// </summary>
    public Result Renew(DateTime asOfDate)
    {
        if (Status == RecurringContractStatus.Cancelled)
            return Result.Failure(Error.Validation("Status", "Un contrat résilié ne peut pas être renouvelé"));
        if (Status is RecurringContractStatus.Draft or RecurringContractStatus.Suspended)
            return Result.Failure(Error.Validation("Status", "Seul un contrat actif ou expiré peut être renouvelé"));
        if (!EndDate.HasValue)
            return Result.Failure(Error.Validation("EndDate", "Ce contrat n'a pas de date de fin à prolonger"));

        var duration = ComputeRenewalDuration(StartDate, EndDate.Value);
        if (duration.Months == 0 && duration.Days <= 0)
            return Result.Failure(Error.Validation("EndDate", "La durée initiale du contrat est nulle"));

        if (Status == RecurringContractStatus.Expired)
        {
            var baseDate = EndDate.Value.Date > asOfDate.Date ? EndDate.Value.Date : asOfDate.Date;
            EndDate = ApplyRenewalDuration(baseDate, duration);
            Status = RecurringContractStatus.Active;
            if (!NextBillingDate.HasValue || NextBillingDate.Value.Date < asOfDate.Date)
            {
                var candidate = ProrationCalculator.ClampBillingDay(asOfDate.Year, asOfDate.Month, BillingDayOfMonth);
                if (candidate < asOfDate.Date)
                {
                    var next = asOfDate.AddMonths(1);
                    candidate = ProrationCalculator.ClampBillingDay(next.Year, next.Month, BillingDayOfMonth);
                }
                NextBillingDate = candidate;
            }
        }
        else // Active
        {
            EndDate = ApplyRenewalDuration(EndDate.Value.Date, duration);
        }

        IncrementVersion();
        return Result.Success();
    }

    /// <summary>
    /// Applique un avenant de lignes sur un contrat Active par fenêtres d'effet, à EFFET IMMÉDIAT :
    /// - ligne active absente de la cible ou modifiée → Deactivate(J - 1 jour) ;
    /// - ligne nouvelle ou modifiée → nouvelle ligne EffectiveFrom = J ;
    /// - lignes actives inchangées → intactes.
    /// L'en-tête n'est PAS modifié (réservé aux brouillons).
    /// Phase 1 : effectiveDate == date du jour uniquement — Deactivate bascule IsActive = false
    /// immédiatement, donc une date d'effet future ouvrirait un trou de facturation (l'ancienne
    /// ligne serait exclue de GetActiveLinesOn avant même sa fin d'effet). Avec effet = J, la
    /// période courante est déjà matérialisée (run existant non modifié) ou le prochain run ne
    /// couvre que la nouvelle ligne : pas de réécriture d'une période déjà facturée.
    /// </summary>
    public Result AmendLines(
        DateTime effectiveDate,
        IReadOnlyList<RecurringContractAmendLineTarget> targetLines,
        DateTime today)
    {
        if (Status != RecurringContractStatus.Active)
            return Result.Failure(Error.Validation("Status", "Les avenants de lignes ne s'appliquent qu'à un contrat actif"));
        if (effectiveDate.Date != today.Date)
            return Result.Failure(Error.Validation("EffectiveDate", "La date d'effet doit être la date du jour (effet immédiat ; les avenants à effet différé sont une extension phase 2)"));

        var closeDate = effectiveDate.Date.AddDays(-1);

        // Règle de correspondance cible ↔ ligne existante : Id source quand fourni,
        // sinon couple (LineType, Description). Une même cible ne peut matcher qu'une ligne.
        var matchedTargetIndexes = new HashSet<int>();
        var activeExisting = _lines.Where(l => l.IsActive).ToList();

        // Plan de mutation calculé AVANT toute modification (validation d'abord).
        var toClose = new List<RecurringContractLine>();
        var toAdd = new List<RecurringContractLine>();

        foreach (var existing in activeExisting)
        {
            var targetIndex = FindTargetIndex(existing, targetLines, matchedTargetIndexes);
            if (targetIndex < 0)
            {
                // Ligne supprimée de la cible → clôture à J - 1.
                toClose.Add(existing);
                continue;
            }

            var target = targetLines[targetIndex];
            matchedTargetIndexes.Add(targetIndex);
            if (HasLineChanged(existing, target.Line))
            {
                // Ligne modifiée → clôture de l'ancienne à J - 1, nouvelle ligne dès J.
                toClose.Add(existing);
                toAdd.Add(target.Line);
            }
            // Ligne inchangée → intacte.
        }

        // Cibles jamais matchées → lignes nouvelles (EffectiveFrom = J, posé par l'appelant).
        for (var i = 0; i < targetLines.Count; i++)
        {
            if (!matchedTargetIndexes.Contains(i))
                toAdd.Add(targetLines[i].Line);
        }

        var resultingActiveCount = (activeExisting.Count - toClose.Count) + toAdd.Count;
        if (resultingActiveCount == 0)
            return Result.Failure(Error.Validation("Lines", "L'avenant laisserait le contrat sans ligne active"));

        foreach (var line in toClose)
            line.Deactivate(closeDate);
        foreach (var line in toAdd)
            _lines.Add(line);

        IncrementVersion();
        return Result.Success();
    }

    /// <summary>Met à jour les notes quel que soit le statut (champ non contractuel).</summary>
    public void UpdateNotes(string? notes)
    {
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        IncrementVersion();
    }

    public bool CanBillForPeriod(DateTime asOfDate)
    {
        if (!Status.CanBill())
            return false;
        if (!NextBillingDate.HasValue)
            return false;
        if (NextBillingDate.Value.Date > asOfDate.Date.AddDays(3))
            return false;
        if (EndDate.HasValue && NextBillingDate.Value.Date > EndDate.Value.Date)
            return false;
        return true;
    }

    public (DateTime PeriodFrom, DateTime PeriodTo) GetCurrentBillingPeriod()
    {
        if (!NextBillingDate.HasValue)
            throw new InvalidOperationException("NextBillingDate is not set");
        return ProrationCalculator.ResolveBillingPeriod(NextBillingDate.Value, BillingFrequency);
    }

    public void AdvanceBillingSchedule()
    {
        if (!NextBillingDate.HasValue)
            return;

        var (_, periodTo) = GetCurrentBillingPeriod();
        LastBilledPeriodEnd = periodTo;
        NextBillingDate = ProrationCalculator.ComputeNextBillingDate(
            NextBillingDate.Value, BillingFrequency, BillingDayOfMonth);

        if (EndDate.HasValue && NextBillingDate.Value.Date > EndDate.Value.Date && !AutoRenew)
            Status = RecurringContractStatus.Expired;
    }

    public void MarkSetupFeeBilled() => SetupFeeBilled = true;

    public IEnumerable<RecurringContractLine> GetActiveLinesOn(DateTime date) =>
        _lines.Where(l => l.IsEffectiveOn(date));

    private DateTime ComputeInitialBillingDate() =>
        RecurringContractScheduleProjector.ComputeInitialBillingDate(StartDate, BillingDayOfMonth);

    /// <summary>
    /// Durée de renouvellement (D7) : si la période initiale est un nombre entier de mois
    /// inclusifs (EndDate == StartDate.AddMonths(N).AddDays(-1)), on retient N mois calendaires
    /// (01/01→31/12 prolonge d'un an au 31/12, pas de 364 jours) ; sinon la durée exacte en jours.
    /// N vaut deltaMois + 1 pour les périodes « 1er → fin de mois » et deltaMois pour les
    /// débuts en cours de mois (15/03→14/09 : N=6) — les deux candidats sont testés.
    /// </summary>
    private static (int Months, int Days) ComputeRenewalDuration(DateTime startDate, DateTime endDate)
    {
        var monthDelta = (endDate.Year - startDate.Year) * 12 + (endDate.Month - startDate.Month);
        foreach (var n in new[] { monthDelta, monthDelta + 1 })
        {
            if (n > 0 && startDate.Date.AddMonths(n).AddDays(-1) == endDate.Date)
                return (n, 0);
        }
        return (0, (endDate.Date - startDate.Date).Days);
    }

    private static DateTime ApplyRenewalDuration(DateTime baseDate, (int Months, int Days) duration) =>
        duration.Months > 0
            ? baseDate.AddMonths(duration.Months)              // baseDate est un EndDate inclusif : AddMonths(N) préserve la convention (31/12 + 12 mois = 31/12)
            : baseDate.AddDays(duration.Days);

    private static int FindTargetIndex(
        RecurringContractLine existing,
        IReadOnlyList<RecurringContractAmendLineTarget> targets,
        HashSet<int> alreadyMatched)
    {
        // 1) Correspondance par Id source quand la cible en porte un.
        for (var i = 0; i < targets.Count; i++)
        {
            var sourceId = targets[i].SourceLineId;
            if (!alreadyMatched.Contains(i) && sourceId.HasValue && sourceId.Value == existing.Id)
                return i;
        }
        // 2) Repli : même type de ligne et même description.
        for (var i = 0; i < targets.Count; i++)
        {
            if (!alreadyMatched.Contains(i) &&
                targets[i].Line.LineType == existing.LineType &&
                string.Equals(targets[i].Line.Description, existing.Description, StringComparison.Ordinal))
                return i;
        }
        return -1;
    }

    private static bool HasLineChanged(RecurringContractLine existing, RecurringContractLine target) =>
        existing.Quantity != target.Quantity ||
        existing.UnitPriceHT != target.UnitPriceHT ||
        existing.VatRate != target.VatRate ||
        existing.UsageMetricId != target.UsageMetricId ||
        existing.IncludedQuantity != target.IncludedQuantity ||
        existing.OverageUnitPriceHT != target.OverageUnitPriceHT ||
        existing.ProductId != target.ProductId;

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
