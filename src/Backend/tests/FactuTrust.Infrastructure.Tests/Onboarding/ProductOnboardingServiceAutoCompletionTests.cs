using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ProductOnboarding;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Onboarding;

/// <summary>
/// Plan §2.6 — dedicated coverage for the auto-completion checks added to
/// <c>ProductOnboardingService.CollectAutoCompletedIdsAsync</c> this session
/// (<c>check-default-warehouse</c>, <c>commerce-stock-receipt</c>, <c>numbering</c>), plus a
/// regression check confirming <c>create-invoice</c> (already implemented pre-plan) still fires.
/// Uses a REAL InMemory-backed <see cref="TenantDbContext"/> (unlike the existing
/// <c>ProductOnboardingServiceTests</c>, which only exercises the disabled/kill-switch/fail-open
/// paths) since these checks need actual tenant DB rows to evaluate.
/// </summary>
public sealed class ProductOnboardingServiceAutoCompletionTests
{
    [Fact]
    public async Task Default_warehouse_still_carrying_the_generic_seed_name_is_not_auto_completed()
    {
        var ids = await CollectIdsAsync(tenantDb =>
        {
            AddWarehouse(tenantDb, "Entrepôt Principal", isDefault: true);
        });

        Assert.DoesNotContain(ProductOnboardingDefaults.CompanyItemIds.CheckDefaultWarehouse, ids);
    }

    [Fact]
    public async Task Renamed_default_warehouse_is_auto_completed()
    {
        var ids = await CollectIdsAsync(tenantDb =>
        {
            AddWarehouse(tenantDb, "Dépôt Nord", isDefault: true);
        });

        Assert.Contains(ProductOnboardingDefaults.CompanyItemIds.CheckDefaultWarehouse, ids);
    }

    [Fact]
    public async Task Second_warehouse_created_is_auto_completed_even_if_default_name_is_unchanged()
    {
        var ids = await CollectIdsAsync(tenantDb =>
        {
            AddWarehouse(tenantDb, "Entrepôt Principal", isDefault: true);
            AddWarehouse(tenantDb, "Entrepôt Secondaire", isDefault: false);
        });

        Assert.Contains(ProductOnboardingDefaults.CompanyItemIds.CheckDefaultWarehouse, ids);
    }

    [Fact]
    public async Task No_stock_voucher_is_not_auto_completed()
    {
        var ids = await CollectIdsAsync(_ => { });

        Assert.DoesNotContain(ProductOnboardingDefaults.CompanyItemIds.CommerceStockReceipt, ids);
    }

    [Fact]
    public async Task Draft_stock_entry_is_not_auto_completed()
    {
        var ids = await CollectIdsAsync(tenantDb =>
        {
            var warehouse = AddWarehouse(tenantDb, "Entrepôt Principal", isDefault: true);
            AddStockVoucher(tenantDb, warehouse, StockVoucherKind.Entry, StockVoucherStatus.Draft);
        });

        Assert.DoesNotContain(ProductOnboardingDefaults.CompanyItemIds.CommerceStockReceipt, ids);
    }

    [Fact]
    public async Task Validated_stock_issue_alone_is_not_auto_completed()
    {
        var ids = await CollectIdsAsync(tenantDb =>
        {
            var warehouse = AddWarehouse(tenantDb, "Entrepôt Principal", isDefault: true);
            AddStockVoucher(tenantDb, warehouse, StockVoucherKind.Issue, StockVoucherStatus.Validated);
        });

        Assert.DoesNotContain(ProductOnboardingDefaults.CompanyItemIds.CommerceStockReceipt, ids);
    }

    [Fact]
    public async Task Validated_stock_entry_is_auto_completed()
    {
        var ids = await CollectIdsAsync(tenantDb =>
        {
            var warehouse = AddWarehouse(tenantDb, "Entrepôt Principal", isDefault: true);
            AddStockVoucher(tenantDb, warehouse, StockVoucherKind.Entry, StockVoucherStatus.Validated);
        });

        Assert.Contains(ProductOnboardingDefaults.CompanyItemIds.CommerceStockReceipt, ids);
    }

    [Fact]
    public async Task Numbering_scheme_left_at_default_is_not_auto_completed()
    {
        var ids = await CollectIdsAsync(tenantDb =>
        {
            var scheme = DocumentNumberingScheme.CreateDefault(Guid.NewGuid(), NumberingDocumentType.Invoice, DateTime.UtcNow.Year);
            AddNumberingScheme(tenantDb, scheme);
        });

        Assert.DoesNotContain(ProductOnboardingDefaults.CompanyItemIds.Numbering, ids);
    }

    [Fact]
    public async Task Numbering_scheme_with_custom_start_number_is_auto_completed()
    {
        var ids = await CollectIdsAsync(tenantDb =>
        {
            var scheme = DocumentNumberingScheme.CreateDefault(Guid.NewGuid(), NumberingDocumentType.Invoice, DateTime.UtcNow.Year);
            scheme.UpdateStartNumber(100);
            AddNumberingScheme(tenantDb, scheme);
        });

        Assert.Contains(ProductOnboardingDefaults.CompanyItemIds.Numbering, ids);
    }

    [Fact]
    public async Task Numbering_scheme_with_custom_format_blocks_is_auto_completed()
    {
        var ids = await CollectIdsAsync(tenantDb =>
        {
            var scheme = DocumentNumberingScheme.CreateDefault(Guid.NewGuid(), NumberingDocumentType.Invoice, DateTime.UtcNow.Year);
            var result = scheme.UpdateFormat(new NumberingFormatBlock[]
            {
                new(NumberingBlockType.FreeText, "CUSTOM", 0),
                new(NumberingBlockType.Separator, "-", 1),
                new(NumberingBlockType.DocumentNumberPadded6, null, 2)
            });
            Assert.True(result.IsSuccess, result.Error?.Description);
            AddNumberingScheme(tenantDb, scheme);
        });

        Assert.Contains(ProductOnboardingDefaults.CompanyItemIds.Numbering, ids);
    }

    [Fact]
    public async Task Existing_invoice_is_auto_completed()
    {
        var ids = await CollectIdsAsync(tenantDb =>
        {
            var address = Address.Create("1 rue Test", "Tunis", "Tunis", postalCode: "1000").Value;
            var clientEmail = Email.Create("client2@test.tn").Value;
            var client = Client.Create("Client Test", ClientType.Individual, address, clientEmail).Value;
            tenantDb.Clients.Add(client);

            var invoiceNumber = InvoiceNumber.Create("FAC", DateTime.UtcNow.Year, 1);
            var invoice = Invoice.Create(invoiceNumber, client, DateTime.UtcNow, DateTime.UtcNow.AddDays(30)).Value;
            tenantDb.Invoices.Add(invoice);
        });

        Assert.Contains(ProductOnboardingDefaults.CompanyItemIds.CreateInvoice, ids);
    }

    private static void AddNumberingScheme(TenantDbContext tenantDb, DocumentNumberingScheme scheme)
    {
        // RowVersion is a SQL Server rowversion column (.IsRowVersion()) with no in-memory
        // default generator, and DocumentNumberingScheme never initializes it in managed code
        // (it's normally populated by the real SQL Server provider on insert) — the InMemory
        // provider requires all non-nullable properties to have a value before SaveChanges.
        typeof(DocumentNumberingScheme).GetProperty(nameof(DocumentNumberingScheme.RowVersion))!
            .SetValue(scheme, new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 });
        tenantDb.DocumentNumberingSchemes.Add(scheme);
    }

    private static Warehouse AddWarehouse(TenantDbContext tenantDb, string name, bool isDefault)
    {
        var warehouse = Warehouse.Create($"WH-{Guid.NewGuid():N}"[..12], name, isDefault: isDefault).Value;
        tenantDb.Warehouses.Add(warehouse);
        return warehouse;
    }

    private static void AddStockVoucher(TenantDbContext tenantDb, Warehouse warehouse, StockVoucherKind kind, StockVoucherStatus status)
    {
        var number = StockVoucherNumber.Create(kind.DefaultPrefix(), DateTime.UtcNow.Year, 1);
        var reason = kind == StockVoucherKind.Entry ? MovementReason.InitialStock : MovementReason.Damage;
        var voucher = StockVoucher.Create(number, kind, warehouse, DateTime.UtcNow, reason).Value;

        // Bypass the "must have at least one line" MarkValidated() guard: this test only cares
        // about Kind/Status being read back by CollectAutoCompletedIdsAsync, not full stock movement
        // business rules — same reflection convention already used elsewhere in this test project
        // (see ProductOnboardingServiceTests.Auto_progress_failure_does_not_fail_get, which
        // reflectively sets Tenant.Id).
        if (status != StockVoucherStatus.Draft)
            typeof(StockVoucher).GetProperty(nameof(StockVoucher.Status))!.SetValue(voucher, status);

        tenantDb.StockVouchers.Add(voucher);
    }

    private static async Task<IReadOnlyList<string>> CollectIdsAsync(Action<TenantDbContext> seedTenantDb)
    {
        await using var masterDb = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var tenantDbName = Guid.NewGuid().ToString();
        var tenantOptions = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase(tenantDbName)
            .Options;

        await using (var seedContext = new TenantDbContext(tenantOptions))
        {
            seedTenantDb(seedContext);
            await seedContext.SaveChangesAsync();
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "auto@test.tn",
            UserName = "auto@test.tn",
            FirstName = "Auto",
            LastName = "Test",
            TenantId = Guid.NewGuid()
        };
        user.ApplyNewInteractiveProductOnboarding();
        masterDb.Users.Add(user);

        var nif = NIF.Create("7654321/A/B/C/000").Value;
        var address = Address.Create("1 rue Test", "Tunis", "Tunis", postalCode: "1000").Value;
        var email = Email.Create("tenant@test.tn").Value;
        var phone = PhoneNumber.Create("20123456").Value;
        var tenant = Tenant.Create("Ste Test", nif, address, email, phone, TaxRegime.RealRegime).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(tenant, user.TenantId);
        masterDb.Tenants.Add(tenant);
        await masterDb.SaveChangesAsync();

        var current = new Mock<ICurrentUser>();
        current.Setup(c => c.UserId).Returns(user.Id);
        current.Setup(c => c.IsAccountingFirmDelegatedContext).Returns(false);

        var factory = new Mock<ITenantDbContextFactory>();
        factory.Setup(f => f.CreateContext()).Returns(new TenantDbContext(tenantOptions));

        var service = new ProductOnboardingService(
            masterDb,
            current.Object,
            factory.Object,
            Options.Create(new ProductOnboardingSettings { Enabled = true }),
            NullLogger<ProductOnboardingService>.Instance);

        var dto = await service.GetMineAsync(CancellationToken.None);
        return dto.AutoCompletedIds;
    }
}
