namespace FactuTrust.Application.Features.Accounting.BankStatementImport;

/// <summary>Sortie JSON attendue du LLM pour un relevé bancaire.</summary>
public sealed class LlmBankStatementExtraction
{
    public string? BankName { get; set; }
    public string? Rib { get; set; }
    public string? HolderName { get; set; }
    public string? PeriodStart { get; set; }
    public string? PeriodEnd { get; set; }
    public string? StatementDate { get; set; }
    public decimal? OpeningBalance { get; set; }
    public decimal? ClosingBalance { get; set; }
    public string? Currency { get; set; }
    public List<LlmBankStatementLine> Lines { get; set; } = new();
    public string? Confidence { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public sealed class LlmBankStatementLine
{
    public string? TransactionDate { get; set; }
    public string? ValueDate { get; set; }
    public string? Reference { get; set; }
    public string? Description { get; set; }
    public decimal? Amount { get; set; }
    public bool? IsDebit { get; set; }
}
