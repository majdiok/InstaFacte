using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Per-tenant sequential journal entry numbers (JV, JA, etc.).
/// </summary>
public sealed class JournalEntrySequence : Entity
{
    public string JournalCode { get; private set; } = null!;
    public int FiscalYear { get; private set; }
    public int LastSequence { get; private set; }

    private JournalEntrySequence() { }

    public static JournalEntrySequence Create(string journalCode, int fiscalYear)
    {
        return new JournalEntrySequence
        {
            JournalCode = journalCode.Trim().ToUpperInvariant(),
            FiscalYear = fiscalYear,
            LastSequence = 0
        };
    }

    public int Next()
    {
        LastSequence++;
        return LastSequence;
    }
}
