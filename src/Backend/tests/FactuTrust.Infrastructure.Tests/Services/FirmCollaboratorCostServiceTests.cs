using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Services.FirmGovernance;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class FirmCollaboratorCostServiceTests
{
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CollaboratorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OtherCollaboratorId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid PayrollEmployeeId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private const int Year = 2026;

    private static MasterDbContext BuildMaster() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task SeedCollaboratorsAsync(MasterDbContext db)
    {
        db.Users.AddRange(
            new ApplicationUser
            {
                Id = CollaboratorId,
                TenantId = FirmId,
                IsActive = true,
                FirstName = "Amine",
                LastName = "Ben Salah",
                Email = "amine@cabinet.tn",
                UserName = "amine@cabinet.tn"
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

    private static FirmCollaboratorCostService BuildService(
        MasterDbContext db,
        FirmPayrollCostSnapshotDto? payrollSnapshot = null)
    {
        var provider = new Mock<IFirmPayrollCostProvider>();
        provider
            .Setup(p => p.GetAnnualEmployerCostsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(payrollSnapshot
                          ?? FirmPayrollCostSnapshotDto.Unavailable("Aucune base de paie n'est rattachée au cabinet."));

        var options = Options.Create(new FirmGovernanceOptions { Enabled = true, DefaultHourlyCostRate = 50m });
        return new FirmCollaboratorCostService(db, provider.Object, options);
    }

    // ============================================
    // SAISIE MANUELLE
    // ============================================

    [Fact]
    public async Task Employer_contributions_are_derived_when_not_supplied()
    {
        await using var db = BuildMaster();
        await SeedCollaboratorsAsync(db);
        var service = BuildService(db);

        var saved = await service.SaveAsync(
            FirmId, isManager: true, CollaboratorId, Year,
            new SaveFirmCollaboratorYearCostDto { GrossAnnualSalary = 30_000m });

        // 16,57 % CNSS + 2 % TFP + 1 % FOPROLOS = 19,57 % → 5 871,000.
        Assert.True(saved.IsSuccess);
        Assert.Equal(5_871m, saved.Value.EmployerContributions);
        Assert.Equal(35_871m, saved.Value.TotalEmployerCost);
    }

    [Fact]
    public async Task Supplied_employer_contributions_are_kept_as_is()
    {
        await using var db = BuildMaster();
        await SeedCollaboratorsAsync(db);
        var service = BuildService(db);

        var saved = await service.SaveAsync(
            FirmId, isManager: true, CollaboratorId, Year,
            new SaveFirmCollaboratorYearCostDto
            {
                GrossAnnualSalary = 30_000m,
                EmployerContributions = 6_500m
            });

        Assert.Equal(6_500m, saved.Value.EmployerContributions);
    }

    [Fact]
    public async Task Saving_a_cost_replaces_the_default_hourly_rate()
    {
        await using var db = BuildMaster();
        await SeedCollaboratorsAsync(db);
        var service = BuildService(db);

        var before = await service.ListAsync(FirmId, Year);
        Assert.All(before, r => Assert.Equal((int)FirmHourlyRateSource.FirmDefault, r.HourlyRateSource));
        Assert.All(before, r => Assert.Equal(50m, r.EffectiveHourlyRate));

        await service.SaveAsync(
            FirmId, isManager: true, CollaboratorId, Year,
            new SaveFirmCollaboratorYearCostDto { GrossAnnualSalary = 30_000m });

        var after = await service.ListAsync(FirmId, Year);
        var updated = after.Single(r => r.CollaboratorUserId == CollaboratorId);
        var untouched = after.Single(r => r.CollaboratorUserId == OtherCollaboratorId);

        Assert.Equal((int)FirmHourlyRateSource.Derived, updated.HourlyRateSource);
        Assert.NotEqual(50m, updated.EffectiveHourlyRate);
        Assert.Contains("÷", updated.HourlyRateBasis);
        // Le collaborateur non renseigné reste au taux par défaut, sans contamination.
        Assert.Equal((int)FirmHourlyRateSource.FirmDefault, untouched.HourlyRateSource);
    }

    [Fact]
    public async Task An_override_without_justification_is_refused()
    {
        await using var db = BuildMaster();
        await SeedCollaboratorsAsync(db);
        var service = BuildService(db);

        var result = await service.SaveAsync(
            FirmId, isManager: true, CollaboratorId, Year,
            new SaveFirmCollaboratorYearCostDto
            {
                GrossAnnualSalary = 30_000m,
                HourlyRateOverride = 80m,
                OverrideJustification = "   "
            });

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Costs_of_two_years_are_independent()
    {
        await using var db = BuildMaster();
        await SeedCollaboratorsAsync(db);
        var service = BuildService(db);

        await service.SaveAsync(
            FirmId, isManager: true, CollaboratorId, 2025,
            new SaveFirmCollaboratorYearCostDto { GrossAnnualSalary = 24_000m });
        await service.SaveAsync(
            FirmId, isManager: true, CollaboratorId, 2026,
            new SaveFirmCollaboratorYearCostDto { GrossAnnualSalary = 30_000m });

        var y2025 = (await service.ListAsync(FirmId, 2025)).Single(r => r.CollaboratorUserId == CollaboratorId);
        var y2026 = (await service.ListAsync(FirmId, 2026)).Single(r => r.CollaboratorUserId == CollaboratorId);

        // Un taux 2026 ne doit jamais réécrire l'historique 2025.
        Assert.Equal(24_000m, y2025.GrossAnnualSalary);
        Assert.Equal(30_000m, y2026.GrossAnnualSalary);
        Assert.NotEqual(y2025.EffectiveHourlyRate, y2026.EffectiveHourlyRate);
    }

    [Fact]
    public async Task An_accountant_cannot_change_a_cost()
    {
        await using var db = BuildMaster();
        await SeedCollaboratorsAsync(db);
        var service = BuildService(db);

        var result = await service.SaveAsync(
            FirmId, isManager: false, CollaboratorId, Year,
            new SaveFirmCollaboratorYearCostDto { GrossAnnualSalary = 30_000m });

        Assert.True(result.IsFailure);
    }

    // ============================================
    // IMPORT DEPUIS LA PAIE DU CABINET
    // ============================================

    [Fact]
    public async Task Import_reports_unavailability_without_failing()
    {
        await using var db = BuildMaster();
        await SeedCollaboratorsAsync(db);
        var service = BuildService(db);

        var result = await service.ImportFromPayrollAsync(FirmId, isManager: true, Year);

        // Base de paie absente : ce n'est pas une erreur, l'écran doit rester utilisable.
        Assert.True(result.IsSuccess);
        Assert.False(result.Value.PayrollAvailable);
        Assert.Equal(0, result.Value.Imported);
        Assert.NotNull(result.Value.UnavailableReason);
    }

    [Fact]
    public async Task Import_only_touches_explicitly_linked_collaborators()
    {
        await using var db = BuildMaster();
        await SeedCollaboratorsAsync(db);
        var snapshot = FirmPayrollCostSnapshotDto.Available(new[]
        {
            new FirmPayrollEmployeeCostDto(PayrollEmployeeId, "Amine Ben Salah", 30_000m, 5_871m, 12)
        });
        var service = BuildService(db, snapshot);

        await service.LinkPayrollEmployeeAsync(FirmId, isManager: true, CollaboratorId, PayrollEmployeeId);
        var result = await service.ImportFromPayrollAsync(FirmId, isManager: true, Year);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.PayrollAvailable);
        Assert.Equal(1, result.Value.Imported);
        Assert.Equal(1, result.Value.Unlinked);

        var rows = await service.ListAsync(FirmId, Year);
        var imported = rows.Single(r => r.CollaboratorUserId == CollaboratorId);
        var untouched = rows.Single(r => r.CollaboratorUserId == OtherCollaboratorId);

        Assert.Equal(35_871m, imported.TotalEmployerCost);
        Assert.Equal((int)FirmPayrollCostSource.ImportedFromPayroll, imported.Source);
        Assert.NotNull(imported.ImportedAt);
        // Sans liaison, aucun coût n'est imputé : un rapprochement par nom confondrait les homonymes.
        Assert.Equal(0m, untouched.TotalEmployerCost);
    }

    [Fact]
    public async Task Import_preserves_manually_entered_extras()
    {
        await using var db = BuildMaster();
        await SeedCollaboratorsAsync(db);
        var snapshot = FirmPayrollCostSnapshotDto.Available(new[]
        {
            new FirmPayrollEmployeeCostDto(PayrollEmployeeId, "Amine Ben Salah", 30_000m, 5_871m, 12)
        });
        var service = BuildService(db, snapshot);

        await service.SaveAsync(
            FirmId, isManager: true, CollaboratorId, Year,
            new SaveFirmCollaboratorYearCostDto { GrossAnnualSalary = 1m, PayrollExtras = 2_000m });
        await service.LinkPayrollEmployeeAsync(FirmId, isManager: true, CollaboratorId, PayrollEmployeeId);
        await service.ImportFromPayrollAsync(FirmId, isManager: true, Year);

        var row = (await service.ListAsync(FirmId, Year)).Single(r => r.CollaboratorUserId == CollaboratorId);

        // L'import ne connaît que ce qui figure au bulletin : les extras saisis ne doivent pas disparaître.
        Assert.Equal(2_000m, row.PayrollExtras);
        Assert.Equal(30_000m, row.GrossAnnualSalary);
        Assert.Equal(37_871m, row.TotalEmployerCost);
    }

    [Fact]
    public async Task A_payroll_employee_cannot_be_linked_twice()
    {
        await using var db = BuildMaster();
        await SeedCollaboratorsAsync(db);
        var service = BuildService(db);

        var first = await service.LinkPayrollEmployeeAsync(
            FirmId, isManager: true, CollaboratorId, PayrollEmployeeId);
        var second = await service.LinkPayrollEmployeeAsync(
            FirmId, isManager: true, OtherCollaboratorId, PayrollEmployeeId);

        // Sinon le même coût serait compté deux fois dans la rentabilité du cabinet.
        Assert.True(first.IsSuccess);
        Assert.True(second.IsFailure);
    }

    [Fact]
    public async Task Unlinking_a_payroll_employee_frees_it_for_another_collaborator()
    {
        await using var db = BuildMaster();
        await SeedCollaboratorsAsync(db);
        var service = BuildService(db);

        await service.LinkPayrollEmployeeAsync(FirmId, isManager: true, CollaboratorId, PayrollEmployeeId);
        await service.LinkPayrollEmployeeAsync(FirmId, isManager: true, CollaboratorId, null);
        var reassigned = await service.LinkPayrollEmployeeAsync(
            FirmId, isManager: true, OtherCollaboratorId, PayrollEmployeeId);

        Assert.True(reassigned.IsSuccess);
    }

    [Fact]
    public async Task An_accountant_cannot_import_payroll_costs()
    {
        await using var db = BuildMaster();
        await SeedCollaboratorsAsync(db);
        var service = BuildService(db);

        var result = await service.ImportFromPayrollAsync(FirmId, isManager: false, Year);

        Assert.True(result.IsFailure);
    }
}
