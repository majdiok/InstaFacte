using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Records a cash desk amount remitted to a bank account; linked to the generated cash debit operation.
/// </summary>
public sealed class BankDeposit : AggregateRoot
{
    public BankDepositNumber Number { get; private set; } = null!;

    public BankDepositType DepositType { get; private set; }

    public DateTime DepositDate { get; private set; }

    public Guid BankAccountId { get; private set; }

    public int Quantity { get; private set; }

    public Money Amount { get; private set; } = null!;

    /// <summary>Optional user reference (e.g. bank slip number written on the physical deposit).</summary>
    public string? DepositSlipReference { get; private set; }

    public string? Notes { get; private set; }

    public BankDepositStatus Status { get; private set; }

    public Guid CashOperationId { get; private set; }

    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    private BankDeposit() { }

    public static Result<BankDeposit> Create(
        BankDepositNumber number,
        BankDepositType depositType,
        DateTime depositDate,
        Guid bankAccountId,
        Money amount,
        int quantity,
        Guid cashOperationId,
        string? depositSlipReference = null,
        string? notes = null)
    {
        if (bankAccountId == Guid.Empty)
            return Result.Failure<BankDeposit>(Error.Validation("BankAccountId", "Le compte bancaire est obligatoire"));

        if (amount.Amount <= 0)
            return Result.Failure<BankDeposit>(Error.Validation("Amount", "Le montant doit être positif"));

        if (quantity < 1)
            return Result.Failure<BankDeposit>(Error.Validation("Quantity", "La quantité doit être au moins 1"));

        if (cashOperationId == Guid.Empty)
            return Result.Failure<BankDeposit>(Error.Validation("CashOperationId", "L'opération caisse liée est obligatoire"));

        if (depositDate > DateTime.UtcNow.AddDays(1))
            return Result.Failure<BankDeposit>(Error.Validation("DepositDate", "La date de remise ne peut pas être dans le futur"));

        if (!Enum.IsDefined(typeof(BankDepositType), depositType))
            return Result.Failure<BankDeposit>(Error.Validation("DepositType", "Le type de dépôt est invalide"));

        var slip = depositSlipReference?.Trim();
        if (string.IsNullOrWhiteSpace(slip))
            slip = null;
        if (slip != null && slip.Length > 100)
            return Result.Failure<BankDeposit>(Error.Validation("DepositSlipReference", "La référence ne peut pas dépasser 100 caractères"));

        var notesTrimmed = notes?.Trim();
        if (string.IsNullOrWhiteSpace(notesTrimmed))
            notesTrimmed = null;
        if (notesTrimmed != null && notesTrimmed.Length > 500)
            return Result.Failure<BankDeposit>(Error.Validation("Notes", "Les notes ne peuvent pas dépasser 500 caractères"));

        var entity = new BankDeposit
        {
            Number = number,
            DepositType = depositType,
            DepositDate = depositDate.Date,
            BankAccountId = bankAccountId,
            Amount = amount,
            Quantity = quantity,
            DepositSlipReference = slip,
            Notes = notesTrimmed,
            Status = BankDepositStatus.Terminee,
            CashOperationId = cashOperationId
        };

        return Result.Success(entity);
    }

    public Result Cancel(string cancellationReason)
    {
        if (Status == BankDepositStatus.Annulee)
            return Result.Failure(Error.Validation("Status", "Cette remise a déjà été annulée"));

        if (string.IsNullOrWhiteSpace(cancellationReason))
            return Result.Failure(Error.Validation("CancellationReason", "Le motif d'annulation est obligatoire"));

        var trimmed = cancellationReason.Trim();
        if (trimmed.Length > 500)
            return Result.Failure(Error.Validation("CancellationReason", "Le motif ne peut pas dépasser 500 caractères"));

        Status = BankDepositStatus.Annulee;
        CancelledAt = DateTime.UtcNow;
        CancellationReason = trimmed;

        IncrementVersion();
        return Result.Success();
    }
}
