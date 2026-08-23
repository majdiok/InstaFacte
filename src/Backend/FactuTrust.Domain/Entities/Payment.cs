using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Represents a payment record for an invoice.
/// </summary>
public sealed class Payment : AggregateRoot
{
    public Guid InvoiceId { get; private set; }
    public Invoice Invoice { get; private set; } = null!;
    
    public Money Amount { get; private set; } = null!;
    public DateTime PaymentDate { get; private set; }
    public PaymentMethod Method { get; private set; }
    public string? Reference { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>Retenue à la source subie sur cet encaissement (client débiteur) — hors export TEJ déclarant.</summary>
    public decimal? ClientWithholdingAmount { get; private set; }

    /// <summary>Échéance de la traite (effet de commerce). Renseignée ssi <see cref="Method"/> == Traite.</summary>
    public DateTime? EffetDueDate { get; private set; }

    /// <summary>Statut de l'effet dans son cycle de vie. Null pour les paiements non-traite.</summary>
    public EffetStatus? EffetStatus { get; private set; }

    /// <summary>Date d'encaissement effectif de l'effet (échéance honorée). Null tant qu'en portefeuille.</summary>
    public DateTime? EffetSettledAt { get; private set; }

    public bool IsRefunded { get; private set; }
    public DateTime? RefundedAt { get; private set; }
    public string? RefundReason { get; private set; }

    /// <summary>Optional POS cash-register session (vacation) that recorded this payment.</summary>
    public Guid? CashRegisterSessionId { get; private set; }

    /// <summary>Vrai si ce paiement est une traite (effet de commerce).</summary>
    public bool IsEffet => Method == PaymentMethod.Traite;

    private Payment() { }

    public static Result<Payment> Create(
        Invoice invoice,
        Money amount,
        DateTime paymentDate,
        PaymentMethod method,
        string? reference = null,
        string? notes = null,
        decimal? clientWithholdingAmount = null,
        DateTime? effetDueDate = null)
    {
        if (amount.Amount <= 0)
            return Result.Failure<Payment>(Error.Validation("Amount", "Le montant doit être positif"));

        if (paymentDate > DateTime.UtcNow.AddDays(1))
            return Result.Failure<Payment>(Error.Validation("PaymentDate", "La date de paiement ne peut pas être dans le futur"));

        if (clientWithholdingAmount.HasValue && clientWithholdingAmount.Value < 0)
            return Result.Failure<Payment>(Error.Validation("ClientWithholdingAmount", "La retenue subie ne peut pas être négative"));

        if (method == PaymentMethod.Traite && !effetDueDate.HasValue)
            return Result.Failure<Payment>(Error.Validation("EffetDueDate", "L'échéance de la traite est obligatoire"));

        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            Invoice = invoice,
            Amount = amount,
            PaymentDate = paymentDate.Date,
            Method = method,
            Reference = reference?.Trim(),
            Notes = notes?.Trim(),
            ClientWithholdingAmount = clientWithholdingAmount is > 0 ? Math.Round(clientWithholdingAmount.Value, 3) : null,
            EffetDueDate = method == PaymentMethod.Traite ? effetDueDate!.Value.Date : null,
            EffetStatus = method == PaymentMethod.Traite ? Enums.EffetStatus.EnPortefeuille : null,
            IsRefunded = false
        };

        return Result.Success(payment);
    }

    /// <summary>
    /// Marque l'effet réglé à son échéance : encaissé (trésorerie créditée) ou impayé (créance réouverte).
    /// N'est valide que pour une traite encore en portefeuille.
    /// </summary>
    public Result MarkEffetSettled(DateTime settledDate, EffetStatus outcome)
    {
        if (!IsEffet)
            return Result.Failure(Error.Validation("Effet", "Ce paiement n'est pas une traite"));

        if (EffetStatus != Enums.EffetStatus.EnPortefeuille)
            return Result.Failure(Error.Validation("Effet", "Seul un effet en portefeuille peut être réglé"));

        if (outcome != Enums.EffetStatus.Encaisse && outcome != Enums.EffetStatus.Impaye)
            return Result.Failure(Error.Validation("Effet", "L'issue de règlement de l'effet est invalide"));

        EffetStatus = outcome;
        EffetSettledAt = settledDate.Date;
        return Result.Success();
    }

    /// <summary>Montant net encaissé + retenue subie (pour solder le TTC facture).</summary>
    public decimal GetTotalAppliedTowardInvoice() => Amount.Amount + (ClientWithholdingAmount ?? 0m);

    public Result Refund(string reason)
    {
        if (IsRefunded)
            return Result.Failure(Error.Validation("Refund", "Ce paiement a déjà été remboursé"));

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(Error.Validation("RefundReason", "Le motif du remboursement est obligatoire"));

        IsRefunded = true;
        RefundedAt = DateTime.UtcNow;
        RefundReason = reason.Trim();

        return Result.Success();
    }

    public void AssignCashRegisterSession(Guid sessionId)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("L'identifiant de session caisse est requis.", nameof(sessionId));

        CashRegisterSessionId = sessionId;
    }
}
