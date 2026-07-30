using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Reports.Queries;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// Freezes the commercial client balance formula:
/// Balance = Σ TotalAmount (non-cancelled) − Σ applied payments (from repository).
/// </summary>
public sealed class GetClientBalancesReportQueryHandlerTests
{
    [Fact]
    public async Task PaidInvoice_YieldsZeroBalance()
    {
        var invoice = NewInvoice("Client A", totalHt: 1000m); // 1190 TTC
        var paid = new Dictionary<Guid, decimal> { [invoice.Id] = 1190m };

        var result = await HandleAsync([invoice], paid);

        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value);
        Assert.Equal(1190m, row.TotalInvoiced);
        Assert.Equal(1190m, row.TotalPaid);
        Assert.Equal(0m, row.Balance);
        Assert.Equal("TND", row.Currency);
    }

    [Fact]
    public async Task PartialPayment_YieldsRemainingBalance()
    {
        var invoice = NewInvoice("Client A", totalHt: 1000m);
        var paid = new Dictionary<Guid, decimal> { [invoice.Id] = 500m };

        var result = await HandleAsync([invoice], paid);

        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value);
        Assert.Equal(1190m, row.TotalInvoiced);
        Assert.Equal(500m, row.TotalPaid);
        Assert.Equal(690m, row.Balance);
    }

    [Fact]
    public async Task CancelledInvoice_IsExcluded()
    {
        var active = NewInvoice("Client A", totalHt: 1000m);
        var cancelled = NewInvoice("Client A", totalHt: 500m, sequence: 2);
        Assert.True(cancelled.Cancel("Annulation test").IsSuccess);

        var paid = new Dictionary<Guid, decimal>
        {
            [active.Id] = 0m,
            [cancelled.Id] = 0m
        };

        var result = await HandleAsync([active, cancelled], paid);

        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value);
        Assert.Equal(1190m, row.TotalInvoiced);
        Assert.Equal(0m, row.TotalPaid);
        Assert.Equal(1190m, row.Balance);
    }

    [Fact]
    public async Task WithholdingIncludedInPaid_ReducesBalance()
    {
        // Repository already returns Amount + ClientWithholdingAmount; handler must use that total.
        var invoice = NewInvoice("Client A", totalHt: 1000m);
        var paid = new Dictionary<Guid, decimal> { [invoice.Id] = 1175m + 15m }; // net + withholding = 1190

        var result = await HandleAsync([invoice], paid);

        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value);
        Assert.Equal(0m, row.Balance);
        Assert.Equal(1190m, row.TotalPaid);
    }

    [Fact]
    public async Task RefundedPaymentExcluded_LeavesFullBalance()
    {
        // Refunded payments are excluded by PaymentRepository; handler sees no paid amount.
        var invoice = NewInvoice("Client A", totalHt: 1000m);
        var paid = new Dictionary<Guid, decimal>(); // empty = refunded excluded

        var result = await HandleAsync([invoice], paid);

        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value);
        Assert.Equal(0m, row.TotalPaid);
        Assert.Equal(1190m, row.Balance);
    }

    [Fact]
    public async Task MultipleInvoices_AggregatePerClientAndCurrency()
    {
        var clientA1 = NewInvoice("Client A", totalHt: 1000m, sequence: 1);
        var clientA2 = NewInvoice("Client A", totalHt: 500m, sequence: 2, client: clientA1.Client);
        var clientB = NewInvoice("Client B", totalHt: 200m, sequence: 3);

        var paid = new Dictionary<Guid, decimal>
        {
            [clientA1.Id] = 200m,
            [clientA2.Id] = 100m,
            [clientB.Id] = 0m
        };

        var result = await HandleAsync([clientA1, clientA2, clientB], paid);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);

        var rowA = result.Value.Single(r => r.ClientName == "Client A");
        Assert.Equal(1190m + 595m, rowA.TotalInvoiced);
        Assert.Equal(300m, rowA.TotalPaid);
        Assert.Equal(1190m + 595m - 300m, rowA.Balance);

        var rowB = result.Value.Single(r => r.ClientName == "Client B");
        Assert.Equal(238m, rowB.TotalInvoiced); // 200 + 19%
        Assert.Equal(0m, rowB.TotalPaid);
        Assert.Equal(238m, rowB.Balance);
    }

    [Fact]
    public async Task CreditNote_ReducesTotalInvoiced()
    {
        var invoice = NewInvoice("Client A", totalHt: 1000m, sequence: 1);
        var creditNote = NewInvoice("Client A", totalHt: 200m, sequence: 2, type: InvoiceType.CreditNote, client: invoice.Client);
        Assert.True(creditNote.TotalAmount.Amount < 0);

        var paid = new Dictionary<Guid, decimal>
        {
            [invoice.Id] = 0m,
            [creditNote.Id] = 0m
        };

        var result = await HandleAsync([invoice, creditNote], paid);

        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value);
        Assert.Equal(1190m - 238m, row.TotalInvoiced);
        Assert.Equal(1190m - 238m, row.Balance);
    }

    [Fact]
    public async Task EmptyInvoices_ReturnsEmptyList()
    {
        var result = await HandleAsync([], new Dictionary<Guid, decimal>());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    private static async Task<Result<IReadOnlyList<ClientBalanceReportRowDto>>> HandleAsync(
        IReadOnlyList<Invoice> invoices,
        IReadOnlyDictionary<Guid, decimal> paidByInvoice)
    {
        var invoiceRepo = new Mock<IInvoiceRepository>();
        invoiceRepo
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoices);

        var paymentRepo = new Mock<IPaymentRepository>();
        paymentRepo
            .Setup(x => x.GetTotalPaidByInvoiceIdsAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
            {
                var dict = new Dictionary<Guid, decimal>();
                foreach (var id in ids)
                {
                    if (paidByInvoice.TryGetValue(id, out var paid))
                        dict[id] = paid;
                }
                return dict;
            });

        var handler = new GetClientBalancesReportQueryHandler(invoiceRepo.Object, paymentRepo.Object);
        return await handler.Handle(new GetClientBalancesReportQuery(), CancellationToken.None);
    }

    private static Invoice NewInvoice(
        string clientName,
        decimal totalHt,
        int sequence = 1,
        InvoiceType type = InvoiceType.Standard,
        Client? client = null)
    {
        client ??= CreateClient(clientName);
        var prefix = type == InvoiceType.CreditNote ? "AVO" : "FAC";
        var invoice = Invoice.Create(
            InvoiceNumber.Create(prefix, 2026, sequence),
            client,
            new DateTime(2026, 7, 20),
            type: type).Value;

        Assert.True(invoice.AddCustomLine(
            "Article", null, 1m, "Unité", Money.Create(totalHt), VatRate.Standard).IsSuccess);

        return invoice;
    }

    private static Client CreateClient(string name)
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create($"{name.Replace(" ", "").ToLowerInvariant()}@example.com").Value;
        return Client.Create(name, ClientType.Individual, address, email).Value;
    }
}
