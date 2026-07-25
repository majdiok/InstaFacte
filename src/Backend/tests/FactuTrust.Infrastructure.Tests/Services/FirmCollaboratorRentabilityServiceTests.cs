using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using TaxRegime = FactuTrust.Domain.Entities.TaxRegime;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class FirmCollaboratorRentabilityServiceTests
{
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SecondUserId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private static MasterDbContext BuildMaster()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MasterDbContext(options);
    }

    private static FirmCollaboratorRentabilityService BuildService(MasterDbContext db) =>
        new(db, CreateUserManager(db).Object, NullLogger<FirmCollaboratorRentabilityService>.Instance);

    private static Mock<UserManager<ApplicationUser>> CreateUserManager(MasterDbContext db)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mgr = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        mgr.Setup(m => m.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => db.Users.FirstOrDefault(u => u.Id.ToString() == id));
        return mgr;
    }

    private static async Task<(FirmClientAssignment Assignment, PermanentFile File)> SeedAsync(MasterDbContext db)
    {
        db.Users.Add(new ApplicationUser
        {
            Id = UserId,
            UserName = "mgr@test.com",
            Email = "mgr@test.com",
            NormalizedEmail = "MGR@TEST.COM",
            FirstName = "Jean",
            LastName = "Manager",
            TenantId = FirmId,
            IsActive = true
        });

        var nif = NIF.Create("1234567/A/B/C/000").Value;
        var address = Address.Create("1 rue Test", "Tunis", "Tunis").Value;
        var email = Email.Create("co@example.com").Value;
        var phone = PhoneNumber.Create("20123456").Value;
        var company = Tenant.Create("Ste Client", nif, address, email, phone, TaxRegime.RealRegime).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(company, CompanyId);
        db.Tenants.Add(company);

        var firmNif = NIF.Create("7654321/A/B/C/000").Value;
        var firm = Tenant.CreateAccountingFirm("Cabinet", firmNif, address, Email.Create("firm@example.com").Value, phone).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(firm, FirmId);
        db.Tenants.Add(firm);

        var assignment = FirmClientAssignment.Request(CompanyId, FirmId, UserId).Value;
        assignment.Accept(UserId);
        db.FirmClientAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var pf = PermanentFile.Create(assignment.Id, FirmId, CompanyId, "Ste Client").Value;
        pf.UpdateBilling(12_000m, BillingFrequency.Annual, "TND", null);
        pf.AssignAccountant(UserId, "Jean Manager");
        db.PermanentFiles.Add(pf);
        await db.SaveChangesAsync();
        return (assignment, pf);
    }

    /// <summary>Ajoute un utilisateur actif du cabinet, pour les scénarios à plusieurs collaborateurs.</summary>
    private static async Task SeedCollaboratorAsync(
        MasterDbContext db, Guid userId, string firstName, string lastName)
    {
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"{firstName}@test.com",
            Email = $"{firstName}@test.com",
            NormalizedEmail = $"{firstName.ToUpperInvariant()}@TEST.COM",
            FirstName = firstName,
            LastName = lastName,
            TenantId = FirmId,
            IsActive = true
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Sème des heures sur un dossier, réparties sur des journées successives.
    /// </summary>
    /// <remarks>
    /// Une saisie ne peut pas dépasser 24 h — règle du domaine. Le total demandé est donc étalé sur
    /// autant de journées que nécessaire, ce qui permet aux tests de raisonner en volume annuel.
    /// </remarks>
    private static async Task SeedTimeSheetAsync(
        MasterDbContext db, Guid userId, Guid assignmentId, decimal hours, int year = 2026)
    {
        const decimal perDay = 8m;
        var day = new DateTime(year, 6, 1);
        var remaining = hours;

        while (remaining > 0)
        {
            var slice = Math.Min(perDay, remaining);
            db.FirmTimeSheetEntries.Add(FirmTimeSheetEntry.Create(
                FirmId, userId, "Collaborateur", day, slice, assignmentId, "Ste Client").Value);
            remaining -= slice;
            day = day.AddDays(1);
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedYearCostAsync(MasterDbContext db, Guid userId, decimal gross, int year = 2026)
    {
        var cost = FirmCollaboratorYearCost.Create(FirmId, userId, year).Value;
        cost.SetManualCost(gross, 0m, 0m);
        db.FirmCollaboratorYearCosts.Add(cost);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Prefill_gives_the_whole_fee_to_the_only_collaborator_who_worked()
    {
        await using var db = BuildMaster();
        var (assignment, _) = await SeedAsync(db);
        await SeedTimeSheetAsync(db, UserId, assignment.Id, 20m);
        var svc = BuildService(db);

        var result = await svc.GetPrefillAsync(FirmId, UserId, 2026);

        Assert.True(result.IsSuccess);
        Assert.Equal(12_000m, result.Value.CalculatedTotalRevenue);
        Assert.Equal(12_000m, result.Value.TotalRevenue);
        var row = Assert.Single(result.Value.Portfolio);
        Assert.Equal(20m, row.PortfolioHours);
        Assert.Equal(20m, row.TotalDossierHours);
        Assert.Equal(12_000m, row.RevenueShare);
    }

    [Fact]
    public async Task Prefill_yields_no_revenue_without_time_sheets()
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var svc = BuildService(db);

        // Le CA découle désormais des heures : sans saisie, aucun honoraire n'est attribué,
        // même si le collaborateur détient le dossier.
        var result = await svc.GetPrefillAsync(FirmId, UserId, 2026);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Value.CalculatedTotalRevenue);
        Assert.Empty(result.Value.Portfolio);
    }

    [Fact]
    public async Task Fees_are_split_between_collaborators_in_proportion_to_hours()
    {
        await using var db = BuildMaster();
        var (assignment, _) = await SeedAsync(db);
        await SeedCollaboratorAsync(db, SecondUserId, "Sonia", "Trabelsi");
        await SeedTimeSheetAsync(db, UserId, assignment.Id, 60m);
        await SeedTimeSheetAsync(db, SecondUserId, assignment.Id, 40m);
        var svc = BuildService(db);

        var first = await svc.GetPrefillAsync(FirmId, UserId, 2026);
        var second = await svc.GetPrefillAsync(FirmId, SecondUserId, 2026);

        // 12 000 répartis 60 / 40, et la somme reste exactement égale aux honoraires du dossier.
        Assert.Equal(7_200m, first.Value.CalculatedTotalRevenue);
        Assert.Equal(4_800m, second.Value.CalculatedTotalRevenue);
        Assert.Equal(12_000m, first.Value.CalculatedTotalRevenue + second.Value.CalculatedTotalRevenue);
    }

    [Fact]
    public async Task A_collaborator_without_hours_or_dossier_becomes_support()
    {
        await using var db = BuildMaster();
        var (assignment, _) = await SeedAsync(db);
        await SeedCollaboratorAsync(db, SecondUserId, "Sonia", "Trabelsi");
        await SeedTimeSheetAsync(db, UserId, assignment.Id, 50m);
        await SeedYearCostAsync(db, UserId, 40_000m);
        // Sonia ne produit aucune heure et ne détient aucun dossier : son coût est du support.
        await SeedYearCostAsync(db, SecondUserId, 9_000m);
        var svc = BuildService(db);

        var result = await svc.GetPrefillAsync(FirmId, UserId, 2026);

        Assert.True(result.IsSuccess);
        Assert.Equal(9_000m, result.Value.AdminPayrollCharge);
        Assert.Equal(9_000m, result.Value.CalculatedSupportShare);
        // 12 000 − 40 000 − 9 000 = −37 000 : le collaborateur coûte plus qu'il ne produit.
        Assert.Equal(-37_000m, result.Value.Rentability);
    }

    [Fact]
    public async Task Save_persists_authoritative_rentability()
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var svc = BuildService(db);

        var saved = await svc.SaveAsync(FirmId, new SaveFirmCollaboratorRentabilityDto
        {
            CollaboratorUserId = UserId,
            Year = 2026,
            TotalRevenue = 100_000m,
            PayrollCost = 40_000m,
            AdminPayrollCharge = 5_000m,
            ItManagementCharge = 3_000m,
            OperatingCharge = 2_000m,
            CompaniesCount = 1,
            AttachedCollaboratorsCount = 1
        }, null);

        Assert.True(saved.IsSuccess);
        Assert.Equal(50_000m, saved.Value.Rentability);

        var list = await svc.ListAsync(FirmId, 2026, null);
        Assert.Single(list.Items);
        Assert.Equal(50_000m, list.Items[0].Rentability);
    }

    [Fact]
    public async Task Duplicate_skips_existing_target_year()
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var svc = BuildService(db);

        var first = await svc.SaveAsync(FirmId, new SaveFirmCollaboratorRentabilityDto
        {
            CollaboratorUserId = UserId,
            Year = 2026,
            TotalRevenue = 10_000m,
            PayrollCost = 1_000m,
            CompaniesCount = 1,
            AttachedCollaboratorsCount = 1
        }, null);
        Assert.True(first.IsSuccess);
        Assert.NotNull(first.Value.Id);

        await svc.SaveAsync(FirmId, new SaveFirmCollaboratorRentabilityDto
        {
            CollaboratorUserId = UserId,
            Year = 2027,
            TotalRevenue = 11_000m,
            PayrollCost = 1_000m,
            CompaniesCount = 1,
            AttachedCollaboratorsCount = 1
        }, null);

        var dup = await svc.DuplicateAsync(FirmId, new[] { first.Value.Id!.Value });
        Assert.True(dup.IsSuccess);
        Assert.Equal(0, dup.Value.Duplicated);
        Assert.Equal(1, dup.Value.Skipped);
    }


    // ============================================
    // COHÉRENCE LISTE ↔ RENTABILITÉ STOCKÉE
    // ============================================

    [Fact]
    public async Task Payroll_detail_alone_still_weighs_on_the_stored_rentability()
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var svc = BuildService(db);

        // Cas qui produisait un écart silencieux : le détail de paie est renseigné, le total agrégé
        // reste à zéro. La liste affichait alors 40 000 de masse salariale et une rentabilité
        // calculée comme si elle valait zéro.
        var saved = await svc.SaveAsync(FirmId, new SaveFirmCollaboratorRentabilityDto
        {
            CollaboratorUserId = UserId,
            Year = 2026,
            TotalRevenue = 100_000m,
            PayrollCost = 0m,
            AdminPayrollCharge = 5_000m,
            ItManagementCharge = 3_000m,
            OperatingCharge = 2_000m,
            CompaniesCount = 1,
            AttachedCollaboratorsCount = 1,
            PayrollRows = new[]
            {
                new FirmRentabilityPayrollRowDto
                {
                    CollaboratorUserId = UserId,
                    CollaboratorName = "Jean Manager",
                    GrossSalary = 30_000m,
                    EmployerContributions = 8_000m,
                    PayrollExtras = 2_000m
                }
            }
        }, null);

        Assert.True(saved.IsSuccess);

        var list = await svc.ListAsync(FirmId, 2026, null);
        var item = list.Items.Single();

        // 100 000 − 40 000 − 5 000 − 3 000 − 2 000 = 50 000.
        Assert.Equal(40_000m, item.PayrollCost);
        Assert.Equal(50_000m, item.Rentability);
        Assert.Equal(item.TotalRevenue - item.PayrollCost - item.AdminPayrollCharge
                     - item.ItManagementCharge - item.OperatingCharge, item.Rentability);
    }

    [Fact]
    public async Task List_and_detail_agree_on_the_payroll_cost()
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var svc = BuildService(db);

        var saved = await svc.SaveAsync(FirmId, new SaveFirmCollaboratorRentabilityDto
        {
            CollaboratorUserId = UserId,
            Year = 2026,
            TotalRevenue = 80_000m,
            PayrollCost = 0m,
            CompaniesCount = 1,
            AttachedCollaboratorsCount = 1,
            PayrollRows = new[]
            {
                new FirmRentabilityPayrollRowDto
                {
                    CollaboratorUserId = UserId,
                    CollaboratorName = "Jean Manager",
                    GrossSalary = 20_000m,
                    EmployerContributions = 4_000m,
                    PayrollExtras = 0m
                }
            }
        }, null);

        var detail = await svc.GetByIdAsync(FirmId, saved.Value.Id!.Value);
        var list = await svc.ListAsync(FirmId, 2026, null);

        Assert.NotNull(detail);
        Assert.Equal(24_000m, detail!.PayrollCost);
        Assert.Equal(detail.PayrollCost, list.Items.Single().PayrollCost);
        Assert.Equal(detail.Rentability, list.Items.Single().Rentability);
    }

    // ============================================
    // DUPLICATION
    // ============================================

    [Fact]
    public async Task Duplicate_carries_over_the_payroll_breakdown()
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var svc = BuildService(db);

        var source = await svc.SaveAsync(FirmId, new SaveFirmCollaboratorRentabilityDto
        {
            CollaboratorUserId = UserId,
            Year = 2026,
            TotalRevenue = 50_000m,
            PayrollCost = 0m,
            CompaniesCount = 1,
            AttachedCollaboratorsCount = 1,
            PayrollRows = new[]
            {
                new FirmRentabilityPayrollRowDto
                {
                    CollaboratorUserId = UserId,
                    CollaboratorName = "Jean Manager",
                    GrossSalary = 18_000m,
                    EmployerContributions = 3_600m,
                    PayrollExtras = 400m
                }
            }
        }, null);

        var dup = await svc.DuplicateAsync(FirmId, new[] { source.Value.Id!.Value });
        Assert.True(dup.IsSuccess);
        Assert.Equal(1, dup.Value.Duplicated);

        var next = (await svc.ListAsync(FirmId, 2027, null)).Items.Single();
        var nextDetail = await svc.GetByIdAsync(FirmId, next.Id);

        // Ne reporter que l'agrégat perdrait la ventilation par collaborateur.
        Assert.Equal(22_000m, next.PayrollCost);
        Assert.NotNull(nextDetail);
        var row = Assert.Single(nextDetail!.PayrollRows);
        Assert.Equal(18_000m, row.GrossSalary);
        Assert.Equal(3_600m, row.EmployerContributions);
        Assert.Equal(400m, row.PayrollExtras);
    }

    // ============================================
    // PORTEFEUILLE
    // ============================================

    [Fact]
    public async Task Prefill_excludes_a_resigned_mission_from_the_revenue()
    {
        await using var db = BuildMaster();
        var (assignment, file) = await SeedAsync(db);
        await SeedTimeSheetAsync(db, UserId, assignment.Id, 20m);
        var svc = BuildService(db);

        var before = await svc.GetPrefillAsync(FirmId, UserId, 2026);
        Assert.Equal(12_000m, before.Value.CalculatedTotalRevenue);

        file.UpdateLegalStatus(null, null, false, missionResigned: true, 2026, "Résiliation");
        await db.SaveChangesAsync();

        var after = await svc.GetPrefillAsync(FirmId, UserId, 2026);

        // Un dossier dont la mission est résiliée ne produit plus d'honoraires.
        Assert.Equal(0m, after.Value.CalculatedTotalRevenue);
        Assert.Empty(after.Value.Portfolio);
    }

    // ============================================
    // RECALCUL GLOBAL
    // ============================================

    [Fact]
    public async Task Recalculation_preserves_the_previous_value_and_is_idempotent()
    {
        await using var db = BuildMaster();
        var (assignment, _) = await SeedAsync(db);
        await SeedTimeSheetAsync(db, UserId, assignment.Id, 20m);
        await SeedYearCostAsync(db, UserId, 5_000m);
        var svc = BuildService(db);

        var saved = await svc.SaveAsync(FirmId, new SaveFirmCollaboratorRentabilityDto
        {
            CollaboratorUserId = UserId,
            Year = 2026,
            TotalRevenue = 99_999m,
            PayrollCost = 1_000m,
            CompaniesCount = 1,
            AttachedCollaboratorsCount = 1
        }, null);
        var originalMargin = saved.Value.Rentability;

        var first = await svc.RecalculateAllAsync(FirmId, isManager: true);
        Assert.True(first.IsSuccess);
        Assert.Equal(1, first.Value.Recalculated);

        var entity = await db.Set<FirmCollaboratorRentability>()
            .FirstAsync(r => r.Id == saved.Value.Id!.Value);
        Assert.Equal(originalMargin, entity.LegacyRentability);
        Assert.NotNull(entity.RecalculatedAt);

        // Le CA vient désormais des heures : 12 000 et non les 99 999 saisis.
        var afterFirst = entity.Rentability;
        Assert.Equal(12_000m, entity.GetTotal(FirmRentabilityReference.TotalRevenue));

        var second = await svc.RecalculateAllAsync(FirmId, isManager: true);
        Assert.True(second.IsSuccess);

        await db.Entry(entity).ReloadAsync();
        // Rejouer ne doit ni changer le résultat ni écraser la valeur d'origine.
        Assert.Equal(afterFirst, entity.Rentability);
        Assert.Equal(originalMargin, entity.LegacyRentability);
    }

    [Fact]
    public async Task Recalculation_leaves_an_exercise_without_time_sheets_untouched()
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var svc = BuildService(db);

        var saved = await svc.SaveAsync(FirmId, new SaveFirmCollaboratorRentabilityDto
        {
            CollaboratorUserId = UserId,
            Year = 2026,
            TotalRevenue = 80_000m,
            PayrollCost = 30_000m,
            CompaniesCount = 1,
            AttachedCollaboratorsCount = 1
        }, null);

        var result = await svc.RecalculateAllAsync(FirmId, isManager: true);

        // Sans feuille de temps, recalculer ramènerait le CA à zéro et détruirait un snapshot valide.
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Recalculated);
        var skipped = Assert.Single(result.Value.Skipped);
        Assert.Equal(2026, skipped.Year);
        Assert.Contains("Aucune feuille de temps", skipped.Reason);

        var entity = await db.Set<FirmCollaboratorRentability>()
            .FirstAsync(r => r.Id == saved.Value.Id!.Value);
        Assert.Equal(50_000m, entity.Rentability);
        Assert.Null(entity.LegacyRentability);
        Assert.Null(entity.RecalculatedAt);
    }

    [Fact]
    public async Task An_accountant_cannot_trigger_the_recalculation()
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var svc = BuildService(db);

        var result = await svc.RecalculateAllAsync(FirmId, isManager: false);

        Assert.True(result.IsFailure);
    }

    // ============================================
    // RECOUVREMENT
    // ============================================

    [Fact]
    public async Task Unrecovered_fees_lower_the_collected_rentability_only()
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var svc = BuildService(db);

        await svc.SaveAsync(FirmId, new SaveFirmCollaboratorRentabilityDto
        {
            CollaboratorUserId = UserId,
            Year = 2026,
            TotalRevenue = 100_000m,
            PayrollCost = 40_000m,
            ClientDebitBalance = 25_000m,
            CompaniesCount = 1,
            AttachedCollaboratorsCount = 1
        }, null);

        var item = (await svc.ListAsync(FirmId, 2026, null)).Items.Single();

        // La formule Décisiel reste intacte ; le recouvrement s'exprime à côté, pas dedans.
        Assert.Equal(60_000m, item.Rentability);
        Assert.Equal(35_000m, item.CollectedRentability);
        Assert.Equal(75m, item.RecoveryRatePercent);
    }
}
