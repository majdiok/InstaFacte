using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Comptabilisation FODEC sur factures de vente et avoirs (compte 43652).
/// </summary>
public sealed class InvoiceAccountingFodecTests
{
    private static ChartOfAccount Acc(string number, AccountNatureType nature = AccountNatureType.Debit) =>
        ChartOfAccount.Create(number, $"Compte {number}", int.Parse(number[..1].ToString()), null, nature).Value;

    private static ChartOfAccount Parent4365() =>
        ChartOfAccount.Create("4365", "État, impôts et taxes à payer", 4, "436", AccountNatureType.Credit, isSystem: true).Value;

    private static (AccountingService service, List<JournalEntry> captured, Mock<IChartOfAccountRepository> chart) BuildService(
        bool includeFodecAccount = true,
        List<ChartOfAccount>? autoCreatedAccounts = null)
    {
        var chart = new Mock<IChartOfAccountRepository>();
        chart.Setup(x => x.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(50);
        chart.Setup(x => x.GetByAccountNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string n, CancellationToken _) =>
            {
                if (n == AccountingService.FodecAccountNumber && !includeFodecAccount)
                    return null;

                return n switch
                {
                    "4365" => Parent4365(),
                    AccountingService.FodecAccountNumber => Acc(AccountingService.FodecAccountNumber, AccountNatureType.Credit),
                    "4371" => Acc("4371", AccountNatureType.Credit),
                    "436711" => Acc("436711", AccountNatureType.Credit),
                    "707" => Acc("707", AccountNatureType.Credit),
                    "4111" => Acc("4111", AccountNatureType.Debit),
                    _ => Acc(n)
                };
            });
        chart.Setup(x => x.AddAsync(It.IsAny<ChartOfAccount>(), It.IsAny<CancellationToken>()))
            .Callback<ChartOfAccount, CancellationToken>((a, _) => autoCreatedAccounts?.Add(a))
            .ReturnsAsync((ChartOfAccount a, CancellationToken _) => a);

        var periodService = new Mock<IAccountingPeriodService>();
        var period = AccountingPeriod.Create(2026, 7, new DateTime(2026, 7, 1), new DateTime(2026, 7, 31));
        periodService.Setup(x => x.EnsureOpenPeriodAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(period));

        var journals = new Mock<IJournalEntryRepository>();
        journals.Setup(x => x.GetBySourceAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JournalEntry?)null);
        journals.Setup(x => x.ReserveNextEntryNumberAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(13);
        var captured = new List<JournalEntry>();
        journals.Setup(x => x.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()))
            .Callback<JournalEntry, CancellationToken>((e, _) => captured.Add(e))
            .ReturnsAsync((JournalEntry e, CancellationToken _) => e);

        var withholding = new Mock<IWithholdingTaxRepository>();
        var ctxFactory = new Mock<ITenantDbContextFactory>();
        var settings = Options.Create(new AccountingSettings());

        var service = new AccountingService(
            chart.Object, periodService.Object, journals.Object, withholding.Object,
            ctxFactory.Object, NullLogger<AccountingService>.Instance, settings);

        return (service, captured, chart);
    }

    private static Invoice NewSaleInvoice(InvoiceType type = InvoiceType.Standard)
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;
        var prefix = type == InvoiceType.CreditNote ? "AVO" : "FAC";
        var number = InvoiceNumber.Create(prefix, 2026, 13);
        var result = Invoice.Create(number, client, new DateTime(2026, 7, 13), type: type);
        Assert.True(result.IsSuccess, result.Error?.Description);
        return result.Value;
    }

    private static void AddCustomLine(
        Invoice invoice,
        string designation,
        decimal qty,
        decimal unitHt,
        VatRate vatRate,
        bool fodecApplicable)
    {
        var price = Money.Create(unitHt, Money.DefaultCurrency);
        var add = invoice.AddCustomLine(
            designation,
            null,
            qty,
            "Unité",
            price,
            vatRate,
            isFodecApplicable: fodecApplicable,
            fodecRatePercent: 1m);
        Assert.True(add.IsSuccess, add.Error?.Description);
    }

    private static void SetStamp(Invoice invoice, decimal signedAmount)
    {
        var set = invoice.SetFiscalStampAmount(Money.FromSignedAmount(signedAmount, Money.DefaultCurrency));
        Assert.True(set.IsSuccess, set.Error?.Description);
    }

    private static (decimal debit, decimal credit) Totals(JournalEntry entry) =>
        (entry.Lines.Sum(l => l.DebitAmount.Amount), entry.Lines.Sum(l => l.CreditAmount.Amount));

    [Fact]
    public async Task GenerateInvoiceSaleEntry_WithFodec_PostsCredit43652AndBalances()
    {
        var invoice = NewSaleInvoice();
        AddCustomLine(invoice, "Television", 1m, 450m, VatRate.Standard, fodecApplicable: true);
        AddCustomLine(invoice, "bureau", 1m, 150m, VatRate.Intermediate, fodecApplicable: true);
        SetStamp(invoice, 1m);

        var (service, captured, _) = BuildService();
        var result = await service.GenerateInvoiceSaleEntryAsync(invoice, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(captured);
        var fodecLine = entry.Lines.Single(l => l.AccountNumber == AccountingService.FodecAccountNumber);
        Assert.Equal(0m, fodecLine.DebitAmount.Amount);
        Assert.Equal(6m, fodecLine.CreditAmount.Amount);

        var (debit, credit) = Totals(entry);
        Assert.Equal(invoice.TotalAmount.Amount, debit);
        Assert.Equal(debit, credit);
    }

    [Fact]
    public async Task GenerateInvoiceSaleEntry_WithoutFodec_DoesNotPost43652()
    {
        var invoice = NewSaleInvoice();
        AddCustomLine(invoice, "Service", 1m, 4600m, VatRate.Standard, fodecApplicable: false);
        SetStamp(invoice, 1m);

        var (service, captured, _) = BuildService();
        var result = await service.GenerateInvoiceSaleEntryAsync(invoice, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(captured);
        Assert.DoesNotContain(entry.Lines, l => l.AccountNumber == AccountingService.FodecAccountNumber);

        var (debit, credit) = Totals(entry);
        Assert.Equal(debit, credit);
    }

    [Fact]
    public async Task GenerateInvoiceCreditNoteEntry_WithFodec_PostsDebit43652AndBalances()
    {
        var invoice = NewSaleInvoice(InvoiceType.CreditNote);
        AddCustomLine(invoice, "Retour", 10m, 100m, VatRate.Standard, fodecApplicable: true);
        SetStamp(invoice, -1m);

        var (service, captured, _) = BuildService();
        var result = await service.GenerateInvoiceCreditNoteEntryAsync(invoice, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(captured);
        var fodecLine = entry.Lines.Single(l => l.AccountNumber == AccountingService.FodecAccountNumber);
        Assert.Equal(10m, fodecLine.DebitAmount.Amount);
        Assert.Equal(0m, fodecLine.CreditAmount.Amount);

        var (debit, credit) = Totals(entry);
        Assert.Equal(Math.Abs(invoice.TotalAmount.Amount), debit);
        Assert.Equal(debit, credit);
    }

    [Fact]
    public async Task GenerateInvoiceSaleEntry_PartialFodec_PostsOnlyEligibleAmount()
    {
        var invoice = NewSaleInvoice();
        AddCustomLine(invoice, "bureau", 1m, 450m, VatRate.Intermediate, fodecApplicable: true);
        AddCustomLine(invoice, "table", 1m, 150m, VatRate.Standard, fodecApplicable: false);
        SetStamp(invoice, 1m);

        var (service, captured, _) = BuildService();
        var result = await service.GenerateInvoiceSaleEntryAsync(invoice, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(captured);
        var fodecLine = entry.Lines.Single(l => l.AccountNumber == AccountingService.FodecAccountNumber);
        Assert.Equal(4.5m, fodecLine.CreditAmount.Amount);
    }

    [Fact]
    public async Task GenerateInvoiceSaleEntry_AutoCreates43652WhenMissing()
    {
        var autoCreated = new List<ChartOfAccount>();
        var invoice = NewSaleInvoice();
        AddCustomLine(invoice, "Article FODEC", 1m, 100m, VatRate.Standard, fodecApplicable: true);

        var (service, captured, chart) = BuildService(includeFodecAccount: false, autoCreatedAccounts: autoCreated);

        var result = await service.GenerateInvoiceSaleEntryAsync(invoice, CancellationToken.None);

        Assert.True(result.IsSuccess);
        chart.Verify(x => x.AddAsync(
            It.Is<ChartOfAccount>(a => a.AccountNumber == AccountingService.FodecAccountNumber),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(autoCreated, a => a.AccountNumber == AccountingService.FodecAccountNumber && a.ParentAccountNumber == "4365");

        var entry = Assert.Single(captured);
        Assert.Contains(entry.Lines, l => l.AccountNumber == AccountingService.FodecAccountNumber);
    }
}
