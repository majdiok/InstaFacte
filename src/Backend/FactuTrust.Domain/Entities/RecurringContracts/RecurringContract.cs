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

    private DateTime ComputeInitialBillingDate()
    {
        var candidate = ProrationCalculator.ClampBillingDay(
            StartDate.Year, StartDate.Month, BillingDayOfMonth);
        if (candidate < StartDate.Date)
        {
            var next = StartDate.AddMonths(1);
            candidate = ProrationCalculator.ClampBillingDay(next.Year, next.Month, BillingDayOfMonth);
        }
        return candidate;
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
