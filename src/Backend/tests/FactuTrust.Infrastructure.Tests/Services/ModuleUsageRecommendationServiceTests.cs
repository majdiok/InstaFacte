using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Plan §3.3 — explainable module usage recommendations. Covers the three rules (high quote volume
/// ⇒ CRM, high delivery-note volume ⇒ Stock, high invoice volume ⇒ RecurringContracts), the
/// inactive-module precondition, threshold gating, the kill-switch, and dismissal persistence +
/// idempotence.
/// </summary>
public sealed class ModuleUsageRecommendationServiceTests
{
    private sealed class FakeTenantContext : ITenantContext
    {
        public Guid? TenantId { get; private set; }
        public string? ConnectionString { get; private set; }
        public void SetTenant(Guid tenantId, string connectionString)
        {
            TenantId = tenantId;
            ConnectionString = connectionString;
        }
        public void Clear()
        {
            TenantId = null;
            ConnectionString = null;
        }
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; set; }
        public string? Email { get; set; }
        public Guid? TenantId { get; set; }
        public UserRole? Role { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAccountingFirmDelegatedContext { get; }
        public Guid? PortalClientId { get; }
        public bool IsClientPortal { get; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public bool HasPermission(string permission) => true;
    }

    private sealed class InMemoryTenantFactory : ITenantDbContextFactory
    {
        private readonly DbContextOptions<TenantDbContext> _options;
        public InMemoryTenantFactory(string name)
        {
            _options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(name)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }
        public TenantDbContext CreateContext() => new(_options);
    }

    private sealed class Harness
    {
        public MasterDbContext Master { get; }
        public InMemoryTenantFactory TenantFactory { get; }
        public FakeTenantContext TenantContext { get; }
        public FakeCurrentUser CurrentUser { get; }
        public ModuleRecommendationsOptions Options { get; }
        public ModuleUsageRecommendationService Service { get; }

        public Harness(int minQuotes = 2, int minDeliveryNotes = 2, int minInvoices = 2, int lookbackDays = 30, bool enabled = true)
        {
            Master = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
            TenantFactory = new InMemoryTenantFactory(Guid.NewGuid().ToString());
            TenantContext = new FakeTenantContext();
            CurrentUser = new FakeCurrentUser { UserId = Guid.NewGuid() };
            Options = new ModuleRecommendationsOptions
            {
                Enabled = enabled,
                LookbackDays = lookbackDays,
                MinQuotesPerMonthForCrm = minQuotes,
                MinDeliveryNotesPerMonthForStock = minDeliveryNotes,
                MinInvoicesPerMonthForRecurringContracts = minInvoices
            };
            var tenantId = Guid.NewGuid();
            TenantContext.SetTenant(tenantId, "unused");
            Service = new ModuleUsageRecommendationService(Master, TenantContext, CurrentUser, TenantFactory, Microsoft.Extensions.Options.Options.Create(Options));
        }
    }

    private static Client MakeClient() =>
        Client.Create("Client Test", ClientType.Individual,
            Address.Create("1 rue Test", "Tunis", "Tunis", postalCode: "1000").Value,
            Email.Create("client@test.tn").Value).Value;

    /// <summary>Seeds a baseline grant (Clients) so the user is not in the "no grants ⇒ all enabled" legacy form.</summary>
    private static async Task SeedBaselineGrantAsync(Harness h)
    {
        h.Master.UserModuleGrants.Add(new UserModuleGrant
        {
            Id = Guid.NewGuid(),
            UserId = h.CurrentUser.UserId!.Value,
            Module = AppModule.Clients,
            IsEnabled = true
        });
        await h.Master.SaveChangesAsync();
    }

    private static async Task SeedQuotesAsync(Harness h, int count)
    {
        var client = MakeClient();
        using var ctx = h.TenantFactory.CreateContext();
        ctx.Clients.Add(client);
        await ctx.SaveChangesAsync();

        for (var i = 0; i < count; i++)
        {
            var quote = Quote.Create(
                QuoteNumber.Create("DEV", DateTime.UtcNow.Year, i + 1),
                client,
                DateTime.UtcNow.Date,
                DateTime.UtcNow.Date.AddDays(30)).Value;
            ctx.Quotes.Add(quote);
        }
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedDeliveryNotesAsync(Harness h, int count)
    {
        var client = MakeClient();
        using var ctx = h.TenantFactory.CreateContext();
        ctx.Clients.Add(client);
        await ctx.SaveChangesAsync();

        for (var i = 0; i < count; i++)
        {
            var number = DeliveryNoteNumber.Create($"BL-{DateTime.UtcNow.Year}-{(i + 1):D6}").Value;
            var note = DeliveryNote.Create(number, client, DateTime.UtcNow.Date, "1 rue Test").Value;
            ctx.DeliveryNotes.Add(note);
        }
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedInvoicesAsync(Harness h, int count)
    {
        var client = MakeClient();
        using var ctx = h.TenantFactory.CreateContext();
        ctx.Clients.Add(client);
        await ctx.SaveChangesAsync();

        for (var i = 0; i < count; i++)
        {
            var invoice = Invoice.Create(
                InvoiceNumber.Create("FAC", DateTime.UtcNow.Year, i + 1),
                client,
                DateTime.UtcNow.Date,
                DateTime.UtcNow.Date.AddDays(30)).Value;
            ctx.Invoices.Add(invoice);
        }
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Recommends_CRM_when_quote_volume_at_threshold_and_CRM_inactive()
    {
        var h = new Harness(minQuotes: 5);
        await SeedBaselineGrantAsync(h);
        await SeedQuotesAsync(h, 5);

        var recs = await h.Service.GetRecommendationsAsync(CancellationToken.None);

        var crm = Assert.Single(recs, r => r.ModuleId == (int)AppModule.CRM);
        Assert.Equal("high-quote-volume", crm.ReasonCode);
        Assert.False(string.IsNullOrWhiteSpace(crm.ReasonFr));
    }

    [Fact]
    public async Task Recommends_Stock_when_delivery_note_volume_at_threshold_and_Stock_inactive()
    {
        var h = new Harness(minDeliveryNotes: 5);
        await SeedBaselineGrantAsync(h);
        await SeedDeliveryNotesAsync(h, 5);

        var recs = await h.Service.GetRecommendationsAsync(CancellationToken.None);

        var stock = Assert.Single(recs, r => r.ModuleId == (int)AppModule.Stock);
        Assert.Equal("high-delivery-note-volume", stock.ReasonCode);
    }

    [Fact]
    public async Task Recommends_RecurringContracts_when_invoice_volume_at_threshold_and_module_inactive()
    {
        var h = new Harness(minInvoices: 8);
        await SeedBaselineGrantAsync(h);
        await SeedInvoicesAsync(h, 8);

        var recs = await h.Service.GetRecommendationsAsync(CancellationToken.None);

        var rc = Assert.Single(recs, r => r.ModuleId == (int)AppModule.RecurringContracts);
        Assert.Equal("high-recurring-invoice-volume", rc.ReasonCode);
    }

    [Fact]
    public async Task Does_not_recommend_an_already_enabled_module()
    {
        var h = new Harness(minQuotes: 5);
        // CRM explicitly enabled ⇒ the high-quote-volume rule is skipped even with enough quotes.
        h.Master.UserModuleGrants.Add(new UserModuleGrant
        {
            Id = Guid.NewGuid(),
            UserId = h.CurrentUser.UserId!.Value,
            Module = AppModule.CRM,
            IsEnabled = true
        });
        await h.Master.SaveChangesAsync();
        await SeedQuotesAsync(h, 5);

        var recs = await h.Service.GetRecommendationsAsync(CancellationToken.None);

        Assert.DoesNotContain(recs, r => r.ModuleId == (int)AppModule.CRM);
    }

    [Fact]
    public async Task Does_not_recommend_below_threshold()
    {
        var h = new Harness(minQuotes: 5);
        await SeedBaselineGrantAsync(h);
        await SeedQuotesAsync(h, 4); // below the 5-quote threshold

        var recs = await h.Service.GetRecommendationsAsync(CancellationToken.None);

        Assert.DoesNotContain(recs, r => r.ModuleId == (int)AppModule.CRM);
    }

    [Fact]
    public async Task Dismissed_module_is_not_recommended_and_dismissal_persists()
    {
        var h = new Harness(minQuotes: 5);
        await SeedBaselineGrantAsync(h);
        await SeedQuotesAsync(h, 5);

        var dismissResult = await h.Service.DismissAsync((int)AppModule.CRM, CancellationToken.None);
        Assert.True(dismissResult.IsSuccess);

        var recs = await h.Service.GetRecommendationsAsync(CancellationToken.None);
        Assert.DoesNotContain(recs, r => r.ModuleId == (int)AppModule.CRM);

        // Dismissal row is persisted for the tenant/module pair.
        var persisted = await h.Master.ModuleRecommendationDismissals.AsNoTracking()
            .SingleOrDefaultAsync(d => d.TenantId == h.TenantContext.TenantId!.Value && d.Module == (int)AppModule.CRM);
        Assert.NotNull(persisted);
    }

    [Fact]
    public async Task DismissAsync_is_idempotent_and_rejects_invalid_module()
    {
        var h = new Harness();

        await h.Service.DismissAsync((int)AppModule.CRM, CancellationToken.None);
        await h.Service.DismissAsync((int)AppModule.CRM, CancellationToken.None);

        var count = await h.Master.ModuleRecommendationDismissals.CountAsync(d =>
            d.TenantId == h.TenantContext.TenantId!.Value && d.Module == (int)AppModule.CRM);
        Assert.Equal(1, count); // second dismiss is a no-op, no duplicate row.

        var invalid = await h.Service.DismissAsync(9999, CancellationToken.None);
        Assert.False(invalid.IsSuccess);
        Assert.Equal("Validation.ModuleId", invalid.Error.Code);
    }

    [Fact]
    public async Task Disabled_feature_returns_no_recommendations()
    {
        var h = new Harness(enabled: false);
        await SeedBaselineGrantAsync(h);
        await SeedQuotesAsync(h, 5);

        var recs = await h.Service.GetRecommendationsAsync(CancellationToken.None);

        Assert.Empty(recs);
    }
}
