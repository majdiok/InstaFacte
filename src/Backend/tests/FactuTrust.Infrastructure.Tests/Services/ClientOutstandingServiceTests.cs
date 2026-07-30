using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Services;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Encours client (lot 6). Décision produit actée : le plafond AVERTIT, il ne bloque jamais —
/// le service ne renvoie donc jamais d'échec pour dépassement, seulement un drapeau.
/// </summary>
public sealed class ClientOutstandingServiceTests
{
    private static readonly Guid ClientId = Guid.NewGuid();

    private readonly Mock<IClientRepository> _clients = new();
    private readonly Mock<IInvoiceRepository> _invoices = new();
    private readonly Mock<IPaymentRepository> _payments = new();
    private readonly Mock<ISalesOrderRepository> _orders = new();

    private ClientOutstandingService Build(
        Client client,
        IReadOnlyList<Invoice>? invoices = null,
        IDictionary<Guid, decimal>? paid = null,
        IReadOnlyList<SalesOrder>? orders = null)
    {
        _clients.Setup(r => r.GetByIdAsync(ClientId, It.IsAny<CancellationToken>())).ReturnsAsync(client);
        _invoices.Setup(r => r.GetByClientIdAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoices ?? Array.Empty<Invoice>());
        _payments.Setup(r => r.GetTotalPaidByInvoiceIdsAsync(
                It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<Guid, decimal>)(paid ?? new Dictionary<Guid, decimal>()));
        _orders.Setup(r => r.GetOpenOrdersByClientAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orders ?? Array.Empty<SalesOrder>());

        return new ClientOutstandingService(
            _clients.Object, _invoices.Object, _payments.Object, _orders.Object);
    }

    [Fact]
    public async Task WithoutAnythingDue_OutstandingIsZero()
    {
        var service = Build(NewClient());

        var result = await service.GetOutstandingAsync(ClientId);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Value.TotalOutstanding);
        Assert.False(result.Value.IsOverLimit);
    }

    [Fact]
    public async Task UnknownClient_Fails()
    {
        _clients.Setup(r => r.GetByIdAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Client?)null);
        var service = new ClientOutstandingService(
            _clients.Object, _invoices.Object, _payments.Object, _orders.Object);

        var result = await service.GetOutstandingAsync(ClientId);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ADraftInvoice_DoesNotCount()
    {
        // Un brouillon n'engage personne : le compter gonflerait l'encours à tort.
        var draft = NewInvoice(1000m, validate: false);
        var service = Build(NewClient(), new[] { draft });

        var result = await service.GetOutstandingAsync(ClientId);

        Assert.Equal(0m, result.Value.UnpaidInvoicesAmount);
        Assert.Equal(0, result.Value.UnpaidInvoiceCount);
    }

    [Fact]
    public async Task AValidatedInvoice_CountsForItsRemainingAmount()
    {
        var invoice = NewInvoice(1000m);
        var service = Build(
            NewClient(),
            new[] { invoice },
            new Dictionary<Guid, decimal> { [invoice.Id] = 400m });

        var result = await service.GetOutstandingAsync(ClientId);

        Assert.Equal(600m, result.Value.UnpaidInvoicesAmount);
        Assert.Equal(1, result.Value.UnpaidInvoiceCount);
    }

    [Fact]
    public async Task AFullyPaidInvoice_DropsOutOfTheOutstanding()
    {
        var invoice = NewInvoice(1000m);
        var service = Build(
            NewClient(),
            new[] { invoice },
            new Dictionary<Guid, decimal> { [invoice.Id] = 1000m });

        var result = await service.GetOutstandingAsync(ClientId);

        Assert.Equal(0m, result.Value.UnpaidInvoicesAmount);
        Assert.Equal(0, result.Value.UnpaidInvoiceCount);
    }

    /// <summary>
    /// Un trop-perçu ne doit pas venir en déduction des autres factures : cela relève du
    /// lettrage. Le neutraliser évite de minorer l'encours à tort.
    /// </summary>
    [Fact]
    public async Task AnOverpaidInvoice_DoesNotReduceTheOthers()
    {
        var overpaid = NewInvoice(100m);
        var open = NewInvoice(500m);

        var service = Build(
            NewClient(),
            new[] { overpaid, open },
            new Dictionary<Guid, decimal> { [overpaid.Id] = 300m });

        var result = await service.GetOutstandingAsync(ClientId);

        Assert.Equal(500m, result.Value.UnpaidInvoicesAmount);
    }

    [Fact]
    public async Task ACreditNote_IsNotCountedAsAReceivable()
    {
        var creditNote = NewInvoice(200m, type: InvoiceType.CreditNote);
        var service = Build(NewClient(), new[] { creditNote });

        var result = await service.GetOutstandingAsync(ClientId);

        Assert.Equal(0m, result.Value.UnpaidInvoicesAmount);
    }

    [Fact]
    public async Task OverLimit_IsFlaggedButNeverFails()
    {
        var invoice = NewInvoice(5000m);
        var service = Build(NewClient(creditLimit: 1000m), new[] { invoice });

        var result = await service.GetOutstandingAsync(ClientId);

        // Le point du lot : on signale, on ne refuse pas.
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsOverLimit);
        Assert.Equal(-4000m, result.Value.AvailableCredit);
    }

    [Fact]
    public async Task WithoutACreditLimit_TheClientIsNeverOverLimit()
    {
        var invoice = NewInvoice(999_999m);
        var service = Build(NewClient(creditLimit: null), new[] { invoice });

        var result = await service.GetOutstandingAsync(ClientId);

        Assert.False(result.Value.IsOverLimit);
        Assert.Null(result.Value.AvailableCredit);
    }

    [Fact]
    public async Task AnInvoiceOverdueByMoreThanThirtyDays_FeedsTheOverdueAmount()
    {
        var recent = NewInvoice(100m, dueDate: DateTime.UtcNow.Date.AddDays(-5));
        var old = NewInvoice(300m, dueDate: DateTime.UtcNow.Date.AddDays(-60));

        var service = Build(NewClient(), new[] { recent, old });

        var result = await service.GetOutstandingAsync(ClientId);

        // Seul l'ancien compte : c'est lui qui appelle une relance.
        Assert.Equal(300m, result.Value.OverdueAmount);
        Assert.Equal(400m, result.Value.UnpaidInvoicesAmount);
    }

    [Fact]
    public async Task UnpaidInvoicesAndOrders_AreReportedSeparatelyAndSummed()
    {
        var invoice = NewInvoice(1000m);
        var order = NewOrderPendingInvoice(600m);

        var service = Build(NewClient(), new[] { invoice }, orders: new[] { order });

        var result = await service.GetOutstandingAsync(ClientId);

        // Séparés à l'affichage — l'écran doit pouvoir dire d'où vient le dépassement.
        Assert.Equal(1000m, result.Value.UnpaidInvoicesAmount);
        Assert.Equal(600m, result.Value.ConfirmedOrdersAmount);
        Assert.Equal(1600m, result.Value.TotalOutstanding);
    }

    // ─────────────────────────── Montage ───────────────────────────

    private static Client NewClient(decimal? creditLimit = null)
    {
        var client = Client.Create(
            "Client test", ClientType.Business,
            Address.Create("Rue 1", "Tunis", "Tunis").Value,
            Email.Create("client@test.tn").Value,
            NIF.Create("1234567/A/B/C/000").Value).Value;

        if (creditLimit.HasValue)
            Assert.True(client.SetCreditTerms(creditLimit, null).IsSuccess);

        return client;
    }

    private static int _seq = 1;

    private static Invoice NewInvoice(
        decimal amountHt,
        bool validate = true,
        InvoiceType type = InvoiceType.Standard,
        DateTime? dueDate = null)
    {
        var prefix = type == InvoiceType.CreditNote ? "AVO" : "FAC";

        // L'échéance ne peut pas précéder l'émission : on recule la date d'émission avec elle,
        // sinon les cas « échu » seraient impossibles à construire.
        var issueDate = dueDate.HasValue && dueDate.Value < new DateTime(2026, 7, 20)
            ? dueDate.Value.AddDays(-30)
            : new DateTime(2026, 7, 20);

        var invoice = Invoice.Create(
            InvoiceNumber.Create(prefix, 2026, _seq++),
            NewClient(),
            issueDate,
            dueDate,
            type: type).Value;

        Assert.True(invoice.AddCustomLine(
            "Article", null, 1m, "Unité", Money.Create(amountHt), VatRate.Exempt).IsSuccess);

        if (validate)
            Assert.True(invoice.Validate().IsSuccess);

        return invoice;
    }

    private static SalesOrder NewOrderPendingInvoice(decimal amountHt)
    {
        var order = SalesOrder.Create(
            SalesOrderNumber.Create("CDE", 2026, _seq++),
            NewClient(),
            new DateTime(2026, 7, 20)).Value;

        var product = Product.Create(
            code: $"P-ENC{_seq}",
            name: "Produit encours",
            type: ProductType.Product,
            unitPrice: Money.Create(amountHt),
            vatRate: VatRate.Exempt,
            categoryId: Guid.NewGuid(),
            unit: "Unité").Value;

        Assert.True(order.AddLine(product, 1m).IsSuccess);
        return order;
    }
}
