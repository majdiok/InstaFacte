using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities;

public sealed class FiscalScheduleEntry : AggregateRoot
{
    public FiscalObligationType ObligationType { get; private set; }
    public string ObligationLabel { get; private set; } = null!;
    public int FiscalYear { get; private set; }
    public int? PeriodMonth { get; private set; }
    public int? PeriodQuarter { get; private set; }
    public DateTime? PeriodStart { get; private set; }
    public DateTime? PeriodEnd { get; private set; }
    public DateTime DueDate { get; private set; }
    public decimal EstimatedAmount { get; private set; }
    public string Currency { get; private set; } = "TND";
    public FiscalScheduleSourceType SourceType { get; private set; }
    public Guid? SourceId { get; private set; }
    public DateTime? DepositDate { get; private set; }
    public DateTime? PaymentDate { get; private set; }
    public Guid? ResponsibleUserId { get; private set; }
    public string? ResponsibleName { get; private set; }
    public string? Observations { get; private set; }
    public DateTime? LastReminderAt { get; private set; }
    public FiscalReminderChannel? LastReminderChannel { get; private set; }
    public DateTime? ValidatedAt { get; private set; }
    public string? ValidatedBy { get; private set; }
    public bool IsCancelled { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private readonly List<FiscalScheduleHistoryEntry> _history = new();
    public IReadOnlyCollection<FiscalScheduleHistoryEntry> History => _history.AsReadOnly();

    private readonly List<FiscalScheduleAttachment> _attachments = new();
    public IReadOnlyCollection<FiscalScheduleAttachment> Attachments => _attachments.AsReadOnly();

    private FiscalScheduleEntry() { }

    public static Result<FiscalScheduleEntry> Create(
        FiscalObligationType obligationType,
        string obligationLabel,
        int fiscalYear,
        DateTime dueDate,
        decimal estimatedAmount,
        string currency = "TND",
        int? periodMonth = null,
        int? periodQuarter = null,
        DateTime? periodStart = null,
        DateTime? periodEnd = null,
        FiscalScheduleSourceType sourceType = FiscalScheduleSourceType.Manual,
        Guid? sourceId = null,
        Guid? responsibleUserId = null,
        string? responsibleName = null,
        string? observations = null)
    {
        var validation = ValidateCore(obligationLabel, fiscalYear, dueDate, estimatedAmount, currency, responsibleName, observations);
        if (validation.IsFailure)
            return Result.Failure<FiscalScheduleEntry>(validation.Error);

        return Result.Success(new FiscalScheduleEntry
        {
            ObligationType = obligationType,
            ObligationLabel = obligationLabel.Trim(),
            FiscalYear = fiscalYear,
            PeriodMonth = periodMonth,
            PeriodQuarter = periodQuarter,
            PeriodStart = periodStart?.Date,
            PeriodEnd = periodEnd?.Date,
            DueDate = dueDate.Date,
            EstimatedAmount = Math.Round(estimatedAmount, 3),
            Currency = NormalizeCurrency(currency),
            SourceType = sourceType,
            SourceId = sourceId,
            ResponsibleUserId = responsibleUserId,
            ResponsibleName = NormalizeNullable(responsibleName),
            Observations = NormalizeNullable(observations)
        });
    }

    public Result Update(
        FiscalObligationType obligationType,
        string obligationLabel,
        int fiscalYear,
        DateTime dueDate,
        decimal estimatedAmount,
        string currency,
        int? periodMonth,
        int? periodQuarter,
        DateTime? periodStart,
        DateTime? periodEnd,
        Guid? responsibleUserId,
        string? responsibleName,
        string? observations)
    {
        if (IsCancelled)
            return Result.Failure(Error.Validation("Status", "Une echeance annulee ne peut pas etre modifiee."));

        var validation = ValidateCore(obligationLabel, fiscalYear, dueDate, estimatedAmount, currency, responsibleName, observations);
        if (validation.IsFailure)
            return validation;

        ObligationType = obligationType;
        ObligationLabel = obligationLabel.Trim();
        FiscalYear = fiscalYear;
        PeriodMonth = periodMonth;
        PeriodQuarter = periodQuarter;
        PeriodStart = periodStart?.Date;
        PeriodEnd = periodEnd?.Date;
        DueDate = dueDate.Date;
        EstimatedAmount = Math.Round(estimatedAmount, 3);
        Currency = NormalizeCurrency(currency);
        ResponsibleUserId = responsibleUserId;
        ResponsibleName = NormalizeNullable(responsibleName);
        Observations = NormalizeNullable(observations);
        IncrementVersion();
        return Result.Success();
    }

    public Result MarkDeposited(DateTime depositDate)
    {
        if (IsCancelled)
            return Result.Failure(Error.Validation("Status", "Une echeance annulee ne peut pas etre deposee."));
        DepositDate = depositDate.Date;
        IncrementVersion();
        return Result.Success();
    }

    /// <summary>
    /// Aligne le montant estimé (synchronisation depuis la déclaration mensuelle).
    /// Refuse une échéance annulée ou un montant négatif.
    /// </summary>
    public Result UpdateEstimatedAmount(decimal amount)
    {
        if (IsCancelled)
            return Result.Failure(Error.Validation("Status", "Une echeance annulee ne peut pas etre modifiee."));
        if (amount < 0)
            return Result.Failure(Error.Validation("EstimatedAmount", "Le montant estime ne peut pas etre negatif."));
        EstimatedAmount = amount;
        IncrementVersion();
        return Result.Success();
    }

    /// <summary>Rattache l'échéance à sa pièce source (ex. la déclaration mensuelle). No-op si identique.</summary>
    public void LinkSource(Guid sourceId)
    {
        if (SourceId == sourceId)
            return;
        SourceId = sourceId;
        IncrementVersion();
    }

    public Result CapturePayment(DateTime paymentDate)
    {
        if (IsCancelled)
            return Result.Failure(Error.Validation("Status", "Une echeance annulee ne peut pas etre payee."));
        if (DepositDate.HasValue && paymentDate.Date < DepositDate.Value.Date)
            return Result.Failure(Error.Validation("PaymentDate", "La date de paiement ne peut pas etre anterieure a la date de depot."));
        PaymentDate = paymentDate.Date;
        IncrementVersion();
        return Result.Success();
    }

    public Result MarkReminder(FiscalReminderChannel channel, DateTime reminderAt)
    {
        if (IsCancelled)
            return Result.Failure(Error.Validation("Status", "Une echeance annulee ne peut pas recevoir de rappel."));
        LastReminderChannel = channel;
        LastReminderAt = reminderAt;
        IncrementVersion();
        return Result.Success();
    }

    public Result MarkValidated(DateTime validatedAt, string validatedBy)
    {
        if (IsCancelled)
            return Result.Failure(Error.Validation("Status", "Une echeance annulee ne peut pas etre validee."));
        if (!DepositDate.HasValue)
            return Result.Failure(Error.Validation("DepositDate", "L'echeance doit etre deposee avant validation."));
        if (PaymentDate.HasValue)
            return Result.Failure(Error.Validation("Status", "Une echeance deja payee ne peut pas etre validee."));
        if (ValidatedAt.HasValue)
            return Result.Failure(Error.Validation("Status", "Cette echeance est deja validee."));
        ValidatedAt = validatedAt.Date;
        ValidatedBy = string.IsNullOrWhiteSpace(validatedBy) ? null : validatedBy.Trim();
        IncrementVersion();
        return Result.Success();
    }

    public void Cancel()
    {
        IsCancelled = true;
        IncrementVersion();
    }

    public FiscalScheduleStatus ResolveStatus(DateTime today)
    {
        if (IsCancelled)
            return FiscalScheduleStatus.Cancelled;
        if (PaymentDate.HasValue)
            return FiscalScheduleStatus.Paid;
        if (ValidatedAt.HasValue)
            return FiscalScheduleStatus.Validated;
        if (DepositDate.HasValue)
            return FiscalScheduleStatus.Deposited;
        var due = DueDate.Date;
        var current = today.Date;
        if (due < current)
            return FiscalScheduleStatus.Overdue;
        return due <= current.AddDays(7)
            ? FiscalScheduleStatus.UpcomingWithin7Days
            : FiscalScheduleStatus.UpcomingAfter7Days;
    }

    private static Result ValidateCore(
        string obligationLabel,
        int fiscalYear,
        DateTime dueDate,
        decimal estimatedAmount,
        string currency,
        string? responsibleName,
        string? observations)
    {
        if (fiscalYear is < 2000 or > 2100)
            return Result.Failure(Error.Validation("FiscalYear", "L'exercice doit etre compris entre 2000 et 2100."));
        if (dueDate == default)
            return Result.Failure(Error.Validation("DueDate", "La date d'echeance est obligatoire."));
        if (estimatedAmount < 0)
            return Result.Failure(Error.Validation("EstimatedAmount", "Le montant estime ne peut pas etre negatif."));
        if (string.IsNullOrWhiteSpace(obligationLabel))
            return Result.Failure(Error.Validation("ObligationLabel", "Le libelle de l'obligation est obligatoire."));
        if (obligationLabel.Trim().Length > 200)
            return Result.Failure(Error.Validation("ObligationLabel", "Le libelle ne doit pas depasser 200 caracteres."));
        var c = NormalizeCurrency(currency);
        if (c.Length != 3)
            return Result.Failure(Error.Validation("Currency", "La devise doit contenir 3 caracteres."));
        if (!string.IsNullOrWhiteSpace(responsibleName) && responsibleName.Trim().Length > 200)
            return Result.Failure(Error.Validation("ResponsibleName", "Le responsable ne doit pas depasser 200 caracteres."));
        if (!string.IsNullOrWhiteSpace(observations) && observations.Trim().Length > 1000)
            return Result.Failure(Error.Validation("Observations", "Les observations ne doivent pas depasser 1000 caracteres."));
        return Result.Success();
    }

    private static string NormalizeCurrency(string currency)
        => string.IsNullOrWhiteSpace(currency) ? "TND" : currency.Trim().ToUpperInvariant();

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
