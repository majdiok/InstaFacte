using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Projects;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.Projects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services.Projects;

/// <summary>Factures liées projet : union factures + avoirs, tri date desc, champs HT/TVA/TTC.</summary>
public sealed class ProjectLinkedInvoicesTests
{
    private sealed class InMemoryTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly DbContextOptions<TenantDbContext> _options;

        public InMemoryTenantDbContextFactory(string databaseName)
        {
            _options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(databaseName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        public TenantDbContext CreateContext() => new(_options);
        public TenantDbContext CreateIsolatedContext() => new(_options);
    }

    private sealed record ProjectFixture(Guid ProjectId, Guid ClientId, Client Client);

    private static async Task<ProjectFixture> SeedProjectAsync(InMemoryTenantDbContextFactory factory)
    {
        var address = Address.Create("1 rue Test", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client projet", ClientType.Individual, address, email).Value;
        var project = Project.Create(client.Id, "Mission PSA", ProjectKind.Esn, ProjectBillingMode.TimeAndMaterials, null, null, null, 5000m).Value;
        Assert.True(project.Activate().IsSuccess);

        await using (var ctx = factory.CreateContext())
        {
            ctx.Clients.Add(client);
            ctx.Projects.Add(project);
            await ctx.SaveChangesAsync();
        }

        return new ProjectFixture(project.Id, client.Id, client);
    }

    private static async Task<Invoice> SeedProjectInvoiceAsync(
        InMemoryTenantDbContextFactory factory,
        ProjectFixture fx,
        DateTime issueDate,
        decimal amountHt,
        ProjectBillingKind kind,
        string numberPrefix = "FA")
    {
        await using var ctx = factory.CreateContext();
        var client = await ctx.Clients.FirstAsync(c => c.Id == fx.ClientId);
        var invoice = Invoice.Create(
            InvoiceNumber.Create(numberPrefix, issueDate.Year, Random.Shared.Next(1, 999999)),
            client,
            issueDate,
            issueDate.AddDays(30),
            $"PRJ-Mission PSA",
            "Facturation projet").Value;
        var add = invoice.AddCustomLine("Prestation", null, 1m, "Unité", Money.Create(amountHt), VatRate.Standard);
        Assert.True(add.IsSuccess);
        Assert.True(invoice.Validate().IsSuccess);

        var billing = ProjectBilling.Create(fx.ProjectId, kind, invoice.Id, amountHt, null);
        Assert.True(billing.IsSuccess);
        invoice.SetProjectBillingSource(fx.ProjectId, billing.Value.Id);

        ctx.Invoices.Add(invoice);
        ctx.ProjectBillings.Add(billing.Value);
        await ctx.SaveChangesAsync();
        return invoice;
    }

    private static async Task<Invoice> SeedCreditNoteAsync(
        InMemoryTenantDbContextFactory factory,
        ProjectFixture fx,
        Guid linkedInvoiceId,
        DateTime issueDate,
        decimal amountHtMagnitude)
    {
        await using var ctx = factory.CreateContext();
        var client = await ctx.Clients.FirstAsync(c => c.Id == fx.ClientId);
        var note = Invoice.CreateCreditNote(
            InvoiceNumber.Create("AVO", issueDate.Year, Random.Shared.Next(1, 999999)),
            client,
            issueDate,
            linkedInvoiceId).Value;
        var add = note.AddCustomLine("Avoir", null, 1m, "Unité", Money.Create(amountHtMagnitude), VatRate.Standard);
        Assert.True(add.IsSuccess);
        Assert.True(note.Validate().IsSuccess);
        ctx.Invoices.Add(note);
        await ctx.SaveChangesAsync();
        return note;
    }

    private static ProjectService CreateService(InMemoryTenantDbContextFactory factory, Client client)
    {
        var masterOptions = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenantId = Guid.NewGuid();
        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(t => t.TenantId).Returns(tenantId);

        var clients = new Mock<IClientRepository>();
        clients.Setup(c => c.GetByIdAsync(client.Id, It.IsAny<CancellationToken>())).ReturnsAsync(client);

        return new ProjectService(
            factory,
            new MasterDbContext(masterOptions),
            clients.Object,
            Mock.Of<IPurchaseOrderRepository>(),
            Mock.Of<ISupplierRepository>(),
            Mock.Of<IProductRepository>(),
            Mock.Of<IInvoiceNumberGenerator>(),
            Mock.Of<ICurrentUser>(),
            tenant.Object,
            Mock.Of<IMediator>(),
            Mock.Of<IConfiguration>());
    }

    [Fact]
    public async Task LinkedInvoices_UnknownProject_ReturnsNull()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedProjectAsync(factory);
        await using var sut = CreateService(factory, fx.Client);

        var items = await sut.GetLinkedInvoicesAsync(Guid.NewGuid());

        Assert.Null(items);
    }

    [Fact]
    public async Task LinkedInvoices_NoInvoices_ReturnsEmpty()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedProjectAsync(factory);
        await using var sut = CreateService(factory, fx.Client);

        var items = await sut.GetLinkedInvoicesAsync(fx.ProjectId);

        Assert.NotNull(items);
        Assert.Empty(items);
    }

    [Fact]
    public async Task LinkedInvoices_ReturnsInvoicesOrderedByIssueDateDesc_WithFields()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedProjectAsync(factory);
        var invoice1 = await SeedProjectInvoiceAsync(factory, fx, new DateTime(2026, 1, 15), 100m, ProjectBillingKind.TimeAndMaterials);
        var invoice2 = await SeedProjectInvoiceAsync(factory, fx, new DateTime(2026, 2, 15), 200m, ProjectBillingKind.Milestone);

        await using var sut = CreateService(factory, fx.Client);
        var items = await sut.GetLinkedInvoicesAsync(fx.ProjectId);

        Assert.NotNull(items);
        Assert.Equal(2, items!.Count);
        Assert.Equal(invoice2.Id, items[0].InvoiceId);
        Assert.Equal(invoice1.Id, items[1].InvoiceId);

        var latest = items[0];
        Assert.Equal("Client projet", latest.ClientName);
        Assert.Equal(200m, latest.AmountHT);
        Assert.Equal(200m * 0.19m, latest.AmountVat);
        Assert.Equal(200m * 1.19m, latest.AmountTTC);
        Assert.Equal(InvoiceStatus.Validated, latest.Status);
        Assert.Equal("Validée", latest.StatusDisplay);
        Assert.False(latest.IsCreditNote);
        Assert.Equal(ProjectBillingKind.Milestone, latest.BillingKind);
        Assert.True(latest.CreatedAt > DateTime.MinValue);
    }

    [Fact]
    public async Task LinkedInvoices_IncludesCreditNotesLinkedToProjectInvoices()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedProjectAsync(factory);
        var invoice = await SeedProjectInvoiceAsync(factory, fx, new DateTime(2026, 2, 1), 250m, ProjectBillingKind.FixedPrice);
        var note = await SeedCreditNoteAsync(factory, fx, invoice.Id, new DateTime(2026, 2, 20), 50m);

        await using var sut = CreateService(factory, fx.Client);
        var items = await sut.GetLinkedInvoicesAsync(fx.ProjectId);

        Assert.NotNull(items);
        Assert.Equal(2, items!.Count);
        Assert.Contains(items, i => i.InvoiceId == note.Id && i.IsCreditNote);
        var creditNote = items.First(i => i.InvoiceId == note.Id);
        Assert.Equal(-50m, creditNote.AmountHT);
        Assert.Null(creditNote.BillingKind);
    }

    [Fact]
    public async Task LinkedInvoices_DoesNotDuplicateCreditNote_WhenItAlreadyCarriesProjectLink()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedProjectAsync(factory);
        var invoice = await SeedProjectInvoiceAsync(factory, fx, new DateTime(2026, 3, 1), 100m, ProjectBillingKind.TaskFixed);
        var note = await SeedCreditNoteAsync(factory, fx, invoice.Id, new DateTime(2026, 3, 10), 25m);

        await using (var ctx = factory.CreateContext())
        {
            var loaded = await ctx.Invoices.FirstAsync(i => i.Id == note.Id);
            var billing = ProjectBilling.Create(fx.ProjectId, ProjectBillingKind.TaskFixed, note.Id, 25m, null).Value;
            loaded.SetProjectBillingSource(fx.ProjectId, billing.Id);
            ctx.ProjectBillings.Add(billing);
            await ctx.SaveChangesAsync();
        }

        await using var sut = CreateService(factory, fx.Client);
        var items = await sut.GetLinkedInvoicesAsync(fx.ProjectId);

        Assert.NotNull(items);
        Assert.Equal(2, items!.Count);
        Assert.Equal(1, items.Count(i => i.InvoiceId == note.Id));
    }

    [Fact]
    public async Task LinkedInvoices_ExcludesInvoicesFromOtherProjects()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx1 = await SeedProjectAsync(factory);
        var fx2 = await SeedProjectAsync(factory);
        await SeedProjectInvoiceAsync(factory, fx1, new DateTime(2026, 4, 1), 100m, ProjectBillingKind.TimeAndMaterials);
        var otherInvoice = await SeedProjectInvoiceAsync(factory, fx2, new DateTime(2026, 4, 2), 300m, ProjectBillingKind.Situation);

        await using var sut = CreateService(factory, fx1.Client);
        var items = await sut.GetLinkedInvoicesAsync(fx1.ProjectId);

        Assert.NotNull(items);
        Assert.Single(items);
        Assert.DoesNotContain(items, i => i.InvoiceId == otherInvoice.Id);
    }
}
