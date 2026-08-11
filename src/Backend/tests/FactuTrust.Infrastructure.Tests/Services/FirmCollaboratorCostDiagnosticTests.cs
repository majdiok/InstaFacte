using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// L'écran des coûts collaborateurs affichait des zéros sans jamais dire pourquoi. Ces tests
/// verrouillent le diagnostic par ligne : chaque état vide doit nommer son obstacle.
/// </summary>
public sealed class FirmCollaboratorCostDiagnosticTests
{
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CollaboratorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid PayrollEmployeeId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private const int Year = 2026;

    private static MasterDbContext BuildMaster() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task SeedCollaboratorAsync(MasterDbContext db, bool linkedToPayroll)
    {
        db.Users.Add(new ApplicationUser
        {
            Id = CollaboratorId,
            TenantId = FirmId,
            IsActive = true,
            FirstName = "Amine",
            LastName = "Ben Salah",
            Email = "amine@cabinet.tn",
            UserName = "amine@cabinet.tn"
        });

        if (linkedToPayroll)
        {
            db.FirmCollaboratorProfiles.Add(new FirmCollaboratorProfile
            {
                UserId = CollaboratorId,
                Qualification = string.Empty,
                PayrollEmployeeId = PayrollEmployeeId,
                PayrollLinkSource = FirmPayrollLinkSource.Manual,
                PayrollLinkedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }

    private static FirmCollaboratorCostService BuildService(
        MasterDbContext db,
        FirmPayrollCostSnapshotDto snapshot)
    {
        var provider = new Mock<IFirmPayrollCostProvider>();
        provider
            .Setup(p => p.GetAnnualEmployerCostsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var options = Options.Create(new FirmGovernanceOptions { Enabled = true, DefaultHourlyCostRate = 50m });
        return new FirmCollaboratorCostService(db, provider.Object, new FirmLeaveAbsenceReader(db), options);
    }

    [Fact]
    public async Task No_cost_row_reports_no_data_instead_of_a_manual_entry()
    {
        await using var db = BuildMaster();
        await SeedCollaboratorAsync(db, linkedToPayroll: false);
        var service = BuildService(db, FirmPayrollCostSnapshotDto.Unavailable("Aucune base de paie n'est rattachée au cabinet."));

        var row = Assert.Single(await service.ListAsync(FirmId, Year));

        // Avant : Source = 1 (« Saisi ») sur une ligne qui n'a jamais été saisie.
        Assert.Equal((int)FirmPayrollCostSource.None, row.Source);
        Assert.Equal("Aucune donnée", row.SourceDisplay);
        Assert.Equal(0m, row.TotalEmployerCost);
    }

    [Fact]
    public async Task Unlinked_collaborator_is_diagnosed_as_missing_a_payroll_link()
    {
        await using var db = BuildMaster();
        await SeedCollaboratorAsync(db, linkedToPayroll: false);
        var service = BuildService(db, FirmPayrollCostSnapshotDto.Unavailable("Aucune paie validée ou clôturée pour l'exercice 2026."));

        var row = Assert.Single(await service.ListAsync(FirmId, Year));

        // L'absence de liaison prime : c'est le seul obstacle levable sans attendre un cycle de paie.
        Assert.Equal((int)FirmCollaboratorCostDiagnostic.NoPayrollLink, row.CostDiagnostic);
        Assert.Equal("Non lié à la paie", row.CostDiagnosticDisplay);
        Assert.Contains("Rattachez", row.CostDiagnosticHint!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Linked_collaborator_with_unreadable_payroll_is_not_reported_as_unlinked()
    {
        await using var db = BuildMaster();
        await SeedCollaboratorAsync(db, linkedToPayroll: true);
        var service = BuildService(db, FirmPayrollCostSnapshotDto.Unavailable(
            "La paie du cabinet n'a pas pu être interrogée. Saisissez le coût employeur manuellement."));

        var row = Assert.Single(await service.ListAsync(FirmId, Year));

        Assert.Equal((int)FirmCollaboratorCostDiagnostic.PayrollUnreadable, row.CostDiagnostic);
        Assert.NotNull(row.PayrollEmployeeId);
        Assert.Equal((int)FirmPayrollLinkSource.Manual, row.PayrollLinkSource);
        // Le motif du fournisseur est relayé tel quel : c'est lui qui porte l'information utile.
        Assert.Contains("n'a pas pu être interrogée", row.CostDiagnosticHint!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Linked_collaborator_without_validated_run_is_diagnosed_accordingly()
    {
        await using var db = BuildMaster();
        await SeedCollaboratorAsync(db, linkedToPayroll: true);
        var service = BuildService(db, FirmPayrollCostSnapshotDto.Unavailable(
            $"Aucune paie validée ou clôturée pour l'exercice {Year}."));

        var row = Assert.Single(await service.ListAsync(FirmId, Year));

        Assert.Equal((int)FirmCollaboratorCostDiagnostic.NoValidatedRun, row.CostDiagnostic);
        Assert.Equal("Aucune paie arrêtée", row.CostDiagnosticDisplay);
    }

    [Fact]
    public async Task Linked_collaborator_absent_from_an_available_snapshot_has_no_payslip()
    {
        await using var db = BuildMaster();
        await SeedCollaboratorAsync(db, linkedToPayroll: true);
        var otherEmployee = new FirmPayrollEmployeeCostDto(
            Guid.NewGuid(), "Sonia Trabelsi", 30_000m, 5_871m, 12);
        var service = BuildService(db, FirmPayrollCostSnapshotDto.Available(new[] { otherEmployee }, 1));

        var row = Assert.Single(await service.ListAsync(FirmId, Year));

        Assert.Equal((int)FirmCollaboratorCostDiagnostic.LinkedWithoutPayslip, row.CostDiagnostic);
    }

    [Fact]
    public async Task Manual_entry_is_diagnosed_as_such_and_warns_about_forced_import()
    {
        await using var db = BuildMaster();
        await SeedCollaboratorAsync(db, linkedToPayroll: true);
        var service = BuildService(db, FirmPayrollCostSnapshotDto.Unavailable("Aucune base de paie."));

        await service.SaveAsync(
            FirmId, isManager: true, CollaboratorId, Year,
            new SaveFirmCollaboratorYearCostDto { GrossAnnualSalary = 30_000m });

        var row = Assert.Single(await service.ListAsync(FirmId, Year));

        Assert.Equal((int)FirmPayrollCostSource.Manual, row.Source);
        Assert.Equal((int)FirmCollaboratorCostDiagnostic.ManualEntry, row.CostDiagnostic);
        Assert.Contains("forcer", row.CostDiagnosticHint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Batch_linking_refuses_to_attach_one_payroll_employee_to_two_collaborators()
    {
        // Sans ce garde-fou, le coût du salarié serait compté deux fois dans la rentabilité.
        await using var db = BuildMaster();
        await SeedCollaboratorAsync(db, linkedToPayroll: false);
        var secondCollaborator = Guid.Parse("44444444-4444-4444-4444-444444444444");
        db.Users.Add(new ApplicationUser
        {
            Id = secondCollaborator,
            TenantId = FirmId,
            IsActive = true,
            FirstName = "Sonia",
            LastName = "Trabelsi",
            Email = "sonia@cabinet.tn",
            UserName = "sonia@cabinet.tn"
        });
        await db.SaveChangesAsync();

        var service = BuildService(db, FirmPayrollCostSnapshotDto.Unavailable("Aucune base de paie."));

        var result = await service.LinkPayrollEmployeesAsync(
            FirmId,
            isManager: true,
            new[]
            {
                new FirmPayrollEmployeeLinkRequest(CollaboratorId, PayrollEmployeeId),
                new FirmPayrollEmployeeLinkRequest(secondCollaborator, PayrollEmployeeId)
            },
            FirmPayrollLinkSource.ProvisionedFromCollaborator);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Linked);
        Assert.Single(result.Value.Messages);
        Assert.Equal(1, await db.FirmCollaboratorProfiles.CountAsync(p => p.PayrollEmployeeId == PayrollEmployeeId));
    }

    [Fact]
    public async Task Batch_linking_is_refused_for_a_non_manager()
    {
        await using var db = BuildMaster();
        await SeedCollaboratorAsync(db, linkedToPayroll: false);
        var service = BuildService(db, FirmPayrollCostSnapshotDto.Unavailable("Aucune base de paie."));

        var result = await service.LinkPayrollEmployeesAsync(
            FirmId,
            isManager: false,
            new[] { new FirmPayrollEmployeeLinkRequest(CollaboratorId, PayrollEmployeeId) },
            FirmPayrollLinkSource.Manual);

        Assert.True(result.IsFailure);
        Assert.False(await db.FirmCollaboratorProfiles.AnyAsync());
    }

    [Fact]
    public async Task Imported_cost_reports_no_pending_action()
    {
        await using var db = BuildMaster();
        await SeedCollaboratorAsync(db, linkedToPayroll: true);
        var employee = new FirmPayrollEmployeeCostDto(
            PayrollEmployeeId, "Amine Ben Salah", 30_000m, 5_871m, 12);
        var service = BuildService(db, FirmPayrollCostSnapshotDto.Available(new[] { employee }, 1));

        await service.ImportFromPayrollAsync(FirmId, isManager: true, Year);

        var row = Assert.Single(await service.ListAsync(FirmId, Year));

        Assert.Equal((int)FirmPayrollCostSource.ImportedFromPayroll, row.Source);
        Assert.Equal((int)FirmCollaboratorCostDiagnostic.Ok, row.CostDiagnostic);
        Assert.Null(row.CostDiagnosticHint);
        Assert.Equal(35_871m, row.TotalEmployerCost);
    }
}
