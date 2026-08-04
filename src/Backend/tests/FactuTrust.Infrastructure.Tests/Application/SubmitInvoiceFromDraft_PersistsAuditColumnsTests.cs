using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Migrations.Tenant;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// Ensures invoice submit path persists AggregateRoot audit columns required by EF mappings.
/// </summary>
public sealed class SubmitInvoiceFromDraft_PersistsAuditColumnsTests
{
    [Fact]
    public void Invoice_AfterValidateAndSetAuditInfo_PersistsAuditColumns()
    {
        var invoice = CreateDraftInvoice();
        Assert.True(invoice.Validate().IsSuccess);

        const string userId = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";
        invoice.SetAuditInfo(userId);

        Assert.Equal(userId, invoice.CreatedBy);
        Assert.Null(invoice.UpdatedBy);
        Assert.Equal(1, invoice.Version);
    }

    [Fact]
    public void FixCoreDocumentAuditColumns_Migration_CoversCorePosTables()
    {
        var migrationSource = File.ReadAllText(
            Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..",
                "FactuTrust.Infrastructure",
                "Migrations",
                "Tenant",
                "20260803180000_FixCoreDocumentAuditColumns_Tenant.cs")));

        foreach (var table in new[] { "Invoices", "Clients", "InvoiceLines", "InvoiceDrafts" })
        {
            Assert.Contains($"\"{table}\"", migrationSource, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void FixCoreDocumentAuditColumns_Migration_IsRegistered()
    {
        var migrationAttribute = typeof(FixCoreDocumentAuditColumns_Tenant)
            .GetCustomAttributes(typeof(MigrationAttribute), inherit: false)
            .Cast<MigrationAttribute>()
            .Single();

        Assert.Equal("20260803180000_FixCoreDocumentAuditColumns_Tenant", migrationAttribute.Id);
    }

    private static Invoice CreateDraftInvoice()
    {
        var client = Client.Create(
            "Client POS",
            ClientType.Individual,
            Address.Create("1 rue Test", "Tunis", "Tunis", postalCode: "1000").Value,
            Email.Create("pos@test.local").Value).Value;

        var invoice = Invoice.Create(
            InvoiceNumber.Create("FAC", 2026, 42),
            client,
            DateTime.UtcNow.Date).Value;

        var product = Product.Create(
            "POS001",
            "Article POS",
            ProductType.Product,
            Money.Create(10m, "TND"),
            VatRate.Standard,
            Guid.NewGuid()).Value;

        Assert.True(invoice.AddLine(product, 1).IsSuccess);
        return invoice;
    }
}
