using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Audit;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.AccountingAudit;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.AccountingAudit;
using FactuTrust.Infrastructure.Services.AccountingAudit.Rules;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AccountingAudit;

/// <summary>
/// Famille « Fiscale » du réviseur : retenue à la source, FODEC, verrouillage des périodes de TVA.
///
/// <para>Chaque règle est aussi éprouvée sur son cas d'<b>abstention</b> : sans paramètre d'exercice,
/// elle doit se taire plutôt que retenir un seuil deviné. Un seuil faux produirait des anomalies
/// erronées sur un exercice entier.</para>
/// </summary>
public sealed class FiscalFamilyRulesTests
{
    private sealed class TestTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly string _databaseName;
        public TestTenantDbContextFactory(string databaseName) => _databaseName = databaseName;

        public TenantDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(_databaseName)
                .Options;
            return new TenantDbContext(options);
        }
    }

    private const int Year = 2026;
    private const decimal Threshold = 1000m;
    private static int _sequence = 1;

    private static TestTenantDbContextFactory NewFactory(string label) => new($"{label}_{Guid.NewGuid()}");

    private static async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        TestTenantDbContextFactory factory,
        IAccountingAuditRule rule,
        params AccountingControlRuleSetting[] settings)
    {
        await using var db = factory.CreateContext();
        var ctx = new AuditEvaluationContextImpl(
            db, new AccountingSettings(), Year,
            new DateOnly(Year, 1, 1), new DateOnly(Year, 12, 31),
            settings, moduleCodesFilter: null);
        return await rule.EvaluateAsync(ctx, CancellationToken.None);
    }

    // ── Retenue à la source ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Monte un cycle d'achat complet chez un fournisseur d'honoraires.
    /// <paramref name="withThreshold"/> à faux omet le paramètre d'exercice, pour éprouver
    /// l'abstention.
    /// </summary>
    private static async Task SeedFeesInvoiceAsync(
        TestTenantDbContextFactory factory,
        decimal invoiceAmount,
        bool withholdingApplied,
        bool withThreshold = true)
    {
        await using var db = factory.CreateContext();

        var taxType = WithholdingTaxType.Create(
            "RS-HON", WithholdingCategory.Honoraires, "Honoraires", 10m).Value;
        db.WithholdingTaxTypes.Add(taxType);

        if (withThreshold)
            db.WithholdingFiscalYearParameters.Add(
                WithholdingFiscalYearParameter.Create(Year, Threshold));

        var supplier = Supplier.Create(
            "Cabinet conseil", SupplierType.Business,
            Address.Create("Rue 1", "Tunis", "Tunis").Value,
            Email.Create($"f{_sequence++}@test.tn").Value,
            nif: NIF.Create("1234567/A/B/C/000").Value).Value;
        supplier.SetWithholdingDefaults(taxType.Id, 10m, isSubjectToWithholding: true);
        db.Suppliers.Add(supplier);

        var category = ProductCategory.Create("SRV", "Services").Value;
        // Chaque propriété owned reçoit SA PROPRE instance de Money.
        var product = Product.Create(
            $"PR-{_sequence++}", "Prestation", ProductType.Service,
            Money.Create(invoiceAmount), VatRate.Standard, category.Id,
            purchasePrice: Money.Create(invoiceAmount)).Value;
        db.ProductCategories.Add(category);
        db.Products.Add(product);

        var order = PurchaseOrder.Create(
            PurchaseOrderNumber.Create("BC", Year, _sequence++), supplier, new DateTime(Year, 2, 1)).Value;
        Assert.True(order.AddLine(product, 1m).IsSuccess);
        Assert.True(order.Confirm().IsSuccess);
        Assert.True(order.ReceiveGoods([(order.Lines.First().Id, 1m)]).IsSuccess);
        db.PurchaseOrders.Add(order);

        var selections = order.Lines
            .Where(l => l.ReceivedNotInvoicedQuantity > 0)
            .Select(l => (l.Id, l.ReceivedNotInvoicedQuantity))
            .ToList();

        var invoice = SupplierInvoice.CreateFromPurchaseOrder(
            order, "FH-001", new DateTime(Year, 3, 1), selections).Value;

        if (withholdingApplied)
        {
            invoice.SetWithholdingInfo(
                isSubjectToWithholding: true,
                withholdingRate: 10m,
                withholdingAmount: Math.Round(invoice.TotalAmount.Amount * 0.10m, 3),
                withholdingTaxTypeId: taxType.Id);
        }

        invoice.SetAuditInfo("test", false);
        db.SupplierInvoices.Add(invoice);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Withholding_missing_flags_fees_above_the_threshold()
    {
        var factory = NewFactory("RsMissing");
        await SeedFeesInvoiceAsync(factory, invoiceAmount: 5000m, withholdingApplied: false);

        var anomaly = Assert.Single(await EvaluateAsync(factory, new WithholdingMissingOnFeesAuditRule()));
        Assert.Equal((int)PreClosingSeverity.Blocking, anomaly.Severity);
    }

    [Fact]
    public async Task Withholding_missing_is_silent_when_the_withholding_is_applied()
    {
        var factory = NewFactory("RsApplied");
        await SeedFeesInvoiceAsync(factory, invoiceAmount: 5000m, withholdingApplied: true);

        Assert.Empty(await EvaluateAsync(factory, new WithholdingMissingOnFeesAuditRule()));
    }

    [Fact]
    public async Task Withholding_missing_ignores_invoices_below_the_threshold()
    {
        var factory = NewFactory("RsBelow");
        await SeedFeesInvoiceAsync(factory, invoiceAmount: 100m, withholdingApplied: false);

        Assert.Empty(await EvaluateAsync(factory, new WithholdingMissingOnFeesAuditRule()));
    }

    /// <summary>
    /// Sans paramètre d'exercice, aucun seuil de référence : retenir une valeur devinée produirait
    /// des anomalies fausses sur tout l'exercice.
    /// </summary>
    [Fact]
    public async Task Withholding_missing_abstains_without_a_fiscal_year_threshold()
    {
        var factory = NewFactory("RsNoThreshold");
        await SeedFeesInvoiceAsync(factory, invoiceAmount: 5000m, withholdingApplied: false, withThreshold: false);

        Assert.Empty(await EvaluateAsync(factory, new WithholdingMissingOnFeesAuditRule()));
    }

    // ── FODEC ─────────────────────────────────────────────────────────────────────────────

    private static async Task SeedFodecInvoiceAsync(
        TestTenantDbContextFactory factory, bool bookFodec)
    {
        await using var db = factory.CreateContext();

        var client = Client.Create(
            "Client test", ClientType.Business,
            Address.Create("Rue 1", "Tunis", "Tunis").Value,
            Email.Create($"c{_sequence++}@test.tn").Value,
            NIF.Create("1234567/A/B/C/000").Value).Value;

        var invoice = Invoice.Create(
            InvoiceNumber.Create("FAC", Year, _sequence++), client, new DateTime(Year, 5, 1)).Value;
        Assert.True(invoice.AddCustomLine(
            "Article", null, 1m, "Unité", Money.Create(1000m), VatRate.Standard,
            isFodecApplicable: true).IsSuccess);
        Assert.True(invoice.Validate().IsSuccess);
        invoice.SetAuditInfo("test", false);

        db.Clients.Add(client);
        db.Invoices.Add(invoice);

        var fodec = invoice.FodecAmount.Amount;
        var ht = invoice.SubTotal.Amount;
        var vat = invoice.TotalVat.Amount;

        var lines = new List<JournalLineInput>
        {
            new("4111", "Client", ht + fodec + vat, 0m, null, ThirdPartyKind.None),
            new("707", "Vente", 0m, ht, null, ThirdPartyKind.None),
            new("436711", "TVA", 0m, vat, null, ThirdPartyKind.None)
        };

        // Le FODEC est soit crédité au 4477, soit — c'est l'anomalie — noyé dans la vente.
        if (bookFodec)
            lines.Add(new JournalLineInput("4477", "FODEC", 0m, fodec, null, ThirdPartyKind.None));
        else
            lines.Add(new JournalLineInput("707", "Vente (FODEC omis)", 0m, fodec, null, ThirdPartyKind.None));

        var entry = JournalEntry.Create(
            1, "JV", new DateTime(Year, 5, 1), "Vente", Guid.NewGuid(),
            isAutoGenerated: true,
            sourceEntityType: FactuTrust.Infrastructure.Services.AccountingService.SourceInvoice,
            sourceEntityId: invoice.Id,
            lines,
            initialStatus: JournalEntryStatus.Validee).Value;
        entry.SetAuditInfo("test", false);
        db.JournalEntries.Add(entry);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Fodec_missing_flags_an_invoice_whose_fodec_is_not_booked()
    {
        var factory = NewFactory("FodecMissing");
        await SeedFodecInvoiceAsync(factory, bookFodec: false);

        var anomaly = Assert.Single(await EvaluateAsync(factory, new FodecMissingAuditRule()));
        Assert.True(anomaly.Amount > 0);
    }

    [Fact]
    public async Task Fodec_missing_is_silent_when_the_fodec_is_booked()
    {
        var factory = NewFactory("FodecOk");
        await SeedFodecInvoiceAsync(factory, bookFodec: true);

        Assert.Empty(await EvaluateAsync(factory, new FodecMissingAuditRule()));
    }

    // ── Période de TVA non verrouillée ────────────────────────────────────────────────────

    private static VatDeclaration NewDeclaration(int month) =>
        VatDeclaration.CreateDraft(
            Year, month,
            Money.Create(1000m), Money.Create(0m), Money.Create(0m),
            Money.Create(0m), Money.Create(0m), Money.Create(0m));

    [Fact]
    public async Task Vat_period_flags_a_declaration_left_open_past_its_deadline()
    {
        var factory = NewFactory("VatOpen");

        await using (var db = factory.CreateContext())
        {
            // Janvier : échéance le 28 février, largement dépassée.
            db.VatDeclarations.Add(NewDeclaration(1));
            await db.SaveChangesAsync();
        }

        var anomaly = Assert.Single(await EvaluateAsync(factory, new VatPeriodNotClosedAuditRule()));
        Assert.Contains("verrouillée", anomaly.Title);
    }

    [Fact]
    public async Task Vat_period_is_silent_on_a_locked_declaration()
    {
        var factory = NewFactory("VatLocked");

        await using (var db = factory.CreateContext())
        {
            var declaration = NewDeclaration(1);
            declaration.Submit();
            declaration.Lock();
            db.VatDeclarations.Add(declaration);
            await db.SaveChangesAsync();
        }

        Assert.Empty(await EvaluateAsync(factory, new VatPeriodNotClosedAuditRule()));
    }

    /// <summary>Le délai de grâce est réglable par dossier : un cabinet tolérant ne voit rien.</summary>
    [Fact]
    public async Task Vat_period_honours_the_tenant_grace_period()
    {
        var factory = NewFactory("VatGrace");

        await using (var db = factory.CreateContext())
        {
            db.VatDeclarations.Add(NewDeclaration(1));
            await db.SaveChangesAsync();
        }

        var generous = new AccountingControlRuleSetting
        {
            RuleCode = "vat-period-not-closed",
            IsEnabled = true,
            IntThreshold = 100_000
        };

        Assert.Empty(await EvaluateAsync(factory, new VatPeriodNotClosedAuditRule(), generous));
    }
}
