using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>
/// Paiement (décaissement) d'un cycle de paie validé, ventilé par bulletin.
/// </summary>
public sealed class PayrollPayment : AggregateRoot
{
    public Guid PayrollRunId { get; private set; }
    public PayrollRun PayrollRun { get; private set; } = null!;

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

    private readonly List<PayrollPaymentLine> _lines = new();
    public IReadOnlyCollection<PayrollPaymentLine> Lines => _lines.AsReadOnly();

    private PayrollPayment() { }

    /// <summary>
    /// Crée un paiement de paie pour un cycle validé.
    /// </summary>
    public static Result<PayrollPayment> Create(
        PayrollRun run,
        IReadOnlyList<(Payslip Payslip, decimal Amount, string AuxiliaryAccount)> lineInputs,
        DateTime paymentDate,
        PaymentMethod method,
        Guid? bankAccountId,
        string? reference,
        string? notes)
    {
        if (run.Status is not PayrollRunStatus.Validated and not PayrollRunStatus.Closed)
        {
            return Result.Failure<PayrollPayment>(Error.Validation(
                "Status",
                "Le paiement n'est disponible qu'après validation du cycle de paie."));
        }

        if (lineInputs.Count == 0)
        {
            return Result.Failure<PayrollPayment>(Error.Validation(
                "Lines",
                "Au moins un bulletin doit être payé."));
        }

        if (paymentDate > DateTime.UtcNow.AddDays(1))
        {
            return Result.Failure<PayrollPayment>(Error.Validation(
                "PaymentDate",
                "La date de paiement ne peut pas être dans le futur."));
        }

        if (method == PaymentMethod.Traite)
        {
            return Result.Failure<PayrollPayment>(Error.Validation(
                "Method",
                "Le paiement de la paie par traite n'est pas autorisé."));
        }

        if (method != PaymentMethod.Cash && bankAccountId is null)
        {
            return Result.Failure<PayrollPayment>(Error.Validation(
                "BankAccountId",
                "Le compte bancaire débiteur est obligatoire pour ce mode de paiement."));
        }

        var currency = Money.DefaultCurrency;
        var payment = new PayrollPayment
        {
            PayrollRunId = run.Id,
            PaymentDate = paymentDate.Date,
            Method = method,
            BankAccountId = bankAccountId,
            Reference = reference?.Trim(),
            Notes = notes?.Trim()
        };

        foreach (var (payslip, amount, auxiliaryAccount) in lineInputs)
        {
            var lineResult = PayrollPaymentLine.Create(payslip, amount, auxiliaryAccount, currency);
            if (lineResult.IsFailure)
                return Result.Failure<PayrollPayment>(lineResult.Error);

            payment._lines.Add(lineResult.Value);
        }

        var total = Math.Round(payment._lines.Sum(l => l.Amount.Amount), 3, MidpointRounding.AwayFromZero);
        payment.Amount = Money.Create(total, currency);
        return Result.Success(payment);
    }

    public Result Cancel(string reason, string cancelledBy)
    {
        if (IsCancelled)
            return Result.Failure(Error.Validation("Payment", "Ce paiement est déjà annulé."));

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
}
