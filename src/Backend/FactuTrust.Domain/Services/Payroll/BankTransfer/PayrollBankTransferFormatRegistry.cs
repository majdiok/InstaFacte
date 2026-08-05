namespace FactuTrust.Domain.Services.Payroll.BankTransfer;

/// <summary>Résout un format d'export vers son writer (stateless, extensible).</summary>
public static class PayrollBankTransferFormatRegistry
{
    private static readonly IReadOnlyDictionary<PayrollBankTransferFormat, IPayrollBankTransferFormatWriter> Writers =
        new Dictionary<PayrollBankTransferFormat, IPayrollBankTransferFormatWriter>
        {
            [PayrollBankTransferFormat.StandardCsv] = new CsvPayrollBankTransferWriter()
        };

    public static IPayrollBankTransferFormatWriter GetWriter(PayrollBankTransferFormat format)
    {
        if (Writers.TryGetValue(format, out var writer))
            return writer;

        throw new ArgumentOutOfRangeException(
            nameof(format),
            format,
            $"Format d'export virement non supporté : {format}.");
    }

    public static bool TryParseFormat(string? value, out PayrollBankTransferFormat format)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            format = PayrollBankTransferFormat.StandardCsv;
            return true;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized is "csv" or "standardcsv" or "standard_csv")
        {
            format = PayrollBankTransferFormat.StandardCsv;
            return true;
        }

        return Enum.TryParse(value, ignoreCase: true, out format)
               && Writers.ContainsKey(format);
    }
}
