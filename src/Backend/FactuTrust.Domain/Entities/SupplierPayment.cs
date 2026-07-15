using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Represents a payment record for a supplier invoice.
/// Supports multiple partial payments (tranches) per invoice.
/// </summary>
public sealed class SupplierPayment : AggregateRoot
{
    public Guid SupplierInvoiceId { get; private set; }
    public SupplierInvoice SupplierInvoice { get; private set; } = null!;

    public Money Amount { get; private set; } = null!;
    public DateTime PaymentDate { get; private set; }
    public PaymentMethod Method { get; private set; }
    public string? Reference { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>Échéance de la traite (effet de commerce). Renseignée ssi <see cref="Method"/> == Traite.</summary>
    public DateTime? EffetDueDate { get; private set; }

    /// <summary>Statut de l'effet dans son cycle de vie. Null pour les paiements non-traite.</summary>
    public EffetStatus? EffetStatus { get; private set; }

    /// <summary>Date de paiement effectif de l'effet (échéance honorée). Null tant qu'en portefeuille.</summary>
    public DateTime? EffetSettledAt { get; private set; }

    /// <summary>Vrai si ce paiement est une traite (effet de commerce).</summary>
    public bool IsEffet => Method == PaymentMethod.Traite;

    private SupplierPayment() { }

    /// <summary>
    /// Creates a supplier payment. Validates amount and date.
    /// Caller must ensure amount does not exceed remaining amount due.
    /// </summary>
    public static Result<SupplierPayment> Create(
        SupplierInvoice supplierInvoice,
        Money amount,
        DateTime paymentDate,
        PaymentMethod method,
        string? reference = null,
        string? notes = null,
        DateTime? effetDueDate = null)
    {
        if (amount.Amount <= 0)
            return Result.Failure<SupplierPayment>(Error.Validation("Amount", "Le montant doit être positif"));

        if (paymentDate > DateTime.UtcNow.AddDays(1))
            return Result.Failure<SupplierPayment>(Error.Validation("PaymentDate", "La date de paiement ne peut pas être dans le futur"));

        if (amount.Currency != supplierInvoice.TotalAmount.Currency)
            return Result.Failure<SupplierPayment>(Error.Validation("Amount", "La devise du paiement doit correspondre à celle de la facture"));

        if (method == PaymentMethod.Traite && !effetDueDate.HasValue)
            return Result.Failure<SupplierPayment>(Error.Validation("EffetDueDate", "L'échéance de la traite est obligatoire"));

        var payment = new SupplierPayment
        {
            SupplierInvoiceId = supplierInvoice.Id,
            SupplierInvoice = supplierInvoice,
            Amount = amount,
            PaymentDate = paymentDate.Date,
            Method = method,
            Reference = reference?.Trim(),
            Notes = notes?.Trim(),
            EffetDueDate = method == PaymentMethod.Traite ? effetDueDate!.Value.Date : null,
            EffetStatus = method == PaymentMethod.Traite ? Enums.EffetStatus.EnPortefeuille : null
        };

        return Result.Success(payment);
    }

    /// <summary>
    /// Marque l'effet fournisseur payé à son échéance (la trésorerie a bougé : 403 → 532).
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
}
