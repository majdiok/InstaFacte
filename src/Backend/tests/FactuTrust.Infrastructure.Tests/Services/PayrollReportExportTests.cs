using System.Text;
using ClosedXML.Excel;
using FactuTrust.Application.Common.Enums;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Infrastructure.Services;
using FactuTrust.Infrastructure.Services.Templates;
using Microsoft.Extensions.Http;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>Exports des états de contrôle paie : CSV (BOM, totaux), Excel et PDF.</summary>
public sealed class PayrollReportExportTests
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    private static PdfService CreatePdfService()
    {
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        var registry = new DocumentTemplateRegistry(new IDocumentTemplate[] { new StandardDocumentTemplate() });
        return new PdfService(httpFactory.Object, registry);
    }

    private static AccountingReportHeader Header(string title, string period) =>
        new("Société Test", "1234567ABC", title, period);

    private static PayrollBookDto BuildBook(bool provisional = false) => new()
    {
        Year = 2026,
        FromMonth = 1,
        ToMonth = 3,
        PeriodLabel = "Janvier à Mars 2026",
        IncludedMonths = [1, 2, 3],
        MissingMonths = [],
        ProvisionalMonths = provisional ? [3] : [],
        IsProvisional = provisional,
        EmployeeCount = 2,
        TotalGross = 10500.500m,
        TotalCnssableGross = 10500.500m,
        TotalCnssEmployee = 963.946m,
        TotalProfessionalExpenses = 953.655m,
        TotalFamilyDeductions = 75m,
        TotalNetTaxable = 8507.899m,
        TotalIrpp = 1200.250m,
        TotalCss = 42.539m,
        TotalOtherDeductions = 150m,
        TotalNonTaxableAllowances = 0m,
        TotalNetSalary = 8143.765m,
        TotalCnssEmployer = 1739.932m,
        TotalWorkAccident = 42.002m,
        TotalTfp = 210.010m,
        TotalFoprolos = 105.005m,
        TotalCssEmployer = 52.503m,
        TotalEmployerCharges = 2149.452m,
        TotalEmployerCost = 12649.952m,
        Lines =
        [
            new PayrollBookLineDto
            {
                EmployeeId = Guid.NewGuid(),
                EmployeeNumber = "EMP001",
                EmployeeName = "Ahmed Ben Ali",
                Cin = "01234567",
                CnssNumber = "1234567890",
                Category = "Cadre",
                Echelon = "3",
                HireDate = new DateTime(2020, 1, 1),
                MonthsCount = 3,
                GrossSalary = 6000m,
                CnssableGross = 6000m,
                CnssEmployee = 550.800m,
                ProfessionalExpenses = 544.920m,
                FamilyDeductions = 75m,
                NetTaxable = 4829.280m,
                Irpp = 800.150m,
                Css = 24.146m,
                OtherDeductions = 150m,
                NonTaxableAllowances = 0m,
                NetSalary = 4474.904m
            },
            new PayrollBookLineDto
            {
                EmployeeId = Guid.NewGuid(),
                EmployeeNumber = "EMP002",
                EmployeeName = "Sonia Trabelsi",
                CnssNumber = "9876543210",
                MonthsCount = 3,
                GrossSalary = 4500.500m,
                CnssableGross = 4500.500m,
                CnssEmployee = 413.146m,
                ProfessionalExpenses = 408.735m,
                FamilyDeductions = 0m,
                NetTaxable = 3678.619m,
                Irpp = 400.100m,
                Css = 18.393m,
                OtherDeductions = 0m,
                NonTaxableAllowances = 0m,
                NetSalary = 3668.861m
            }
        ]
    };

    private static PayrollJournalDto BuildJournal(bool balanced = true, bool posted = true) => new()
    {
        PayrollRunId = Guid.NewGuid(),
        Year = 2026,
        Month = 3,
        PeriodLabel = "Mars 2026",
        Status = "Validated",
        StatusDisplay = "Validé",
        IsProvisional = false,
        EmployeeCount = 1,
        TotalGross = 2000m,
        TotalCnssableGross = 2000m,
        TotalCnssEmployee = 183.600m,
        TotalProfessionalExpenses = 181.640m,
        TotalFamilyDeductions = 0m,
        TotalNetTaxable = 1634.760m,
        TotalIrpp = 250m,
        TotalCss = 8.174m,
        TotalOtherDeductions = 0m,
        TotalNonTaxableAllowances = 0m,
        TotalNetSalary = 1558.226m,
        TotalCnssEmployer = 331.400m,
        TotalWorkAccident = 8m,
        TotalTfp = 40m,
        TotalFoprolos = 20m,
        TotalCssEmployer = 10m,
        TotalEmployerCharges = 409.400m,
        TotalEmployerCost = 2409.400m,
        TotalDebit = 2409.400m,
        TotalCredit = balanced ? 2409.400m : 2000m,
        IsBalanced = balanced,
        AccountingLinesArePosted = posted,
        AccountingEntryNumber = posted ? 42 : null,
        AccountingEntryDate = posted ? new DateTime(2026, 3, 31) : null,
        AccountingJournalCode = posted ? "JOD" : null,
        Lines =
        [
            new PayrollJournalEmployeeLineDto
            {
                PayslipId = Guid.NewGuid(),
                EmployeeId = Guid.NewGuid(),
                EmployeeNumber = "EMP001",
                EmployeeName = "Ahmed Ben Ali",
                CnssNumber = "1234567890",
                GrossSalary = 2000m,
                CnssableGross = 2000m,
                CnssEmployee = 183.600m,
                ProfessionalExpenses = 181.640m,
                MonthlyNetTaxable = 1634.760m,
                Irpp = 250m,
                Css = 8.174m,
                NetSalary = 1558.226m,
                CnssEmployer = 331.400m,
                WorkAccidentContribution = 8m,
                Tfp = 40m,
                Foprolos = 20m,
                CssEmployer = 10m,
                TotalEmployerCharges = 409.400m,
                TotalCost = 2409.400m
            }
        ],
        AccountingLines =
        [
            new PayrollJournalAccountingLineDto { AccountNumber = "640", AccountLabel = "Charges de personnel", Label = "Paie 03/2026", Debit = 2000m, Credit = 0m },
            new PayrollJournalAccountingLineDto { AccountNumber = "647", AccountLabel = "Charges sociales de l'employeur", Label = "Paie 03/2026", Debit = 409.400m, Credit = 0m },
            new PayrollJournalAccountingLineDto { AccountNumber = "425", AccountLabel = "Personnel — rémunérations dues", Label = "Paie 03/2026", Debit = 0m, Credit = 1558.226m },
            new PayrollJournalAccountingLineDto { AccountNumber = "432", AccountLabel = "État — retenues et taxes sur salaires", Label = "Paie 03/2026", Debit = 0m, Credit = 328.174m },
            new PayrollJournalAccountingLineDto { AccountNumber = "453", AccountLabel = "Organismes sociaux", Label = "Paie 03/2026", Debit = 0m, Credit = 523m }
        ]
    };

    private static string DecodeCsv(byte[] bytes)
    {
        Assert.True(bytes.Length > Utf8Bom.Length);
        Assert.Equal(Utf8Bom, bytes.Take(Utf8Bom.Length).ToArray());
        return Encoding.UTF8.GetString(bytes, Utf8Bom.Length, bytes.Length - Utf8Bom.Length);
    }

    // ── Livre de paie ──

    [Fact]
    public void BookCsv_HasHeaderRowsAndTotals()
    {
        var csv = DecodeCsv(new PayrollReportExportService().ExportPayrollBookToCsv(BuildBook()));
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToList();

        Assert.StartsWith("Matricule;Salarié;CIN;N° CNSS;", lines[0]);
        Assert.Contains(lines, l => l.StartsWith("EMP001;Ahmed Ben Ali;"));
        Assert.Contains(lines, l => l.StartsWith("TOTAL;"));

        // Montants en invariant à 3 décimales (millimes).
        Assert.Contains("10500.500", csv);
        Assert.Contains("8143.765", csv);

        // Bloc charges patronales en pied de fichier (CSS patronale incluse).
        Assert.Contains("CSS patronale;52.503", csv);
        Assert.Contains("Total charges patronales;2149.452", csv);
        Assert.Contains("Coût employeur;12649.952", csv);
    }

    [Fact]
    public void BookCsv_EscapesSeparatorInNames()
    {
        var book = BuildBook();
        var patched = book with
        {
            Lines = [book.Lines[0] with { EmployeeName = "Ben Ali; Ahmed" }, book.Lines[1]]
        };

        var csv = DecodeCsv(new PayrollReportExportService().ExportPayrollBookToCsv(patched));

        Assert.Contains("\"Ben Ali; Ahmed\"", csv);
    }

    [Fact]
    public void BookExcel_WritesTitleHeaderAndTotals()
    {
        var bytes = new PayrollReportExportService().ExportPayrollBookToExcel(BuildBook());

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheet("Livre de paie");

        Assert.Equal("Livre de paie simplifié", ws.Cell(1, 1).GetString());
        Assert.Equal("Janvier à Mars 2026", ws.Cell(2, 1).GetString());
        Assert.Equal("Matricule", ws.Cell(4, 1).GetString());
        Assert.Equal("Ahmed Ben Ali", ws.Cell(5, 2).GetString());

        var totalRow = 4 + 1 + 2; // en-tête + 2 lignes salariés
        Assert.Equal("TOTAL", ws.Cell(totalRow, 1).GetString());
        Assert.Equal(10500.500, ws.Cell(totalRow, 9).GetDouble(), 3);
    }

    [Fact]
    public void BookExcel_ShiftsRowsWhenProvisional()
    {
        var bytes = new PayrollReportExportService().ExportPayrollBookToExcel(BuildBook(provisional: true));

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheet("Livre de paie");

        Assert.Contains("PROVISOIRE", ws.Cell(3, 1).GetString());
        Assert.Equal("Matricule", ws.Cell(5, 1).GetString());
    }

    [Fact]
    public async Task BookPdf_ReturnsNonEmptyDocument()
    {
        var bytes = await CreatePdfService().GeneratePayrollBookPdfAsync(
            BuildBook(), Header("Livre de paie simplifié", "Janvier à Mars 2026"), CancellationToken.None);

        Assert.NotEmpty(bytes);
        Assert.Equal(0x25, bytes[0]); // '%' — magic PDF
        Assert.True(bytes.Length > 500);
    }

    [Fact]
    public async Task BookPdf_RendersEmptyPeriod()
    {
        var empty = BuildBook() with { Lines = [], EmployeeCount = 0 };

        var bytes = await CreatePdfService().GeneratePayrollBookPdfAsync(
            empty, Header("Livre de paie simplifié", "Janvier à Mars 2026"), CancellationToken.None);

        Assert.NotEmpty(bytes);
        Assert.Equal(0x25, bytes[0]);
    }

    // ── Journal de paie ──

    [Fact]
    public void JournalEmployeeCsv_HasHeaderAndTotals()
    {
        var csv = DecodeCsv(new PayrollReportExportService()
            .ExportPayrollJournalToCsv(BuildJournal(), PayrollJournalView.ByEmployee));

        Assert.StartsWith("Matricule;Salarié;N° CNSS;Brut;", csv);
        Assert.Contains("EMP001;Ahmed Ben Ali;", csv);
        Assert.Contains("TOTAL;;;2000.000;", csv);
        Assert.Contains("CSS patronale", csv);
        Assert.Contains("2409.400", csv);
    }

    [Fact]
    public void JournalAccountingCsv_ListsAccountsAndTotals()
    {
        var csv = DecodeCsv(new PayrollReportExportService()
            .ExportPayrollJournalToCsv(BuildJournal(), PayrollJournalView.Accounting));

        Assert.StartsWith("Compte;Libellé compte;Libellé écriture;Débit;Crédit", csv);
        Assert.Contains("640;Charges de personnel;Paie 03/2026;2000.000;0.000", csv);
        Assert.Contains("TOTAL;;;2409.400;2409.400", csv);
    }

    [Fact]
    public void JournalAccountingCsv_FlagsImbalance()
    {
        var csv = DecodeCsv(new PayrollReportExportService()
            .ExportPayrollJournalToCsv(BuildJournal(balanced: false), PayrollJournalView.Accounting));

        Assert.Contains("Contrôle;", csv);
    }

    [Fact]
    public void JournalExcel_UsesOneSheetPerView()
    {
        var service = new PayrollReportExportService();

        using var byEmployee = new XLWorkbook(new MemoryStream(
            service.ExportPayrollJournalToExcel(BuildJournal(), PayrollJournalView.ByEmployee)));
        using var accounting = new XLWorkbook(new MemoryStream(
            service.ExportPayrollJournalToExcel(BuildJournal(), PayrollJournalView.Accounting)));

        Assert.True(byEmployee.Worksheets.Contains("Journal de paie"));
        Assert.True(accounting.Worksheets.Contains("Ventilation comptable"));
        Assert.Contains("Écriture comptabilisée n° 42", accounting.Worksheet("Ventilation comptable").Cell(4, 1).GetString());
    }

    [Fact]
    public void JournalExcel_SignalsSimulatedSplit()
    {
        var bytes = new PayrollReportExportService()
            .ExportPayrollJournalToExcel(BuildJournal(posted: false), PayrollJournalView.Accounting);

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        Assert.Contains("simulée", wb.Worksheet("Ventilation comptable").Cell(4, 1).GetString());
    }

    [Theory]
    [InlineData(PayrollJournalView.ByEmployee)]
    [InlineData(PayrollJournalView.Accounting)]
    public async Task JournalPdf_ReturnsNonEmptyDocument(PayrollJournalView view)
    {
        var bytes = await CreatePdfService().GeneratePayrollJournalPdfAsync(
            BuildJournal(), view, Header("Journal de paie", "Mars 2026"), CancellationToken.None);

        Assert.NotEmpty(bytes);
        Assert.Equal(0x25, bytes[0]);
        Assert.True(bytes.Length > 500);
    }
}
