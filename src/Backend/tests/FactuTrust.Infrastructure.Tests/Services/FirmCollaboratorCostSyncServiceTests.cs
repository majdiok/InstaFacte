using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Persistence;
using TaxRegime = FactuTrust.Domain.Entities.TaxRegime;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class FirmCollaboratorCostSyncServiceTests
{
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CollaboratorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OtherCollaboratorId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid PayrollEmployeeId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid OtherPayrollEmployeeId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private const int Year = 2026;
    private const string SharedEmail = "amine@cabinet.tn";

    private static MasterDbContext BuildMaster() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task SeedFirmAsync(MasterDbContext db)
    {
        var address = Address.Create("1 rue Test", "Tunis", "Tunis").Value;
        var phone = PhoneNumber.Create("20123456").Value;
        var firm = Tenant.CreateAccountingFirm(
            "Cabinet Test",
            NIF.Create("7654321/A/B/C/000").Value,
            address,
            Email.Create("firm@cabinet.tn").Value,
            phone).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(firm, FirmId);
        db.Tenants.Add(firm);
        db.Users.AddRange(
            new ApplicationUser
            {
                Id = CollaboratorId,
                TenantId = FirmId,
                IsActive = true,
                FirstName = "Amine",
                LastName = "Ben Salah",
                Email = SharedEmail,
                UserName = SharedEmail
            },
            new ApplicationUser
            {
                Id = OtherCollaboratorId,
                TenantId = FirmId,
                IsActive = true,
                FirstName = "Sonia",
                LastName = "Trabelsi",
                Email = "sonia@cabinet.tn",
                UserName = "sonia@cabinet.tn"
            });
        await db.SaveChangesAsync();
    }

    private static FirmCollaboratorCostSyncService BuildSyncService(
        MasterDbContext db,
        FirmPayrollCostSnapshotDto? snapshot = null,
        IReadOnlyList<FirmPayrollEmployeeLinkDto>? linkEmployees = null,
        DateTime? latestActivity = null)
    {
        var costService = new FirmCollaboratorCostService(
            db,
            BuildPayrollProvider(snapshot, linkEmployees, latestActivity),
            new FirmLeaveAbsenceReader(db),
            Options.Create(new FirmGovernanceOptions
            {
                Enabled = true,
                DefaultHourlyCostRate = 50m,
                AutoLinkCollaboratorsByEmail = true
            }));

        return new FirmCollaboratorCostSyncService(
            db,
            costService,
            BuildPayrollProvider(snapshot, linkEmployees, latestActivity),
            Options.Create(new FirmGovernanceOptions
            {
                Enabled = true,
                AutoLinkCollaboratorsByEmail = true
            }),
            NullLogger<FirmCollaboratorCostSyncService>.Instance);
    }

    private static IFirmPayrollCostProvider BuildPayrollProvider(
        FirmPayrollCostSnapshotDto? snapshot,
        IReadOnlyList<FirmPayrollEmployeeLinkDto>? linkEmployees,
        DateTime? latestActivity)
    {
        var provider = new Mock<IFirmPayrollCostProvider>();
        provider
            .Setup(p => p.GetAnnualEmployerCostsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot ?? FirmPayrollCostSnapshotDto.Unavailable("Paie indisponible"));
        provider
            .Setup(p => p.GetActiveEmployeesForLinkingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(linkEmployees ?? Array.Empty<FirmPayrollEmployeeLinkDto>());
        provider
            .Setup(p => p.GetLatestPayrollActivityAtAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestActivity);
        return provider.Object;
    }

    [Fact]
    public async Task Auto_link_by_unique_email()
    {
        await using var db = BuildMaster();
        await SeedFirmAsync(db);

        var snapshot = FirmPayrollCostSnapshotDto.Available(new[]
        {
            new FirmPayrollEmployeeCostDto(PayrollEmployeeId, "Amine Ben Salah", 30_000m, 5_871m, 12)
        }, 1);
        var links = new[] { new FirmPayrollEmployeeLinkDto(PayrollEmployeeId, SharedEmail, "Amine Ben Salah") };
        var sync = BuildSyncService(db, snapshot, links, DateTime.UtcNow);

        var result = await sync.EnsureFreshAsync(FirmId, Year, FirmCostSyncTrigger.ManualSync);

        Assert.True(result.PayrollAvailable);
        Assert.Equal(1, result.LinkedByEmail);
        Assert.Equal(1, result.Imported);

        var profile = await db.FirmCollaboratorProfiles.SingleAsync(p => p.UserId == CollaboratorId);
        Assert.Equal(PayrollEmployeeId, profile.PayrollEmployeeId);
        Assert.Equal(FirmPayrollLinkSource.AutoEmail, profile.PayrollLinkSource);
        Assert.NotNull(profile.PayrollLinkedAt);
    }

    [Fact]
    public async Task Auto_link_runs_even_when_no_payroll_has_been_validated_yet()
    {
        // Mise en service d'un cabinet : des salariés existent, aucun cycle n'a encore été arrêté.
        // La liaison doit malgré tout s'établir, sinon aucun bulletin ne pourra jamais être rattaché.
        await using var db = BuildMaster();
        await SeedFirmAsync(db);

        var links = new[] { new FirmPayrollEmployeeLinkDto(PayrollEmployeeId, SharedEmail, "Amine Ben Salah") };
        var sync = BuildSyncService(
            db,
            FirmPayrollCostSnapshotDto.Unavailable($"Aucune paie validée ou clôturée pour l'exercice {Year}."),
            links);

        var result = await sync.EnsureFreshAsync(FirmId, Year, FirmCostSyncTrigger.ManualSync);

        Assert.False(result.PayrollAvailable);
        Assert.Equal(1, result.LinkedByEmail);
        Assert.Equal(0, result.Imported);

        var profile = await db.FirmCollaboratorProfiles.SingleAsync(p => p.UserId == CollaboratorId);
        Assert.Equal(PayrollEmployeeId, profile.PayrollEmployeeId);
        Assert.Equal(FirmPayrollLinkSource.AutoEmail, profile.PayrollLinkSource);
    }

    [Fact]
    public async Task Non_accounting_firm_is_rejected_before_any_linking()
    {
        // Le garde-fou « tenant cabinet » doit rester en tête : on ne touche pas aux profils
        // d'un tenant qui n'est pas un cabinet, même pour les lier.
        await using var db = BuildMaster();

        var links = new[] { new FirmPayrollEmployeeLinkDto(PayrollEmployeeId, SharedEmail, "Amine Ben Salah") };
        var sync = BuildSyncService(db, null, links);

        var result = await sync.EnsureFreshAsync(FirmId, Year, FirmCostSyncTrigger.ManualSync);

        Assert.False(result.PayrollAvailable);
        Assert.Equal(0, result.LinkedByEmail);
        Assert.Contains("cabinet comptable", result.UnavailableReason!, StringComparison.OrdinalIgnoreCase);
        Assert.False(await db.FirmCollaboratorProfiles.AnyAsync());
    }

    [Fact]
    public async Task Skip_ambiguous_email_when_two_employees_share_it()
    {
        await using var db = BuildMaster();
        await SeedFirmAsync(db);

        var links = new[]
        {
            new FirmPayrollEmployeeLinkDto(PayrollEmployeeId, SharedEmail, "Amine A"),
            new FirmPayrollEmployeeLinkDto(OtherPayrollEmployeeId, SharedEmail, "Amine B")
        };
        var sync = BuildSyncService(
            db,
            FirmPayrollCostSnapshotDto.Available(Array.Empty<FirmPayrollEmployeeCostDto>(), 0),
            links);

        var result = await sync.EnsureFreshAsync(FirmId, Year, FirmCostSyncTrigger.ManualSync);

        Assert.Equal(0, result.LinkedByEmail);
        Assert.False(await db.FirmCollaboratorProfiles.AnyAsync());
    }

    [Fact]
    public async Task Auto_import_skips_manual_rows()
    {
        await using var db = BuildMaster();
        await SeedFirmAsync(db);

        var profile = new FirmCollaboratorProfile
        {
            UserId = CollaboratorId,
            Qualification = string.Empty,
            PayrollEmployeeId = PayrollEmployeeId,
            PayrollLinkSource = FirmPayrollLinkSource.Manual
        };
        db.FirmCollaboratorProfiles.Add(profile);
        var manual = FirmCollaboratorYearCost.Create(FirmId, CollaboratorId, Year).Value;
        manual.SetManualCost(10_000m, 1_000m, 0m);
        db.FirmCollaboratorYearCosts.Add(manual);
        await db.SaveChangesAsync();

        var snapshot = FirmPayrollCostSnapshotDto.Available(new[]
        {
            new FirmPayrollEmployeeCostDto(PayrollEmployeeId, "Amine", 30_000m, 5_871m, 12)
        }, 1);
        var sync = BuildSyncService(db, snapshot, null, DateTime.UtcNow);

        var result = await sync.EnsureFreshAsync(FirmId, Year, FirmCostSyncTrigger.ManualSync, forceImport: false);

        Assert.Equal(0, result.Imported);
        Assert.Equal(1, result.SkippedManual);
        Assert.Equal(10_000m, (await db.FirmCollaboratorYearCosts.SingleAsync()).GrossAnnualSalary);
    }

    [Fact]
    public async Task Force_import_overwrites_manual_rows()
    {
        await using var db = BuildMaster();
        await SeedFirmAsync(db);

        db.FirmCollaboratorProfiles.Add(new FirmCollaboratorProfile
        {
            UserId = CollaboratorId,
            Qualification = string.Empty,
            PayrollEmployeeId = PayrollEmployeeId,
            PayrollLinkSource = FirmPayrollLinkSource.Manual
        });
        var manual = FirmCollaboratorYearCost.Create(FirmId, CollaboratorId, Year).Value;
        manual.SetManualCost(10_000m, 1_000m, 0m);
        db.FirmCollaboratorYearCosts.Add(manual);
        await db.SaveChangesAsync();

        var snapshot = FirmPayrollCostSnapshotDto.Available(new[]
        {
            new FirmPayrollEmployeeCostDto(PayrollEmployeeId, "Amine", 30_000m, 5_871m, 12)
        }, 1);
        var sync = BuildSyncService(db, snapshot, null, DateTime.UtcNow);

        var result = await sync.EnsureFreshAsync(FirmId, Year, FirmCostSyncTrigger.ManualImport, forceImport: true);

        Assert.Equal(1, result.Imported);
        var cost = await db.FirmCollaboratorYearCosts.SingleAsync();
        Assert.Equal(30_000m, cost.GrossAnnualSalary);
        Assert.Equal(FirmPayrollCostSource.ImportedFromPayroll, cost.Source);
    }

    [Fact]
    public async Task Manual_link_sets_manual_source()
    {
        await using var db = BuildMaster();
        await SeedFirmAsync(db);
        var service = new FirmCollaboratorCostService(
            db,
            BuildPayrollProvider(null, null, null),
            new FirmLeaveAbsenceReader(db),
            Options.Create(new FirmGovernanceOptions { Enabled = true }));

        var linked = await service.LinkPayrollEmployeeAsync(FirmId, true, CollaboratorId, PayrollEmployeeId);
        Assert.True(linked.IsSuccess);

        var profile = await db.FirmCollaboratorProfiles.SingleAsync();
        Assert.Equal(FirmPayrollLinkSource.Manual, profile.PayrollLinkSource);
        Assert.NotNull(profile.PayrollLinkedAt);
    }

    [Fact]
    public async Task Stale_import_refreshed_after_new_payroll()
    {
        await using var db = BuildMaster();
        await SeedFirmAsync(db);

        db.FirmCollaboratorProfiles.Add(new FirmCollaboratorProfile
        {
            UserId = CollaboratorId,
            Qualification = string.Empty,
            PayrollEmployeeId = PayrollEmployeeId,
            PayrollLinkSource = FirmPayrollLinkSource.AutoEmail
        });
        var imported = FirmCollaboratorYearCost.Create(FirmId, CollaboratorId, Year).Value;
        imported.SetImportedCost(20_000m, 4_000m, 0m);
        typeof(FirmCollaboratorYearCost).GetProperty(nameof(FirmCollaboratorYearCost.ImportedAt))!
            .SetValue(imported, DateTime.UtcNow.AddDays(-30));
        db.FirmCollaboratorYearCosts.Add(imported);
        await db.SaveChangesAsync();

        var snapshot = FirmPayrollCostSnapshotDto.Available(new[]
        {
            new FirmPayrollEmployeeCostDto(PayrollEmployeeId, "Amine", 30_000m, 5_871m, 12)
        }, 1);
        var latestActivity = DateTime.UtcNow;
        var sync = BuildSyncService(db, snapshot, null, latestActivity);

        var result = await sync.EnsureFreshAsync(
            FirmId, Year, FirmCostSyncTrigger.RentabilityPrefill, forceImport: false);

        Assert.Equal(1, result.Imported);
        var cost = await db.FirmCollaboratorYearCosts.SingleAsync();
        Assert.Equal(30_000m, cost.GrossAnnualSalary);
    }

    [Fact]
    public async Task Non_accounting_firm_returns_unavailable()
    {
        await using var db = BuildMaster();
        var companyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var address = Address.Create("1 rue Test", "Tunis", "Tunis").Value;
        var phone = PhoneNumber.Create("20123456").Value;
        var company = Tenant.Create(
            "Société",
            NIF.Create("1234567/A/B/C/000").Value,
            address,
            Email.Create("co@example.com").Value,
            phone,
            TaxRegime.RealRegime).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(company, companyId);
        db.Tenants.Add(company);
        await db.SaveChangesAsync();

        var sync = BuildSyncService(db);
        var result = await sync.EnsureFreshAsync(companyId, Year, FirmCostSyncTrigger.ManualSync);

        Assert.False(result.PayrollAvailable);
        Assert.Contains("cabinet", result.UnavailableReason!, StringComparison.OrdinalIgnoreCase);
    }
}
