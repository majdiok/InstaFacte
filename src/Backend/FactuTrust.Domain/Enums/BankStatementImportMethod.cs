namespace FactuTrust.Domain.Enums;

/// <summary>Origine / mode d'import d'un relevé bancaire.</summary>
public enum BankStatementImportMethod
{
    Manual = 0,
    Csv = 1,
    Excel = 2,
    PdfText = 3,
    PdfOcr = 4,
    PdfLlm = 5,
    Image = 6
}
