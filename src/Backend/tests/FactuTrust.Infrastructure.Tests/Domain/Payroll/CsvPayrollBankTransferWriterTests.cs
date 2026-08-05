using System.Text;
using FactuTrust.Domain.Banking;
using FactuTrust.Domain.Services.Payroll.BankTransfer;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

public sealed class CsvPayrollBankTransferWriterTests
{
    private static PayrollBankTransferBatch SampleBatch(string lastName = "Ben Ali") => new()
    {
        PayrollRunId = Guid.NewGuid(),
        Year = 2026,
        Month = 8,
        PeriodLabel = "Paie 08/2026",
        TransferLabel = "Paie 08/2026",
        ExportDateUtc = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc),
        Company = new BankTransferCompanyInfo("ACME SARL"),
        DebtorAccount = new BankTransferDebtorAccountInfo(
            "08009999888877776666",
            "TN5908009999888877776666",
            "BIAT",
            "BIAT"),
        Lines =
        [
            new PayrollBankTransferLine
            {
                EmployeeId = Guid.NewGuid(),
                PayslipId = Guid.NewGuid(),
                EmployeeNumber = "EMP001",
                LastName = lastName,
                FirstName = "Ahmed",
                Rib = "20001234567890123456",
                Iban = "TN5920001234567890123456",
                NetSalary = 1234.567m,
                TransferLabel = "Paie 08/2026",
                Cin = "12345678",
                CnssNumber = "1234567890"
            }
        ],
        ExcludedLines = Array.Empty<PayrollBankTransferExcludedLine>(),
        Warnings = Array.Empty<PayrollBankTransferWarning>(),
        EligibleCount = 1,
        TotalAmount = 1234.567m
    };

    [Fact]
    public void Write_HasUtf8Bom()
    {
        var bytes = new CsvPayrollBankTransferWriter().Write(SampleBatch());
        Assert.True(bytes.Length >= 3);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
    }

    [Fact]
    public void Write_UsesSemicolonSeparatorAndMetaHeader()
    {
        var bytes = new CsvPayrollBankTransferWriter().Write(SampleBatch());
        var text = Encoding.UTF8.GetString(bytes);
        // Strip BOM for assertions
        if (text.Length > 0 && text[0] == '\uFEFF')
            text = text[1..];

        Assert.Contains("# Société;ACME SARL", text);
        Assert.Contains("# Compte débiteur RIB;08009999888877776666", text);
        Assert.Contains("Type ligne;Matricule;Nom;Prénom;RIB;IBAN;Montant net;Libellé;CIN;CNSS;Réf saisie", text);
        Assert.Contains("Salarié;EMP001;Ben Ali;Ahmed;20001234567890123456;", text);
        Assert.Contains(";1234.567;Paie 08/2026;", text);
        Assert.Contains("TOTAL;;;1;;1234.567;;", text);
    }

    [Fact]
    public void Write_EscapesSemicolonInName()
    {
        var bytes = new CsvPayrollBankTransferWriter().Write(SampleBatch("Ben;Ali"));
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("\"Ben;Ali\"", text);
    }

    [Fact]
    public void Registry_ResolvesCsv()
    {
        Assert.True(PayrollBankTransferFormatRegistry.TryParseFormat("csv", out var format));
        Assert.Equal(PayrollBankTransferFormat.StandardCsv, format);
        var writer = PayrollBankTransferFormatRegistry.GetWriter(format);
        Assert.Equal("csv", writer.FileExtension);
        Assert.Contains("csv", writer.ContentType);
    }
}

public sealed class TunisianIbanTests
{
    [Fact]
    public void FromRib_ProducesValidLengthAndCheckDigits()
    {
        const string rib = "20001234567890123456";
        var iban = TunisianIban.FromRib(rib);
        Assert.NotNull(iban);
        Assert.Equal(24, iban!.Length);
        Assert.StartsWith("TN", iban);
        Assert.Equal(rib, iban[4..]);
    }

    [Fact]
    public void FromRib_InvalidLength_ReturnsNull()
    {
        Assert.Null(TunisianIban.FromRib("123"));
        Assert.Null(TunisianIban.FromRib(null));
    }

    [Fact]
    public void IsValidRibDigits_RequiresTwentyDigits()
    {
        Assert.True(TunisianIban.IsValidRibDigits("2000 1234 5678 9012 3456"));
        Assert.False(TunisianIban.IsValidRibDigits("123"));
        Assert.False(TunisianIban.IsValidRibDigits(""));
    }
}
