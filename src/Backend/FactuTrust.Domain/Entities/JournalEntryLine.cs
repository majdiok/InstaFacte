using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

public sealed class JournalEntryLine : Entity
{
    public Guid JournalEntryId { get; private set; }
    public JournalEntry JournalEntry { get; private set; } = null!;
    public int LineNumber { get; private set; }
    public string AccountNumber { get; private set; } = null!;
    public string Label { get; private set; } = null!;
    public Money DebitAmount { get; private set; } = null!;
    public Money CreditAmount { get; private set; } = null!;
    public string? LetteringCode { get; private set; }

    /// <summary>
    /// Montant au débit exprimé dans la devise de transaction de l'écriture.
    ///
    /// <para>
    /// <b>Significatif uniquement lorsque <c>JournalEntry.CurrencyCode</c> diffère de la devise de
    /// tenue.</b> Pour une écriture en devise fonctionnelle — le cas de la totalité de l'existant —
    /// la colonne reste à 0 et c'est <see cref="DebitAmount"/> qui fait foi. Cette convention évite
    /// toute reprise de données sur les écritures déjà comptabilisées.
    /// </para>
    /// </summary>
    public decimal DebitAmountInCurrency { get; private set; }

    /// <summary>Montant au crédit dans la devise de transaction. Mêmes règles que <see cref="DebitAmountInCurrency"/>.</summary>
    public decimal CreditAmountInCurrency { get; private set; }
    public Guid? ThirdPartyId { get; private set; }
    public ThirdPartyKind ThirdPartyKind { get; private set; }

    private JournalEntryLine() { }

    internal static JournalEntryLine Create(
        int lineNumber,
        string accountNumber,
        string label,
        Money debitAmount,
        Money creditAmount,
        Guid? thirdPartyId,
        ThirdPartyKind thirdPartyKind,
        decimal debitInCurrency = 0m,
        decimal creditInCurrency = 0m)
    {
        return new JournalEntryLine
        {
            LineNumber = lineNumber,
            AccountNumber = accountNumber,
            Label = label,
            DebitAmount = debitAmount,
            CreditAmount = creditAmount,
            ThirdPartyId = thirdPartyId,
            ThirdPartyKind = thirdPartyKind,
            DebitAmountInCurrency = debitInCurrency,
            CreditAmountInCurrency = creditInCurrency
        };
    }

    internal void AttachToEntry(JournalEntry entry)
    {
        JournalEntry = entry;
        JournalEntryId = entry.Id;
    }

    public void SetLetteringCode(string? code)
    {
        LetteringCode = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
    }
}
