using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Projects;

public sealed class InvoiceProjectBillingCharacterizationTests
{
    [Fact]
    public void ClassicInvoiceCreate_LeavesProjectSourcesNull()
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;
        var invoice = Invoice.Create(InvoiceNumber.Create("FAC", 2026, 1), client, new DateTime(2026, 8, 1)).Value;

        Assert.Null(invoice.SourceProjectId);
        Assert.Null(invoice.SourceProjectBillingId);
        Assert.Null(invoice.SourceDeliveryNoteId);
        Assert.Equal(InvoiceType.Standard, invoice.Type);

        var price = Money.Create(100m, Money.DefaultCurrency);
        var add = invoice.AddCustomLine("Prestation", null, 1m, "Unité", price, VatRate.Standard);
        Assert.True(add.IsSuccess, add.Error.Description);
        Assert.Null(invoice.Lines.Single().ProductId);
        Assert.Equal("CUSTOM", invoice.Lines.Single().ProductCode);
        Assert.Null(invoice.SourceProjectId);
        Assert.Null(invoice.SourceDeliveryNoteId);
    }

    [Fact]
    public void CreateFromProjectBilling_DoesNotSetDeliveryNoteSource()
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;
        var projectId = Guid.NewGuid();
        var billingId = Guid.NewGuid();
        var invoice = Invoice.CreateFromProjectBilling(
            InvoiceNumber.Create("FAC", 2026, 2),
            client,
            new DateTime(2026, 8, 1),
            projectId,
            billingId).Value;

        Assert.Equal(projectId, invoice.SourceProjectId);
        Assert.Equal(billingId, invoice.SourceProjectBillingId);
        Assert.Null(invoice.SourceDeliveryNoteId);
    }
}
