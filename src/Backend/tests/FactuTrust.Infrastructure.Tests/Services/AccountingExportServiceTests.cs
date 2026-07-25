using System.Text;
using ClosedXML.Excel;
using FactuTrust.Application.DTOs;
using FactuTrust.Infrastructure.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class AccountingExportServiceTests
{
    private static readonly AccountingExportService Svc = new();

    private static void AssertIsXlsx(byte[] bytes)
    {
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
        // Un .xlsx est une archive ZIP : signature « PK ».
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    private static string CsvText(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    [Fact]
    public void ExportBalanceToCsv_FormatsAmountsInMillimes()
    {
        var rows = new List<BalanceRowDto>
        {
            new() { AccountNumber = "707000", Label = "Ventes", MovementCredit = 1000m, ClosingCredit = 1000m }
        };

        var csv = CsvText(Svc.ExportBalanceToCsv(rows));

        // Correctif : le dinar est en millimes (3 décimales), pas 2.
        Assert.Contains("1000.000", csv);
        Assert.DoesNotContain("1000.00;", csv);
    }

    [Fact]
    public void ExportJournalToExcel_ProducesReadableWorkbook()
    {
        var entries = new List<JournalEntryDto>
        {
            new()
            {
                EntryNumber = 1, JournalCode = "VTE", EntryDate = new DateTime(2025, 1, 2), Label = "Vente",
                Lines = new List<JournalEntryLineDto>
                {
                    new() { AccountNumber = "411000", Label = "Client X", Debit = 1190.500m },
                    new() { AccountNumber = "707000", Label = "Ventes", Credit = 1000.000m }
                }
            }
        };

        var bytes = Svc.ExportJournalToExcel(entries);
        AssertIsXlsx(bytes);

        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet(1);
        Assert.Equal("Date", ws.Cell(1, 1).GetString());
        Assert.Equal("411000", ws.Cell(2, 4).GetString());
    }

    [Fact]
    public void ExportAuxiliaryBalanceToExcel_ProducesWorkbookWithTotals()
    {
        var rows = new List<AuxiliaryBalanceRowDto>
        {
            new() { ThirdPartyName = "Client X", MovementDebit = 1190.500m, ClosingDebit = 1190.500m }
        };

        var bytes = Svc.ExportAuxiliaryBalanceToExcel(rows);
        AssertIsXlsx(bytes);

        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet(1);
        Assert.Equal("Tiers", ws.Cell(1, 1).GetString());
        Assert.Equal("TOTAUX", ws.Cell(3, 1).GetString());
    }

    [Fact]
    public void ExportAgingToExcel_ProducesWorkbook()
    {
        var rows = new List<AgingReportRowDto>
        {
            new() { ThirdPartyName = "Client X", Total = 1500m, NotYetDue = 500m, Days0To30 = 1000m }
        };
        AssertIsXlsx(Svc.ExportAgingToExcel(rows, "Clients"));
    }

    [Fact]
    public void ExportThirdPartyLedgerToExcel_ProducesWorkbook()
    {
        var ledger = new ThirdPartyLedgerDto
        {
            ThirdPartyName = "Client X",
            OpeningBalance = 100m,
            Rows = new List<ThirdPartyLedgerRowDto>
            {
                new() { EntryDate = new DateTime(2025, 1, 2), JournalCode = "VTE", PieceNumber = 1, AccountNumber = "411000", Label = "Facture", Debit = 1190.500m, RunningBalance = 1290.500m }
            }
        };
        AssertIsXlsx(Svc.ExportThirdPartyLedgerToExcel(ledger));
    }

    [Fact]
    public void ExportFiscalResult_ProducesCsvInMillimes_AndReadableWorkbook()
    {
        var dto = new FiscalResultDeclarationDto
        {
            FiscalYear = 2025,
            TaxpayerKind = 0,
            AccountingResult = 100000m,
            Adjustments = new List<FiscalAdjustmentLineDto>
            {
                new() { Kind = 0, Label = "Impôt sur les sociétés", Amount = 15000m }
            },
            Computation = new IncomeTaxComputationDto
            {
                AccountingResult = 100000m, TotalReintegrations = 15000m, TaxableResult = 115000m,
                TaxOnResult = 17250m, MinimumTax = 1000m, TaxDue = 17250m, TotalTaxDue = 17250m, NetToPay = 17250m
            }
        };

        var csv = CsvText(Svc.ExportFiscalResultToCsv(dto));
        Assert.Contains("17250.000", csv);
        Assert.Contains("RÉINTÉGRATIONS", csv);

        AssertIsXlsx(Svc.ExportFiscalResultToExcel(dto));
    }

    [Fact]
    public void ExportConsolidatedLiasse_ProducesMultiSheetWorkbook()
    {
        var current = new Dictionary<string, decimal>
        {
            ["221"] = 10000m, ["101"] = -15000m, ["70"] = -20000m, ["601"] = 12000m
        };
        var liasse = FactuTrust.Infrastructure.Services.NctStatementBuilder.Build(2025, current, new Dictionary<string, decimal>(), enabled: true);

        var dto = new ConsolidatedLiasseDto
        {
            FiscalYear = 2025,
            FinancialStatements = liasse,
            FiscalResult = new FiscalResultDeclarationDto { FiscalYear = 2025, Computation = new IncomeTaxComputationDto() },
            AmortizationTable = new List<FiscalTableRowDto> { new() { Code = "IMM-1", Label = "Matériel", Amount = 1000m } },
            ProvisionsTable = new List<FiscalTableRowDto> { new() { Code = "15", Label = "Provisions", Amount = 3000m, PreviousAmount = 2000m } }
        };

        var bytes = Svc.ExportConsolidatedLiasseToExcel(dto);
        AssertIsXlsx(bytes);

        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        Assert.True(wb.Worksheets.Count >= 4);
    }

    [Fact]
    public void ExportBalanceSheetAndIncomeStatement_ProduceWorkbooks()
    {
        var bs = new BalanceSheetDto
        {
            Assets = new List<FinancialStatementLineDto> { new() { AccountNumber = "22", Label = "Immo", Amount = 30000m } },
            Liabilities = new List<FinancialStatementLineDto> { new() { AccountNumber = "10", Label = "Capital", Amount = 30000m } },
            TotalAssets = 30000m, TotalLiabilities = 30000m
        };
        AssertIsXlsx(Svc.ExportBalanceSheetToExcel(bs));

        var isr = new IncomeStatementDto
        {
            Revenue = new List<FinancialStatementLineDto> { new() { AccountNumber = "70", Label = "Ventes", Amount = 20000m } },
            Expenses = new List<FinancialStatementLineDto> { new() { AccountNumber = "60", Label = "Achats", Amount = 12000m } },
            TotalRevenue = 20000m, TotalExpenses = 12000m, NetResult = 8000m
        };
        AssertIsXlsx(Svc.ExportIncomeStatementToExcel(isr));
    }
}
