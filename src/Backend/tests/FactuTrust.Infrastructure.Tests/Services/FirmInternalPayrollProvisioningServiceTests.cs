using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class FirmInternalPayrollProvisioningServiceTests
{
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CollaboratorId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static MasterDbContext BuildMaster() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task SeedFirmWithCollaboratorAsync(MasterDbContext db)
    {
        var address = Address.Create("1 rue Test", "Tunis", "Tunis").Value;
        var firm = Tenant.CreateAccountingFirm(
            "Cabinet Test",
            NIF.Create("7654321/A/B/C/000").Value,
            address,
            Email.Create("firm@cabinet.tn").Value,
            PhoneNumber.Create("20123456").Value).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(firm, FirmId);
        db.Tenants.Add(firm);
        db.Users.Add(new ApplicationUser
        {
            Id = CollaboratorId,
            TenantId = FirmId,
            IsActive = true,
            FirstName = "Ahmed",
            LastName = "Boudaya",
            Email = "ahmed@cabinet.tn",
            UserName = "ahmed@cabinet.tn"
        });
        await db.SaveChangesAsync();
    }

    private sealed class InMemoryTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly string _databaseName;

        public InMemoryTenantDbContextFactory(string databaseName) => _databaseName = databaseName;

        public TenantDbContext CreateContext() => CreateIsolatedContext();

        public TenantDbContext CreateIsolatedContext() =>
            new(new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(_databaseName)
                .Options);
    }

    private static FirmInternalPayrollProvisioningService BuildService(
        MasterDbContext db,
        ITenantDbContextFactory? tenantDbContextFactory = null,
        IFirmCollaboratorCostService? costs = null,
        bool internalPayrollEnabled = true,
        string? tenantConnectionString = null)
    {
        var tenantService = new Mock<ITenantService>();
        tenantService
            .Setup(t => t.GetConnectionStringAsync(FirmId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantConnectionString);

        var costService = costs ?? new Mock<IFirmCollaboratorCostService>().Object;
        var tenantFactory = tenantDbContextFactory ?? new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());

        return new FirmInternalPayrollProvisioningService(
            db,
            new FirmTenantPayrollAccessor(tenantService.Object, tenantFactory),
            costService,
            Options.Create(new FirmGovernanceOptions
            {
                Enabled = true,
                EnableFirmInternalPayroll = internalPayrollEnabled
            }),
            NullLogger<FirmInternalPayrollProvisioningService>.Instance);
    }

    [Fact]
    public async Task GetProvisioningStatus_flag_off_reports_disabled()
    {
        await using var db = BuildMaster();
        await SeedFirmWithCollaboratorAsync(db);
        var service = BuildService(db, internalPayrollEnabled: false);

        var status = await service.GetProvisioningStatusAsync(FirmId);

        Assert.False(status.InternalPayrollEnabled);
        Assert.Single(status.Collaborators);
    }

    [Fact]
    public async Task ProvisionFromCollaborators_fails_when_feature_disabled()
    {
        await using var db = BuildMaster();
        await SeedFirmWithCollaboratorAsync(db);
        var service = BuildService(db, internalPayrollEnabled: false);

        var result = await service.ProvisionFromCollaboratorsAsync(FirmId, isManager: true);

        Assert.True(result.IsFailure);
        Assert.Contains("pas activée", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProvisionFromCollaborators_fails_for_non_manager()
    {
        await using var db = BuildMaster();
        await SeedFirmWithCollaboratorAsync(db);
        var service = BuildService(db);

        var result = await service.ProvisionFromCollaboratorsAsync(FirmId, isManager: false);

        Assert.True(result.IsFailure);
        Assert.Contains("manager", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProvisionCollaborator_creates_employee_without_querying_unsaved_row()
    {
        await using var db = BuildMaster();
        await SeedFirmWithCollaboratorAsync(db);

        var tenantDbName = Guid.NewGuid().ToString();
        var tenantFactory = new InMemoryTenantDbContextFactory(tenantDbName);

        var costs = BuildCostServiceAcceptingLinks();

        var service = BuildService(
            db,
            tenantFactory,
            costs.Object,
            tenantConnectionString: "InMemory");

        var result = await service.ProvisionCollaboratorAsync(FirmId, isManager: true, CollaboratorId);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");
        Assert.Equal(1, result.Value.Created);
        Assert.Equal(0, result.Value.Linked);

        await using var tenant = tenantFactory.CreateContext();
        var employee = await tenant.Set<Employee>().SingleAsync();
        Assert.StartsWith("CAB-", employee.EmployeeNumber, StringComparison.Ordinal);
        Assert.Equal("ahmed@cabinet.tn", employee.Email!.Value, StringComparer.OrdinalIgnoreCase);

        costs.Verify(c => c.LinkPayrollEmployeesAsync(
            FirmId,
            true,
            It.Is<IReadOnlyList<FirmPayrollEmployeeLinkRequest>>(l =>
                l.Count == 1
                && l[0].CollaboratorUserId == CollaboratorId
                && l[0].PayrollEmployeeId == employee.Id),
            FirmPayrollLinkSource.ProvisionedFromCollaborator,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProvisionCollaborator_with_onboarding_uses_custom_employee_number_and_contract()
    {
        await using var db = BuildMaster();
        await SeedFirmWithCollaboratorAsync(db);

        var tenantDbName = Guid.NewGuid().ToString();
        var tenantFactory = new InMemoryTenantDbContextFactory(tenantDbName);
        var costs = BuildCostServiceAcceptingLinks();
        var service = BuildService(
            db,
            tenantFactory,
            costs.Object,
            tenantConnectionString: "InMemory");

        var onboarding = new FirmCollaboratorPayrollOnboardingDto
        {
            EmployeeNumber = "MAT-ONBOARD-001",
            HireDate = new DateTime(2024, 3, 15),
            Contract = new CreateContractDto
            {
                Type = "Cdi",
                Regime = "Rsna",
                WeeklyRegime = "FortyEightHours",
                StartDate = new DateTime(2024, 3, 15),
                BaseSalary = 2200m,
                WorkAccidentRate = 0.5m,
                JobTitle = "Comptable senior"
            }
        };

        var result = await service.ProvisionCollaboratorAsync(FirmId, isManager: true, CollaboratorId, onboarding);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");
        Assert.Equal(1, result.Value.Created);

        await using var tenant = tenantFactory.CreateContext();
        var employee = await tenant.Set<Employee>().SingleAsync();
        Assert.Equal("MAT-ONBOARD-001", employee.EmployeeNumber);
        Assert.Equal(new DateTime(2024, 3, 15).Date, employee.HireDate.Date);
        Assert.Equal("Ahmed", employee.FirstName);
        Assert.Equal("Boudaya", employee.LastName);

        var contract = await tenant.Set<EmploymentContract>().SingleAsync();
        Assert.Equal(2200m, contract.BaseSalary);
        Assert.Equal(0.5m, contract.WorkAccidentRate);
        Assert.Equal("Comptable senior", contract.JobTitle);
    }

    [Fact]
    public async Task Links_are_written_only_after_the_payroll_database_accepted_the_employees()
    {
        // Le défaut corrigé : les liaisons Master étaient enregistrées au fil de la boucle alors
        // que les salariés n'étaient poussés dans la base de paie qu'à la fin. Un échec de ce
        // dernier enregistrement laissait des profils pointant vers des salariés inexistants.
        await using var db = BuildMaster();
        await SeedFirmWithCollaboratorAsync(db);

        var costs = BuildCostServiceAcceptingLinks();
        var service = BuildService(
            db,
            new FailingTenantDbContextFactory(),
            costs.Object,
            tenantConnectionString: "InMemory");

        var result = await service.ProvisionCollaboratorAsync(FirmId, isManager: true, CollaboratorId);

        Assert.True(result.IsFailure);
        Assert.Contains("Aucune liaison", result.Error.Description, StringComparison.OrdinalIgnoreCase);

        // Aucune liaison ne doit avoir été tentée : la paie n'a rien accepté.
        costs.Verify(c => c.LinkPayrollEmployeesAsync(
            It.IsAny<Guid>(),
            It.IsAny<bool>(),
            It.IsAny<IReadOnlyList<FirmPayrollEmployeeLinkRequest>>(),
            It.IsAny<FirmPayrollLinkSource>(),
            It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(await db.FirmCollaboratorProfiles.AnyAsync(p => p.PayrollEmployeeId != null));
    }

    [Fact]
    public async Task Refused_links_are_not_counted_as_provisioned()
    {
        // Un salarié déjà rattaché à un autre collaborateur est refusé par la pose en lot :
        // les compteurs ne doivent pas annoncer une liaison qui n'existe pas.
        await using var db = BuildMaster();
        await SeedFirmWithCollaboratorAsync(db);

        var costs = new Mock<IFirmCollaboratorCostService>();
        costs.Setup(c => c.LinkPayrollEmployeesAsync(
                It.IsAny<Guid>(),
                It.IsAny<bool>(),
                It.IsAny<IReadOnlyList<FirmPayrollEmployeeLinkRequest>>(),
                It.IsAny<FirmPayrollLinkSource>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new FirmPayrollLinkBatchResultDto
            {
                Linked = 0,
                Messages = new[] { "Un salarié paie est déjà lié à un autre collaborateur : liaison ignorée." }
            }));

        var service = BuildService(
            db,
            new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString()),
            costs.Object,
            tenantConnectionString: "InMemory");

        var result = await service.ProvisionCollaboratorAsync(FirmId, isManager: true, CollaboratorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Created);
        Assert.Equal(0, result.Value.Linked);
        Assert.Equal(1, result.Value.Skipped);
        Assert.Contains(result.Value.Messages, m => m.Contains("déjà lié", StringComparison.OrdinalIgnoreCase));
    }

    private static Mock<IFirmCollaboratorCostService> BuildCostServiceAcceptingLinks()
    {
        var costs = new Mock<IFirmCollaboratorCostService>();
        costs.Setup(c => c.LinkPayrollEmployeesAsync(
                It.IsAny<Guid>(),
                It.IsAny<bool>(),
                It.IsAny<IReadOnlyList<FirmPayrollEmployeeLinkRequest>>(),
                It.IsAny<FirmPayrollLinkSource>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, bool _, IReadOnlyList<FirmPayrollEmployeeLinkRequest> links, FirmPayrollLinkSource _, CancellationToken _) =>
                Result.Success(new FirmPayrollLinkBatchResultDto { Linked = links.Count }));
        return costs;
    }

    /// <summary>Base de paie qui refuse toute écriture — simule une indisponibilité au moment du flush.</summary>
    private sealed class FailingTenantDbContextFactory : ITenantDbContextFactory
    {
        public TenantDbContext CreateContext() => CreateIsolatedContext();

        public TenantDbContext CreateIsolatedContext() => new FailingTenantDbContext(
            new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        public TenantDbContext CreateIsolatedContext(string connectionString) => CreateIsolatedContext();
    }

    private sealed class FailingTenantDbContext : TenantDbContext
    {
        public FailingTenantDbContext(DbContextOptions<TenantDbContext> options) : base(options) { }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Base de paie injoignable.");
    }
}
