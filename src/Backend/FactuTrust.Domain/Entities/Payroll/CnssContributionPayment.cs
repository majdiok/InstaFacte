using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>
/// Versement mensuel des cotisations CNSS (compte 453) pour une période de paie.
/// </summary>
public sealed class CnssContributionPayment : AggregateRoot
{
    public int Year { get; private set; }
    public int Month { get; private set; }
    public Guid PayrollRunId { get; private set; }
    public PayrollRun PayrollRun { get; private set; } = null!;

    public decimal TotalCnssEmployee { get; private set; }
    public decimal TotalCnssEmployer { get; private set; }
    public decimal TotalWorkAccident { get; private set; }
    public decimal TotalDue { get; private set; }

    public Money Amount { get; private set; } = null!;
    public DateTime PaymentDate { get; private set; }
    public PaymentMethod Method { get; private set; }
    public Guid? BankAccountId { get; private set; }
    public string? Reference { get; private set; }
    public string? Notes { get; private set; }

    public bool IsCancelled { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancelledBy { get; private set; }
    public string? CancellationReason { get; private set; }

    private CnssContributionPayment() { }

    public static Result<CnssContributionPayment> Create(
        PayrollRun run,
        decimal totalCnssEmployee,
        decimal totalCnssEmployer,
        decimal totalWorkAccident,
        decimal totalDue,
        decimal amount,
        DateTime paymentDate,
        PaymentMethod method,
        Guid? bankAccountId,
        string? reference,
        string? notes)
    {
        if (run.Status is not PayrollRunStatus.Validated and not PayrollRunStatus.Closed)
        {
            return Result.Failure<CnssContributionPayment>(Error.Validation(
                "Status",
                "Le versement CNSS n'est disponible qu'après validation du cycle de paie."));
        }

        if (paymentDate > DateTime.UtcNow.AddDays(1))
        {
            return Result.Failure<CnssContributionPayment>(Error.Validation(
                "PaymentDate",
                "La date de paiement ne peut pas être dans le futur."));
        }

        if (method == PaymentMethod.Traite)
        {
            return Result.Failure<CnssContributionPayment>(Error.Validation(
                "Method",
                "Le versement CNSS par traite n'est pas autorisé."));
        }

        if (method != PaymentMethod.Cash && bankAccountId is null)
        {
            return Result.Failure<CnssContributionPayment>(Error.Validation(
                "BankAccountId",
                "Le compte bancaire débiteur est obligatoire pour ce mode de paiement."));
        }

        var roundedDue = R(totalDue);
        var roundedAmount = R(amount);
        if (Math.Abs(roundedAmount - roundedDue) > 0.001m)
        {
            return Result.Failure<CnssContributionPayment>(Error.Validation(
                "Amount",
                "Le montant payé doit correspondre au total à verser CNSS."));
        }

        if (roundedAmount <= 0)
        {
            return Result.Failure<CnssContributionPayment>(Error.Validation(
                "Amount",
                "Le montant du versement CNSS doit être strictement positif."));
        }

        var currency = Money.DefaultCurrency;
        var payment = new CnssContributionPayment
        {
            Year = run.Year,
            Month = run.Month,
            PayrollRunId = run.Id,
            TotalCnssEmployee = R(totalCnssEmployee),
            TotalCnssEmployer = R(totalCnssEmployer),
            TotalWorkAccident = R(totalWorkAccident),
            TotalDue = roundedDue,
            Amount = Money.Create(roundedAmount, currency),
            PaymentDate = paymentDate.Date,
            Method = method,
            BankAccountId = bankAccountId,
            Reference = reference?.Trim(),
            Notes = notes?.Trim()
        };

        return Result.Success(payment);
    }

    public Result Cancel(string reason, string cancelledBy)
    {
        if (IsCancelled)
            return Result.Failure(Error.Validation("Payment", "Ce versement CNSS est déjà annulé."));

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(Error.Validation(
                "CancellationReason",
                "Le motif d'annulation est obligatoire."));
        }

        IsCancelled = true;
        CancelledAt = DateTime.UtcNow;
        CancelledBy = string.IsNullOrWhiteSpace(cancelledBy) ? null : cancelledBy.Trim();
        CancellationReason = reason.Trim();
        IncrementVersion();
        return Result.Success();
    }

    public CnssRemittancePaymentStatus Status =>
        IsCancelled ? CnssRemittancePaymentStatus.Cancelled : CnssRemittancePaymentStatus.Paid;

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
