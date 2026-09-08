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

namespace FactuTrust.Infrastructure.Tests.Domain.Projects;

public sealed class ProjectTaskBillingServiceTests
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

    private sealed record BillingFixture(
        Guid ProjectId,
        Guid TaskHourlyId,
        Guid TaskFixedId,
        Client Client);

    private static async Task<BillingFixture> SeedAsync(InMemoryTenantDbContextFactory factory)
    {
        var address = Address.Create("1 rue Test", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;

        var project = Project.Create(client.Id, "Mission", ProjectKind.Esn, ProjectBillingMode.TimeAndMaterials, null, null, null, 5000m).Value;
        Assert.True(project.Activate().IsSuccess);

        var phase = ProjectPhase.Create(project.Id, "À faire", 0).Value;
        var taskHourly = ProjectTask.Create(project.Id, phase.Id, "Tâche horaire", null, ProjectTaskPriority.Normal, null, null, null, 8m).Value;
        var taskFixed = ProjectTask.Create(project.Id, phase.Id, "Tâche forfait", null, ProjectTaskPriority.Normal, null, null, null, 0m).Value;
        var userId = Guid.NewGuid();
        var member = ProjectMember.Create(project.Id, userId, ProjectMemberRole.Member, 640m, 80m, 40m).Value;

        var entry = ProjectTimeEntry.Create(project.Id, userId, new DateTime(2026, 8, 1), 5m, true, null, taskHourly.Id).Value;
        Assert.True(entry.Submit().IsSuccess);
        Assert.True(entry.Validate().IsSuccess);

        await using (var ctx = factory.CreateContext())
        {
            ctx.Clients.Add(client);
            ctx.Projects.Add(project);
            ctx.ProjectPhases.Add(phase);
            ctx.ProjectTasks.AddRange(taskHourly, taskFixed);
            ctx.ProjectMembers.Add(member);
            ctx.ProjectTimeEntries.Add(entry);
            await ctx.SaveChangesAsync();
        }

        return new BillingFixture(project.Id, taskHourly.Id, taskFixed.Id, client);
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

    [Fact]
    public async Task GetBillableTasks_Hourly_ReturnsTaskWithHours()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedAsync(factory);
        await using var sut = CreateService(factory, fx.Client);

        var tasks = await sut.GetBillableTasksAsync(fx.ProjectId, "hourly");

        Assert.Contains(tasks, t => t.Id == fx.TaskHourlyId && t.IsEligible && t.UninvoicedBillableHours == 5m);
        Assert.DoesNotContain(tasks, t => t.Id == fx.TaskFixedId);
    }

    [Fact]
    public async Task GetBillableTasks_Fixed_IncludesTaskWithoutHours()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedAsync(factory);
        await using var sut = CreateService(factory, fx.Client);

        var tasks = await sut.GetBillableTasksAsync(fx.ProjectId, "fixed");

        Assert.Contains(tasks, t => t.Id == fx.TaskFixedId && t.IsEligible);
        Assert.Contains(tasks, t => t.Id == fx.TaskHourlyId && t.IsEligible);
    }

    [Fact]
    public async Task InvoiceTasks_Hourly_LocksTaskAndMarksTime()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedAsync(factory);
        await using var sut = CreateService(factory, fx.Client);

        var result = await sut.InvoiceTasksAsync(fx.ProjectId, new InvoiceTasksDto
        {
            Method = "hourly",
            Tasks = new[] { new InvoiceTaskLineDto { TaskId = fx.TaskHourlyId } }
        });

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");
        await using (var verify = factory.CreateContext())
        {
            var task = await verify.ProjectTasks.FirstAsync(t => t.Id == fx.TaskHourlyId);
            Assert.NotNull(task.InvoicedInvoiceId);
            Assert.Equal(ProjectTaskBillingMethod.Hourly, task.InvoicedBillingMethod);

            var entry = await verify.ProjectTimeEntries.FirstAsync(e => e.TaskId == fx.TaskHourlyId);
            Assert.Equal(task.InvoicedInvoiceId, entry.InvoicedInvoiceId);

            var billing = await verify.ProjectBillings.FirstAsync(b => b.Id == result.Value.BillingId);
            Assert.Equal(ProjectBillingKind.TaskHourly, billing.Kind);
        }
    }

    [Fact]
    public async Task InvoiceTasks_Fixed_LocksTaskWithoutRequiringHours()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedAsync(factory);
        await using var sut = CreateService(factory, fx.Client);

        var result = await sut.InvoiceTasksAsync(fx.ProjectId, new InvoiceTasksDto
        {
            Method = "fixed",
            Tasks = new[] { new InvoiceTaskLineDto { TaskId = fx.TaskFixedId, AmountHt = 1200m } }
        });

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");
        await using var verify = factory.CreateContext();
        var task = await verify.ProjectTasks.FirstAsync(t => t.Id == fx.TaskFixedId);
        Assert.Equal(ProjectTaskBillingMethod.Fixed, task.InvoicedBillingMethod);
        var billing = await verify.ProjectBillings.FirstAsync(b => b.Id == result.Value.BillingId);
        Assert.Equal(ProjectBillingKind.TaskFixed, billing.Kind);
    }

    [Fact]
    public async Task InvoiceTasks_SecondCallOnSameTask_Fails()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedAsync(factory);
        await using var sut = CreateService(factory, fx.Client);

        var first = await sut.InvoiceTasksAsync(fx.ProjectId, new InvoiceTasksDto
        {
            Method = "fixed",
            Tasks = new[] { new InvoiceTaskLineDto { TaskId = fx.TaskFixedId, AmountHt = 500m } }
        });
        Assert.True(first.IsSuccess);

        var second = await sut.InvoiceTasksAsync(fx.ProjectId, new InvoiceTasksDto
        {
            Method = "hourly",
            Tasks = new[] { new InvoiceTaskLineDto { TaskId = fx.TaskFixedId } }
        });
        Assert.True(second.IsFailure);
    }

    [Fact]
    public async Task InvoiceTime_GroupByTask_IsRejected()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedAsync(factory);
        await using var sut = CreateService(factory, fx.Client);

        var result = await sut.InvoiceTimeAsync(fx.ProjectId, new InvoiceTimeDto { GroupBy = "task" });
        Assert.True(result.IsFailure);
        Assert.Contains("facturation par tâche", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvoiceTasks_Fixed_OnTaskWithHours_MarksTimeEntries()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedAsync(factory);
        await using var sut = CreateService(factory, fx.Client);

        var result = await sut.InvoiceTasksAsync(fx.ProjectId, new InvoiceTasksDto
        {
            Method = "fixed",
            Tasks = new[] { new InvoiceTaskLineDto { TaskId = fx.TaskHourlyId, AmountHt = 900m } }
        });

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");
        await using var verify = factory.CreateContext();
        var entry = await verify.ProjectTimeEntries.FirstAsync(e => e.TaskId == fx.TaskHourlyId);
        Assert.NotNull(entry.InvoicedInvoiceId);
    }

    [Fact]
    public async Task InvoiceTime_AfterTaskForfait_ExcludesPreviouslyLinkedHours()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedAsync(factory);
        await using var sut = CreateService(factory, fx.Client);

        var forfait = await sut.InvoiceTasksAsync(fx.ProjectId, new InvoiceTasksDto
        {
            Method = "fixed",
            Tasks = new[] { new InvoiceTaskLineDto { TaskId = fx.TaskHourlyId, AmountHt = 900m } }
        });
        Assert.True(forfait.IsSuccess);

        var member = await sut.InvoiceTimeAsync(fx.ProjectId, new InvoiceTimeDto { GroupBy = "member" });
        Assert.True(member.IsFailure);
        Assert.Contains("Aucun temps", member.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetBillableTasks_Hourly_WithPartialMemberInvoicedHours_ShowsRemainingOnly()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedPartialMemberBillingAsync(factory);
        await using var sut = CreateService(factory, fx.Client);

        var tasks = await sut.GetBillableTasksAsync(fx.ProjectId, "hourly");

        var row = Assert.Single(tasks, t => t.Id == fx.TaskHourlyId);
        Assert.True(row.IsEligible);
        Assert.Equal(3m, row.UninvoicedBillableHours);
        Assert.Equal(80m, row.HourlyRate);
        Assert.Equal(240m, row.PreviewAmountHt);
    }

    [Fact]
    public async Task GetBillableTasks_Hourly_WhenAllHoursInvoiced_ExcludesTask()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedAsync(factory);

        await using (var ctx = factory.CreateContext())
        {
            var entry = await ctx.ProjectTimeEntries.FirstAsync(e => e.TaskId == fx.TaskHourlyId);
            entry.MarkInvoiced(Guid.NewGuid());
            await ctx.SaveChangesAsync();
        }

        await using var sut = CreateService(factory, fx.Client);
        var tasks = await sut.GetBillableTasksAsync(fx.ProjectId, "hourly");
        Assert.DoesNotContain(tasks, t => t.Id == fx.TaskHourlyId);
    }

    [Fact]
    public async Task GetBillableTasks_Fixed_StillAvailableWhenSomeHoursInvoicedByMember()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedPartialMemberBillingAsync(factory);
        await using var sut = CreateService(factory, fx.Client);

        var tasks = await sut.GetBillableTasksAsync(fx.ProjectId, "fixed");
        Assert.Contains(tasks, t => t.Id == fx.TaskHourlyId && t.IsEligible);
    }

    [Fact]
    public async Task InvoiceTasks_Hourly_AfterPartialMemberBilling_InvoicesRemainingHoursOnly()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedPartialMemberBillingAsync(factory);
        await using var sut = CreateService(factory, fx.Client);

        var result = await sut.InvoiceTasksAsync(fx.ProjectId, new InvoiceTasksDto
        {
            Method = "hourly",
            Tasks = new[] { new InvoiceTaskLineDto { TaskId = fx.TaskHourlyId } }
        });

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");
        await using var verify = factory.CreateContext();
        var entries = await verify.ProjectTimeEntries.Where(e => e.TaskId == fx.TaskHourlyId).ToListAsync();
        Assert.All(entries, e => Assert.NotNull(e.InvoicedInvoiceId));

        var invoice = await verify.Invoices.Include(i => i.Lines).FirstAsync(i => i.Id == result.Value.InvoiceId);
        var line = Assert.Single(invoice.Lines);
        Assert.Equal(3m, line.Quantity);
    }

    [Fact]
    public async Task GetBillableTasks_Hourly_WeightedRate_Alice2h70_Bob1h130_Returns90()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedWeightedMultiMemberAsync(factory);
        await using var sut = CreateService(factory, fx.Client);

        var tasks = await sut.GetBillableTasksAsync(fx.ProjectId, "hourly");

        var row = Assert.Single(tasks, t => t.Id == fx.TaskHourlyId);
        Assert.True(row.IsEligible);
        Assert.Equal(3m, row.UninvoicedBillableHours);
        Assert.Equal(90m, row.HourlyRate);
        Assert.Equal(270m, row.PreviewAmountHt);
    }

    [Fact]
    public async Task InvoiceTasks_Hourly_UsesCustomHourlyRateFromDto()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedWeightedMultiMemberAsync(factory);
        await using var sut = CreateService(factory, fx.Client);

        var result = await sut.InvoiceTasksAsync(fx.ProjectId, new InvoiceTasksDto
        {
            Method = "hourly",
            Tasks = new[] { new InvoiceTaskLineDto { TaskId = fx.TaskHourlyId, HourlyRate = 100m } }
        });

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");
        await using var verify = factory.CreateContext();
        var invoice = await verify.Invoices.Include(i => i.Lines).FirstAsync(i => i.Id == result.Value.InvoiceId);
        var line = Assert.Single(invoice.Lines);
        Assert.Equal(3m, line.Quantity);
        Assert.Equal(100m, line.UnitPrice.Amount);
    }

    [Fact]
    public async Task InvoiceTasks_Hourly_FallsBackToWeightedWhenNoRateInDto()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var fx = await SeedWeightedMultiMemberAsync(factory);
        await using var sut = CreateService(factory, fx.Client);

        var result = await sut.InvoiceTasksAsync(fx.ProjectId, new InvoiceTasksDto
        {
            Method = "hourly",
            Tasks = new[] { new InvoiceTaskLineDto { TaskId = fx.TaskHourlyId } }
        });

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");
        await using var verify = factory.CreateContext();
        var invoice = await verify.Invoices.Include(i => i.Lines).FirstAsync(i => i.Id == result.Value.InvoiceId);
        var line = Assert.Single(invoice.Lines);
        Assert.Equal(3m, line.Quantity);
        Assert.Equal(90m, line.UnitPrice.Amount);
    }

    private static async Task<BillingFixture> SeedWeightedMultiMemberAsync(InMemoryTenantDbContextFactory factory)
    {
        var address = Address.Create("1 rue Test", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;

        var project = Project.Create(client.Id, "Mission", ProjectKind.Esn, ProjectBillingMode.TimeAndMaterials, null, null, null, 5000m).Value;
        Assert.True(project.Activate().IsSuccess);

        var phase = ProjectPhase.Create(project.Id, "À faire", 0).Value;
        var taskHourly = ProjectTask.Create(project.Id, phase.Id, "Tâche horaire", null, ProjectTaskPriority.Normal, null, null, null, 3m).Value;
        var taskFixed = ProjectTask.Create(project.Id, phase.Id, "Tâche forfait", null, ProjectTaskPriority.Normal, null, null, null, 0m).Value;

        var aliceId = Guid.NewGuid();
        var bobId = Guid.NewGuid();
        var alice = ProjectMember.Create(project.Id, aliceId, ProjectMemberRole.Member, 560m, 70m, 40m).Value;
        var bob = ProjectMember.Create(project.Id, bobId, ProjectMemberRole.Member, 1040m, 130m, 40m).Value;

        var aliceEntry = ProjectTimeEntry.Create(project.Id, aliceId, new DateTime(2026, 8, 1), 2m, true, null, taskHourly.Id).Value;
        Assert.True(aliceEntry.Submit().IsSuccess);
        Assert.True(aliceEntry.Validate().IsSuccess);

        var bobEntry = ProjectTimeEntry.Create(project.Id, bobId, new DateTime(2026, 8, 2), 1m, true, null, taskHourly.Id).Value;
        Assert.True(bobEntry.Submit().IsSuccess);
        Assert.True(bobEntry.Validate().IsSuccess);

        await using (var ctx = factory.CreateContext())
        {
            ctx.Clients.Add(client);
            ctx.Projects.Add(project);
            ctx.ProjectPhases.Add(phase);
            ctx.ProjectTasks.AddRange(taskHourly, taskFixed);
            ctx.ProjectMembers.AddRange(alice, bob);
            ctx.ProjectTimeEntries.AddRange(aliceEntry, bobEntry);
            await ctx.SaveChangesAsync();
        }

        return new BillingFixture(project.Id, taskHourly.Id, taskFixed.Id, client);
    }

    private static async Task<BillingFixture> SeedPartialMemberBillingAsync(InMemoryTenantDbContextFactory factory)
    {
        var address = Address.Create("1 rue Test", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;

        var project = Project.Create(client.Id, "Mission", ProjectKind.Esn, ProjectBillingMode.TimeAndMaterials, null, null, null, 5000m).Value;
        Assert.True(project.Activate().IsSuccess);

        var phase = ProjectPhase.Create(project.Id, "À faire", 0).Value;
        var taskHourly = ProjectTask.Create(project.Id, phase.Id, "Tâche horaire", null, ProjectTaskPriority.Normal, null, null, null, 6m).Value;
        var taskFixed = ProjectTask.Create(project.Id, phase.Id, "Tâche forfait", null, ProjectTaskPriority.Normal, null, null, null, 0m).Value;
        var userId = Guid.NewGuid();
        var member = ProjectMember.Create(project.Id, userId, ProjectMemberRole.Member, 640m, 80m, 40m).Value;

        var invoicedEntry = ProjectTimeEntry.Create(project.Id, userId, new DateTime(2026, 8, 1), 3m, true, null, taskHourly.Id).Value;
        Assert.True(invoicedEntry.Submit().IsSuccess);
        Assert.True(invoicedEntry.Validate().IsSuccess);
        Assert.True(invoicedEntry.MarkInvoiced(Guid.NewGuid()).IsSuccess);

        var openEntry = ProjectTimeEntry.Create(project.Id, userId, new DateTime(2026, 8, 2), 3m, true, null, taskHourly.Id).Value;
        Assert.True(openEntry.Submit().IsSuccess);
        Assert.True(openEntry.Validate().IsSuccess);

        await using (var ctx = factory.CreateContext())
        {
            ctx.Clients.Add(client);
            ctx.Projects.Add(project);
            ctx.ProjectPhases.Add(phase);
            ctx.ProjectTasks.AddRange(taskHourly, taskFixed);
            ctx.ProjectMembers.Add(member);
            ctx.ProjectTimeEntries.AddRange(invoicedEntry, openEntry);
            await ctx.SaveChangesAsync();
        }

        return new BillingFixture(project.Id, taskHourly.Id, taskFixed.Id, client);
    }
}
