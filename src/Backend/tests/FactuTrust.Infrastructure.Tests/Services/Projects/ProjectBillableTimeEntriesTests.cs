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

public sealed class ProjectBillableTimeEntriesTests
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

    private sealed record Fixture(Guid ProjectId, Guid Entry1Id, Guid Entry2Id, Guid TaskHourlyId, Client Client);

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

        var numbers = new Mock<IInvoiceNumberGenerator>();
        numbers.Setup(n => n.ReserveNextNumberAsync(tenantId, "FAC", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InvoiceNumber.Create("FAC", 2026, 100));

        return new ProjectService(
            factory,
            new MasterDbContext(masterOptions),
            clients.Object,
            Mock.Of<IPurchaseOrderRepository>(),
            Mock.Of<ISupplierRepository>(),
            Mock.Of<IProductRepository>(),
            numbers.Object,
            Mock.Of<ICurrentUser>(),
            tenant.Object,
            Mock.Of<IMediator>(),
            Mock.Of<IConfiguration>());
    }

    private static async Task<Fixture> SeedTwoEntriesAsync(InMemoryTenantDbContextFactory factory)
    {
        var address = Address.Create("1 rue Test", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;

        var project = Project.Create(client.Id, "Mission", ProjectKind.Esn, ProjectBillingMode.TimeAndMaterials, null, null, null, 5000m).Value;
        Assert.True(project.Activate().IsSuccess);

        var phase = ProjectPhase.Create(project.Id, "À faire", 0).Value;
        var taskHourly = ProjectTask.Create(project.Id, phase.Id, "Tâche horaire", null, ProjectTaskPriority.Normal, null, null, null, 8m).Value;

        var aliceId = Guid.NewGuid();
        var bobId = Guid.NewGuid();
        var alice = ProjectMember.Create(project.Id, aliceId, ProjectMemberRole.Member, null, 80m, 40m).Value;
        var bob = ProjectMember.Create(project.Id, bobId, ProjectMemberRole.Member, 320m, 100m, 40m).Value;

        var entry1 = ProjectTimeEntry.Create(project.Id, aliceId, new DateTime(2026, 8, 1), 5m, true, null, taskHourly.Id).Value;
        Assert.True(entry1.Submit().IsSuccess);
        Assert.True(entry1.Validate().IsSuccess);

        var entry2 = ProjectTimeEntry.Create(project.Id, bobId, new DateTime(2026, 8, 2), 3m, true, null, null).Value;
        Assert.True(entry2.Submit().IsSuccess);
        Assert.True(entry2.Validate().IsSuccess);

        await using (var ctx = factory.CreateContext())
        {
            ctx.Clients.Add(client);
            ctx.Projects.Add(project);
            ctx.ProjectPhases.Add(phase);
            ctx.ProjectTasks.Add(taskHourly);
            ctx.ProjectMembers.AddRange(alice, bob);
            ctx.ProjectTimeEntries.AddRange(entry1, entry2);
            await ctx.SaveChangesAsync();
        }

        return new Fixture(project.Id, entry1.Id, entry2.Id, taskHourly.Id, client);
    }

    [Fact]
    public async Task GetBillableTimeEntries_NoValidatedTime_ReturnsEmpty()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var address = Address.Create("1 rue Test", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;
        var project = Project.Create(client.Id, "Mission", ProjectKind.Esn, ProjectBillingMode.TimeAndMaterials, null, null, null, 5000m).Value;
        Assert.True(project.Activate().IsSuccess);

        await using (var ctx = factory.CreateContext())
        {
            ctx.Clients.Add(client);
            ctx.Projects.Add(project);
            await ctx.SaveChangesAsync();
        }

        await using var sut = CreateService(factory, client);
        var items = await sut.GetBillableTimeEntriesAsync(project.Id);

        Assert.Empty(items);
    }

    [Fact]
    public async Task GetBillableTimeEntries_ReturnsEligibleEntriesWithPreview()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedTwoEntriesAsync(factory);
        await using var sut = CreateService(factory, fx.Client);

        var items = await sut.GetBillableTimeEntriesAsync(fx.ProjectId);

        Assert.Equal(2, items.Count);
        var latest = items[0];
        Assert.Equal(fx.Entry2Id, latest.Id);
        Assert.True(latest.IsEligible);
        Assert.Equal(3m, latest.Hours);
        Assert.Equal(40m, latest.HourlyRate);
        Assert.Equal(120m, latest.PreviewAmountHt);
    }

    [Fact]
    public async Task GetBillableTimeEntries_WhenTjmAndCostBothSet_UsesTjmForClientBilling()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var address = Address.Create("1 rue Test", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;

        var project = Project.Create(client.Id, "Mission", ProjectKind.Esn, ProjectBillingMode.TimeAndMaterials, null, null, null, 5000m).Value;
        Assert.True(project.Activate().IsSuccess);

        var memberId = Guid.NewGuid();
        var member = ProjectMember.Create(project.Id, memberId, ProjectMemberRole.Member, 320m, 100m, 40m).Value;
        var entry = ProjectTimeEntry.Create(project.Id, memberId, new DateTime(2026, 8, 3), 2m, true, null, null).Value;
        Assert.True(entry.Submit().IsSuccess);
        Assert.True(entry.Validate().IsSuccess);

        await using (var ctx = factory.CreateContext())
        {
            ctx.Clients.Add(client);
            ctx.Projects.Add(project);
            ctx.ProjectMembers.Add(member);
            ctx.ProjectTimeEntries.Add(entry);
            await ctx.SaveChangesAsync();
        }

        await using var sut = CreateService(factory, client);
        var items = await sut.GetBillableTimeEntriesAsync(project.Id);

        Assert.Single(items);
        Assert.Equal(40m, items[0].HourlyRate);
        Assert.Equal(80m, items[0].PreviewAmountHt);
    }

    [Fact]
    public async Task GetBillableTimeEntries_ExcludesInvoicedEntries()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedTwoEntriesAsync(factory);

        await using (var ctx = factory.CreateContext())
        {
            var entry = await ctx.ProjectTimeEntries.FirstAsync(e => e.Id == fx.Entry1Id);
            entry.MarkInvoiced(Guid.NewGuid());
            await ctx.SaveChangesAsync();
        }

        await using var sut = CreateService(factory, fx.Client);
        var items = await sut.GetBillableTimeEntriesAsync(fx.ProjectId);

        Assert.Single(items);
        Assert.Equal(fx.Entry2Id, items[0].Id);
    }

    [Fact]
    public async Task GetBillableTimeEntries_ExcludesNonBillableEntries()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var address = Address.Create("1 rue Test", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;

        var project = Project.Create(client.Id, "Mission", ProjectKind.Esn, ProjectBillingMode.TimeAndMaterials, null, null, null, 5000m).Value;
        Assert.True(project.Activate().IsSuccess);

        var memberId = Guid.NewGuid();
        var member = ProjectMember.Create(project.Id, memberId, ProjectMemberRole.Member, null, 80m, 40m).Value;
        var billable = ProjectTimeEntry.Create(project.Id, memberId, new DateTime(2026, 8, 1), 4m, true, null, null).Value;
        var nonBillable = ProjectTimeEntry.Create(project.Id, memberId, new DateTime(2026, 8, 2), 2m, false, null, null).Value;
        Assert.True(billable.Submit().IsSuccess);
        Assert.True(billable.Validate().IsSuccess);
        Assert.True(nonBillable.Submit().IsSuccess);
        Assert.True(nonBillable.Validate().IsSuccess);

        await using (var ctx = factory.CreateContext())
        {
            ctx.Clients.Add(client);
            ctx.Projects.Add(project);
            ctx.ProjectMembers.Add(member);
            ctx.ProjectTimeEntries.AddRange(billable, nonBillable);
            await ctx.SaveChangesAsync();
        }

        await using var sut = CreateService(factory, client);
        var items = await sut.GetBillableTimeEntriesAsync(project.Id);

        Assert.Single(items);
        Assert.Equal(billable.Id, items[0].Id);
    }

    [Fact]
    public async Task GetBillableTimeEntries_ExcludesEntriesOnForfaitInvoicedTask()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedTwoEntriesAsync(factory);

        await using (var ctx = factory.CreateContext())
        {
            var task = await ctx.ProjectTasks.FirstAsync(t => t.Id == fx.TaskHourlyId);
            task.MarkInvoiced(Guid.NewGuid(), ProjectTaskBillingMethod.Fixed);
            await ctx.SaveChangesAsync();
        }

        await using var sut = CreateService(factory, fx.Client);
        var items = await sut.GetBillableTimeEntriesAsync(fx.ProjectId);

        Assert.Single(items);
        Assert.Equal(fx.Entry2Id, items[0].Id);
    }

    [Fact]
    public async Task GetBillableTimeEntries_MemberWithoutRate_IsIneligible()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedTwoEntriesAsync(factory);

        await using (var ctx = factory.CreateContext())
        {
            var member = await ctx.ProjectMembers.FirstAsync();
            member.Update(ProjectMemberRole.Member, null, null, member.WeeklyCapacityHours);
            await ctx.SaveChangesAsync();
        }

        await using var sut = CreateService(factory, fx.Client);
        var items = await sut.GetBillableTimeEntriesAsync(fx.ProjectId);

        Assert.Contains(items, i => !i.IsEligible && i.BlockReason != null);
    }

    [Fact]
    public async Task InvoiceTime_WithSelectedIds_OnlyMarksSelectedEntries()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedTwoEntriesAsync(factory);
        await using var sut = CreateService(factory, fx.Client);

        var result = await sut.InvoiceTimeAsync(fx.ProjectId, new InvoiceTimeDto
        {
            GroupBy = "member",
            TimeEntryIds = new[] { fx.Entry1Id }
        });

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");

        await using var verify = factory.CreateContext();
        var entry1 = await verify.ProjectTimeEntries.FirstAsync(e => e.Id == fx.Entry1Id);
        var entry2 = await verify.ProjectTimeEntries.FirstAsync(e => e.Id == fx.Entry2Id);
        Assert.NotNull(entry1.InvoicedInvoiceId);
        Assert.Null(entry2.InvoicedInvoiceId);
    }

    [Fact]
    public async Task InvoiceTime_WithInvalidId_Fails()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedTwoEntriesAsync(factory);
        await using var sut = CreateService(factory, fx.Client);

        var result = await sut.InvoiceTimeAsync(fx.ProjectId, new InvoiceTimeDto
        {
            GroupBy = "member",
            TimeEntryIds = new[] { Guid.NewGuid() }
        });

        Assert.True(result.IsFailure);
        Assert.Contains("facturables", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvoiceTime_WithEmptyIds_LegacyInvoicesAllEligible()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedTwoEntriesAsync(factory);
        await using var sut = CreateService(factory, fx.Client);

        var result = await sut.InvoiceTimeAsync(fx.ProjectId, new InvoiceTimeDto { GroupBy = "member" });

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");

        await using var verify = factory.CreateContext();
        var entries = await verify.ProjectTimeEntries.Where(e => e.ProjectId == fx.ProjectId).ToListAsync();
        Assert.All(entries, e => Assert.NotNull(e.InvoicedInvoiceId));
    }

    [Fact]
    public async Task InvoiceTime_PartialMultiMember_CreatesMultipleInvoiceLines()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedTwoEntriesAsync(factory);
        await using var sut = CreateService(factory, fx.Client);

        var result = await sut.InvoiceTimeAsync(fx.ProjectId, new InvoiceTimeDto
        {
            GroupBy = "member",
            TimeEntryIds = new[] { fx.Entry1Id, fx.Entry2Id }
        });

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");

        await using var verify = factory.CreateContext();
        var invoice = await verify.Invoices.Include(i => i.Lines).FirstAsync(i => i.Id == result.Value.InvoiceId);
        Assert.Equal(2, invoice.Lines.Count);
    }
}
