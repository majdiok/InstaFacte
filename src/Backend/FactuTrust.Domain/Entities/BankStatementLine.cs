using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

public sealed class BankStatementLine : Entity
{
    public Guid BankStatementId { get; private set; }
    public BankStatement BankStatement { get; private set; } = null!;
    public DateTime TransactionDate { get; private set; }
    public DateTime? ValueDate { get; private set; }
    public string Reference { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public Money Amount { get; private set; } = null!;
    public bool IsDebit { get; private set; }
    public bool IsReconciled { get; private set; }
    public Guid? ReconciledJournalEntryLineId { get; private set; }

    /// <summary>
    /// Empreinte déterministe (compte|date|montant|sens|référence|libellé) servant au dédoublonnage
    /// lors du ré-import d'un même relevé. Nullable pour compatibilité avec les lignes historiques.
    /// </summary>
    public string? Fingerprint { get; private set; }

    private BankStatementLine() { }

    public static BankStatementLine Create(Guid bankStatementId, DateTime transactionDate,
        string reference, string description, Money amount, bool isDebit, DateTime? valueDate = null,
        string? fingerprint = null)
    {
        return new BankStatementLine
        {
            Id = Guid.NewGuid(),
            BankStatementId = bankStatementId,
            TransactionDate = transactionDate,
            ValueDate = valueDate?.Date,
            Reference = reference,
            Description = description,
            Amount = amount,
            IsDebit = isDebit,
            IsReconciled = false,
            Fingerprint = fingerprint
        };
    }

    /// <summary>
    /// Calcule l'empreinte déterministe d'une ligne de relevé, portée par le compte bancaire
    /// (deux imports du même mouvement sur le même compte produisent la même empreinte).
    /// </summary>
    public static string ComputeFingerprint(string accountNumber, DateTime transactionDate,
        decimal amount, bool isDebit, string? reference, string? description)
    {
        var canonical = string.Join('|',
            (accountNumber ?? string.Empty).Trim().ToUpperInvariant(),
            transactionDate.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            amount.ToString("0.###", CultureInfo.InvariantCulture),
            isDebit ? "D" : "C",
            (reference ?? string.Empty).Trim().ToUpperInvariant(),
            (description ?? string.Empty).Trim().ToUpperInvariant());
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }

    public void Reconcile(Guid journalEntryLineId)
    {
        IsReconciled = true;
        ReconciledJournalEntryLineId = journalEntryLineId;
    }

    public void Unreconcile()
    {
        IsReconciled = false;
        ReconciledJournalEntryLineId = null;
    }
}
