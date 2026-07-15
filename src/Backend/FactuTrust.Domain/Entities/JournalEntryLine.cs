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
        ThirdPartyKind thirdPartyKind)
    {
        return new JournalEntryLine
        {
            LineNumber = lineNumber,
            AccountNumber = accountNumber,
            Label = label,
            DebitAmount = debitAmount,
            CreditAmount = creditAmount,
            ThirdPartyId = thirdPartyId,
            ThirdPartyKind = thirdPartyKind
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
