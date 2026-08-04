using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities.Honoraires;

public sealed class HonorairesPayment : Entity
{
    public Guid HonorairesInvoiceId { get; private set; }
    public HonorairesInvoice Invoice { get; private set; } = null!;
    public DateTime PaymentDate { get; private set; }
    public Money Amount { get; private set; } = null!;
    public Money ClientWithholdingAmount { get; private set; } = null!;
    public PaymentMethod Method { get; private set; }
    public string? Reference { get; private set; }
    public string? Notes { get; private set; }
    public string? BankAccountLabel { get; private set; }

    public decimal AppliedAmount => Amount.Amount + ClientWithholdingAmount.Amount;

    private HonorairesPayment() { }

    public static Result<HonorairesPayment> Create(
        HonorairesInvoice invoice,
        DateTime paymentDate,
        Money amount,
        Money clientWithholdingAmount,
        PaymentMethod method,
        string? reference = null,
        string? notes = null,
        string? bankAccountLabel = null)
    {
        if (amount.Amount < 0 || clientWithholdingAmount.Amount < 0)
            return Result.Failure<HonorairesPayment>(Error.Validation("Amount", "Les montants ne peuvent pas être négatifs"));

        var applied = amount.Amount + clientWithholdingAmount.Amount;
        if (applied <= 0)
            return Result.Failure<HonorairesPayment>(Error.Validation("Amount", "Le montant encaissé ou la retenue doit être positif"));

        return Result.Success(new HonorairesPayment
        {
            HonorairesInvoiceId = invoice.Id,
            Invoice = invoice,
            PaymentDate = paymentDate,
            Amount = amount,
            ClientWithholdingAmount = clientWithholdingAmount,
            Method = method,
            Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            BankAccountLabel = string.IsNullOrWhiteSpace(bankAccountLabel) ? null : bankAccountLabel.Trim()
        });
    }
}
