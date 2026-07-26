using System.Text;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Infrastructure.Services;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Exports du tableau d'amortissement d'emprunt (CSV / Excel / PDF). On vérifie que les trois
/// formats produisent un contenu non vide et exploitable, et que le CSV porte bien l'échéancier
/// et ses totaux.
/// </summary>
public sealed class LoanScheduleExportTests
{
    private static LoanScheduleDto Schedule()
    {
        var lines = new List<LoanScheduleLineDto>
        {
            new()
            {
                InstallmentNumber = 1, DueDate = new DateTime(2026, 2, 28),
                OpeningBalance = 10_000m, InterestAmount = 50m,
                PrincipalAmount = 5_000m, InstallmentAmount = 5_050m, ClosingBalance = 5_000m
            },
            new()
            {
                InstallmentNumber = 2, DueDate = new DateTime(2026, 3, 31),
                OpeningBalance = 5_000m, InterestAmount = 25m,
                PrincipalAmount = 5_000m, InstallmentAmount = 5_025m, ClosingBalance = 0m
            }
        };

        return new LoanScheduleDto
        {
            Loan = new LoanDto
            {
                Id = Guid.NewGuid(),
                LoanNumber = "EMP-2026-001",
                Label = "Crédit d'investissement",
                LenderName = "Banque de Tunisie",
                Principal = 10_000m,
                AnnualRatePercent = 6m,
                StartDate = new DateTime(2026, 1, 31),
                InstallmentCount = 2,
                LoanAccountNumber = "164",
                InterestAccountNumber = "651",
                BankAccountNumber = "532"
            },
            Lines = lines,
            TotalPrincipal = 10_000m,
            TotalInterest = 75m,
            TotalInstallments = 10_075m,
            IsSettled = true
        };
    }

    private static AccountingExportService Export() => new();

    [Fact]
    public void Csv_ContainsHeaderScheduleAndTotals()
    {
        var bytes = Export().ExportLoanScheduleToCsv(Schedule());
        var text = Encoding.UTF8.GetString(bytes);

        Assert.Contains("EMP-2026-001", text);
        Assert.Contains("Banque de Tunisie", text);
        Assert.Contains("Capital restant dû", text);
        Assert.Contains("TOTAUX", text);
        // Les deux échéances sont présentes.
        Assert.Contains("28/02/2026", text);
        Assert.Contains("31/03/2026", text);
    }

    [Fact]
    public void Excel_ProducesNonEmptyWorkbook()
    {
        var bytes = Export().ExportLoanScheduleToExcel(Schedule());
        Assert.NotEmpty(bytes);
        // Signature ZIP d'un .xlsx (PK).
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);
    }

    [Fact]
    public async Task Pdf_ProducesNonEmptyDocument()
    {
        var pdf = new PdfService(new Mock<IHttpClientFactory>().Object, new Mock<IDocumentTemplateRegistry>().Object);
        var header = new AccountingReportHeader("Société test", "1234567/A/B/C/000",
            "Tableau d'amortissement d'emprunt", "EMP-2026-001");

        var bytes = await pdf.GenerateLoanSchedulePdfAsync(Schedule(), header);

        Assert.NotEmpty(bytes);
        Assert.Equal(Encoding.ASCII.GetBytes("%PDF"), bytes.Take(4).ToArray());
    }
}
