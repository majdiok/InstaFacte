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
/// Comptabilité des effets de commerce (traites) : réception (412/403 via JOD), encaissement/paiement
/// à échéance (532/412, 403/532 via JB), impayé (4111/412), idempotence, et non-régression des modes usuels.
/// </summary>
public sealed class EffetDeCommerceAccountingTests
{
    private static ChartOfAccount Acc(string number) =>
        ChartOfAccount.Create(number, $"Compte {number}", int.Parse(number[..1].ToString()), null, AccountNatureType.Debit).Value;

    private static (AccountingService service, Mock<IJournalEntryRepository> journals, List<JournalEntry> captured) BuildService(
        JournalEntry? existingBySource = null)
    {
        var chart = new Mock<IChartOfAccountRepository>();
        chart.Setup(x => x.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(50);
        chart.Setup(x => x.GetByAccountNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string n, CancellationToken _) => Acc(n));

        var periodService = new Mock<IAccountingPeriodService>();
        var period = AccountingPeriod.Create(2026, 4, new DateTime(2026, 4, 1), new DateTime(2026, 4, 30));
        periodService.Setup(x => x.EnsureOpenPeriodAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(period));

        var journals = new Mock<IJournalEntryRepository>();
        journals.Setup(x => x.GetBySourceAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingBySource);
        journals.Setup(x => x.ReserveNextEntryNumberAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);
        var captured = new List<JournalEntry>();
        journals.Setup(x => x.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()))
            .Callback<JournalEntry, CancellationToken>((e, _) => captured.Add(e))
            .ReturnsAsync((JournalEntry e, CancellationToken _) => e);

        var withholding = new Mock<IWithholdingTaxRepository>();
        var ctxFactory = new Mock<ITenantDbContextFactory>();
        var settings = Options.Create(new AccountingSettings { EffetDeCommerceEnabled = true });

        var service = new AccountingService(
            chart.Object, periodService.Object, journals.Object, withholding.Object,
            ctxFactory.Object, NullLogger<AccountingService>.Instance, settings);

        return (service, journals, captured);
    }

    private static Payment ClientPayment(PaymentMethod method, DateTime? effetDueDate = null)
    {
        var address = Address.Create("1 rue de la Republique", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;
        var invoice = Invoice.Create(
            InvoiceNumber.Create("FAC", 2026, 42),
            client,
            issueDate: new DateTime(2026, 4, 20),
            dueDate: new DateTime(2026, 4, 20)).Value;

        return Payment.Create(
            invoice,
            Money.Create(813.450m, Money.DefaultCurrency),
            paymentDate: new DateTime(2026, 4, 20),
            method: method,
            reference: "REF",
            effetDueDate: effetDueDate).Value;
    }

    private static SupplierPayment SupplierTraitePayment(PaymentMethod method, DateTime? effetDueDate = null)
    {
        var address = Address.Create("1 rue de test", "Tunis", "Tunis").Value;
        var email = Email.Create("supplier@test.com").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        var supplier = Supplier.Create("Ste test FF", SupplierType.Business, address, email, nif: nif).Value;
        var category = ProductCategory.Create("GEN", "Général").Value;
        var unitPrice = Money.Create(100m, Money.DefaultCurrency);
        var product = Product.Create("PR-FF-1", "Article test", ProductType.Product, unitPrice, VatRate.Standard, category.Id, purchasePrice: unitPrice).Value;

        var po = PurchaseOrder.Create(PurchaseOrderNumber.Create("BC", 2026, 500001), supplier, new DateTime(2026, 4, 1)).Value;
        po.AddLine(product, 2m);
        po.Confirm();
        po.ReceiveGoods(new[] { (po.Lines.First().Id, 2m) });

        var inv = SupplierInvoice.CreateFromPurchaseOrder(po, "FS-2026-TEST-FF", new DateTime(2026, 4, 5)).Value;
        return SupplierPayment.Create(inv, inv.TotalAmount, new DateTime(2026, 4, 10), method, reference: "REF-FF", effetDueDate: effetDueDate).Value;
    }

    // ─────────────── Réception (1er volet) ───────────────

    [Fact]
    public async Task ClientTraite_Reception_DebitsEffetAccount412ViaJod()
    {
        var (service, _, captured) = BuildService();
        var payment = ClientPayment(PaymentMethod.Traite, new DateTime(2026, 6, 20));

        var result = await service.GenerateClientPaymentEntryAsync(payment, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(captured);
        Assert.Equal("JOD", entry.JournalCode);
        var debit = entry.Lines.Single(l => l.DebitAmount.Amount > 0);
        var credit = entry.Lines.Single(l => l.CreditAmount.Amount > 0);
        Assert.Equal("412", debit.AccountNumber);
        Assert.Equal(813.450m, debit.DebitAmount.Amount);
        Assert.Equal("4111", credit.AccountNumber);
    }

    [Fact]
    public async Task ClientBankTransfer_StillPostsToTreasury5321ViaJb()
    {
        var (service, _, captured) = BuildService();
        var payment = ClientPayment(PaymentMethod.BankTransfer);

        var result = await service.GenerateClientPaymentEntryAsync(payment, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(captured);
        Assert.Equal("JB", entry.JournalCode);
        Assert.Equal("5321", entry.Lines.Single(l => l.DebitAmount.Amount > 0).AccountNumber);
    }

    [Fact]
    public async Task SupplierTraite_Reception_CreditsEffetAccount403ViaJod()
    {
        var (service, _, captured) = BuildService();
        var payment = SupplierTraitePayment(PaymentMethod.Traite, new DateTime(2026, 6, 20));

        var result = await service.GenerateSupplierPaymentEntryAsync(payment, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(captured);
        Assert.Equal("JOD", entry.JournalCode);
        Assert.Equal("4011", entry.Lines.Single(l => l.DebitAmount.Amount > 0).AccountNumber);
        Assert.Equal("403", entry.Lines.Single(l => l.CreditAmount.Amount > 0).AccountNumber);
    }

    // ─────────────── Règlement à échéance (2ᵉ volet) ───────────────

    [Fact]
    public async Task ClientEffet_Encaisse_DebitsBank532CreditsEffet412ViaJb()
    {
        var (service, _, captured) = BuildService();
        var payment = ClientPayment(PaymentMethod.Traite, new DateTime(2026, 6, 20));
        payment.MarkEffetSettled(new DateTime(2026, 6, 20), EffetStatus.Encaisse);

        var result = await service.GenerateClientEffetSettlementEntryAsync(payment, EffetStatus.Encaisse, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(captured);
        Assert.Equal("JB", entry.JournalCode);
        Assert.Equal("5321", entry.Lines.Single(l => l.DebitAmount.Amount > 0).AccountNumber);
        Assert.Equal("412", entry.Lines.Single(l => l.CreditAmount.Amount > 0).AccountNumber);
    }

    [Fact]
    public async Task ClientEffet_Impaye_ReopensReceivable4111ViaJod()
    {
        var (service, _, captured) = BuildService();
        var payment = ClientPayment(PaymentMethod.Traite, new DateTime(2026, 6, 20));
        payment.MarkEffetSettled(new DateTime(2026, 6, 20), EffetStatus.Impaye);

        var result = await service.GenerateClientEffetSettlementEntryAsync(payment, EffetStatus.Impaye, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(captured);
        Assert.Equal("JOD", entry.JournalCode);
        Assert.Equal("4111", entry.Lines.Single(l => l.DebitAmount.Amount > 0).AccountNumber);
        Assert.Equal("412", entry.Lines.Single(l => l.CreditAmount.Amount > 0).AccountNumber);
    }

    [Fact]
    public async Task SupplierEffet_Paid_Debits403CreditsBank532ViaJb()
    {
        var (service, _, captured) = BuildService();
        var payment = SupplierTraitePayment(PaymentMethod.Traite, new DateTime(2026, 6, 20));
        payment.MarkEffetSettled(new DateTime(2026, 6, 20), EffetStatus.Encaisse);

        var result = await service.GenerateSupplierEffetSettlementEntryAsync(payment, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(captured);
        Assert.Equal("JB", entry.JournalCode);
        Assert.Equal("403", entry.Lines.Single(l => l.DebitAmount.Amount > 0).AccountNumber);
        Assert.Equal("5321", entry.Lines.Single(l => l.CreditAmount.Amount > 0).AccountNumber);
    }

    // ─────────────── Idempotence ───────────────

    [Fact]
    public async Task ClientEffetSettlement_WhenAlreadyPosted_DoesNotPostAgain()
    {
        var existing = JournalEntry.Create(1, "JB", new DateTime(2026, 4, 20), "déjà", Guid.NewGuid(), false,
            AccountingService.SourceEffetSettlement, Guid.NewGuid(),
            new[]
            {
                new JournalLineInput("5321", "x", 10m, 0, null, ThirdPartyKind.None),
                new JournalLineInput("412", "y", 0, 10m, null, ThirdPartyKind.None)
            }, Money.DefaultCurrency).Value;

        var (service, journals, _) = BuildService(existingBySource: existing);
        var payment = ClientPayment(PaymentMethod.Traite, new DateTime(2026, 6, 20));
        payment.MarkEffetSettled(new DateTime(2026, 6, 20), EffetStatus.Encaisse);

        var result = await service.GenerateClientEffetSettlementEntryAsync(payment, EffetStatus.Encaisse, CancellationToken.None);

        Assert.True(result.IsSuccess);
        journals.Verify(x => x.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
