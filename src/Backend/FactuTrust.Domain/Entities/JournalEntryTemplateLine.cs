using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

public sealed class JournalEntryTemplateLine : Entity
{
    public Guid JournalEntryTemplateId { get; private set; }
    public JournalEntryTemplate JournalEntryTemplate { get; private set; } = null!;
    public int LineNumber { get; private set; }
    public string AccountNumber { get; private set; } = null!;
    public string? LineLabelTemplate { get; private set; }
    /// <summary>Optional fixed debit amount. Null means "leave empty for user input".</summary>
    public decimal? FixedDebit { get; private set; }
    /// <summary>Optional fixed credit amount. Null means "leave empty for user input".</summary>
    public decimal? FixedCredit { get; private set; }

    private JournalEntryTemplateLine() { }

    public static Result<JournalEntryTemplateLine> Create(
        JournalEntryTemplate template,
        int lineNumber,
        string accountNumber,
        string? lineLabelTemplate = null,
        decimal? fixedDebit = null,
        decimal? fixedCredit = null)
    {
        accountNumber = accountNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(accountNumber))
            return Result.Failure<JournalEntryTemplateLine>(Error.Validation("AccountNumber", "Le numéro de compte est obligatoire"));

        if (fixedDebit is < 0)
            return Result.Failure<JournalEntryTemplateLine>(Error.Validation("FixedDebit", "Le débit doit être positif ou nul"));
        if (fixedCredit is < 0)
            return Result.Failure<JournalEntryTemplateLine>(Error.Validation("FixedCredit", "Le crédit doit être positif ou nul"));
        if (fixedDebit is > 0 && fixedCredit is > 0)
            return Result.Failure<JournalEntryTemplateLine>(Error.Validation("Amount", "Une ligne ne peut pas avoir un débit et un crédit pré-définis simultanément"));

        return Result.Success(new JournalEntryTemplateLine
        {
            JournalEntryTemplate = template,
            JournalEntryTemplateId = template.Id,
            LineNumber = lineNumber > 0 ? lineNumber : 1,
            AccountNumber = accountNumber,
            LineLabelTemplate = lineLabelTemplate?.Trim(),
            FixedDebit = fixedDebit,
            FixedCredit = fixedCredit
        });
    }
}
