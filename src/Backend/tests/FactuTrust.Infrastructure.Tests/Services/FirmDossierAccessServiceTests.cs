using FactuTrust.Application.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class FirmDossierAccessServiceTests
{
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CompanyA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CompanyB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid CompanyAwaiting = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid AssignmentA = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    private static readonly Guid AssignmentB = Guid.Parse("b1111111-1111-1111-1111-111111111111");
    private static readonly Guid AssignmentAwaiting = Guid.Parse("c1111111-1111-1111-1111-111111111111");
    private static readonly Guid AccountantA = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid AccountantB = Guid.Parse("bbbbbbbb-1111-1111-1111-111111111111");
    private static readonly Guid ManagerId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static MasterDbContext BuildMaster()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MasterDbContext(options);
    }

    private static async Task SeedAsync(MasterDbContext db)
    {
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        var address = Address.Create("1 rue Test", "Tunis", "Tunis").Value;
        var email = Email.Create("co@example.com").Value;
        var phone = PhoneNumber.Create("20123456").Value;

        var firm = Tenant.CreateAccountingFirm("Cabinet Test", nif, address, email, phone).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(firm, FirmId);

        var companyA = Tenant.Create("Société A", nif, address, email, phone, TaxRegime.RealRegime).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(companyA, CompanyA);
        var companyB = Tenant.Create("Société B", nif, address, email, phone, TaxRegime.RealRegime).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(companyB, CompanyB);
        var companyC = Tenant.Create("Société C", nif, address, email, phone, TaxRegime.RealRegime).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(companyC, CompanyAwaiting);

        db.Tenants.AddRange(firm, companyA, companyB, companyC);

        var aA = FirmClientAssignment.Request(CompanyA, FirmId, ManagerId).Value;
        typeof(FirmClientAssignment).GetProperty(nameof(FirmClientAssignment.Id))!.SetValue(aA, AssignmentA);
        aA.Accept(ManagerId);

        var aB = FirmClientAssignment.Request(CompanyB, FirmId, ManagerId).Value;
        typeof(FirmClientAssignment).GetProperty(nameof(FirmClientAssignment.Id))!.SetValue(aB, AssignmentB);
        aB.Accept(ManagerId);

        var aC = FirmClientAssignment.Request(CompanyAwaiting, FirmId, ManagerId).Value;
        typeof(FirmClientAssignment).GetProperty(nameof(FirmClientAssignment.Id))!.SetValue(aC, AssignmentAwaiting);
        aC.Accept(ManagerId);

        db.FirmClientAssignments.AddRange(aA, aB, aC);

        var pfA = PermanentFile.Create(AssignmentA, FirmId, CompanyA, "Société A").Value;
        pfA.AssignAccountant(AccountantA, "Accountant A");
        var pfB = PermanentFile.Create(AssignmentB, FirmId, CompanyB, "Société B").Value;
        pfB.AssignAccountant(AccountantB, "Accountant B");
        var pfC = PermanentFile.Create(AssignmentAwaiting, FirmId, CompanyAwaiting, "Société C").Value;

        db.PermanentFiles.AddRange(pfA, pfB, pfC);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task FirmManager_can_access_unassigned_and_assigned_dossiers()
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var svc = new FirmDossierAccessService(db);
        var scope = FirmDossierAccessScope.ForUser(ManagerId, UserRole.FirmManager);

        Assert.True(await svc.CanAccessClientDossierAsync(FirmId, scope, CompanyAwaiting));
        Assert.True(await svc.CanAccessClientDossierAsync(FirmId, scope, CompanyA));
        Assert.True(await svc.CanAccessAssignmentAsync(FirmId, scope, AssignmentB));
        Assert.Null(await svc.GetAccessibleCompanyTenantIdsAsync(FirmId, scope));
    }

    [Fact]
    public async Task FirmAccountant_can_only_access_own_assigned_dossier()
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var svc = new FirmDossierAccessService(db);
        var scope = FirmDossierAccessScope.ForUser(AccountantA, UserRole.FirmAccountant);

        Assert.True(await svc.CanAccessClientDossierAsync(FirmId, scope, CompanyA));
        Assert.False(await svc.CanAccessClientDossierAsync(FirmId, scope, CompanyB));
        Assert.False(await svc.CanAccessClientDossierAsync(FirmId, scope, CompanyAwaiting));
        Assert.True(await svc.CanAccessAssignmentAsync(FirmId, scope, AssignmentA));
        Assert.False(await svc.CanAccessAssignmentAsync(FirmId, scope, AssignmentB));
        Assert.False(await svc.CanAccessAssignmentAsync(FirmId, scope, AssignmentAwaiting));

        var companies = await svc.GetAccessibleCompanyTenantIdsAsync(FirmId, scope);
        Assert.NotNull(companies);
        Assert.Equal(new HashSet<Guid> { CompanyA }, companies);
    }

    [Fact]
    public async Task FirmAccountant_denied_when_assignment_revoked()
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var assignment = await db.FirmClientAssignments.SingleAsync(a => a.Id == AssignmentA);
        assignment.RevokeByFirm(ManagerId);
        await db.SaveChangesAsync();

        var svc = new FirmDossierAccessService(db);
        var scope = FirmDossierAccessScope.ForUser(AccountantA, UserRole.FirmAccountant);
        Assert.False(await svc.CanAccessClientDossierAsync(FirmId, scope, CompanyA));
    }
}
