using System.Security.Claims;
using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Cycle de vie d'un dossier client géré par le cabinet (sans compte plateforme) :
/// basculement de contexte comptabilité et résiliation.
/// </summary>
public sealed class FirmManagedClientLifecycleTests
{
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ManagedClientId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid FirmUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static MasterDbContext BuildMaster()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MasterDbContext(options);
    }

    private static async Task SeedFirmManagedClientAsync(MasterDbContext db)
    {
        var address = Address.Create("1 rue Test", "Tunis", "Tunis").Value;

        var firm = Tenant.CreateAccountingFirm(
            "Cabinet Test",
            NIF.Create("7654321/A/B/C/000").Value,
            address,
            Email.Create("cabinet@example.com").Value,
            PhoneNumber.Create("71123456").Value).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(firm, FirmId);

        var managed = Tenant.CreateFirmManaged(
            FirmId,
            "Client Géré SARL",
            NIF.Create("1234567/A/B/C/000").Value,
            address,
            Email.Create("client@example.com").Value,
            PhoneNumber.Create("73123456").Value,
            TaxRegime.RealRegime).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(managed, ManagedClientId);

        var assignment = FirmClientAssignment.CreateByFirm(ManagedClientId, FirmId, FirmUserId).Value;

        db.Tenants.AddRange(firm, managed);
        db.FirmClientAssignments.Add(assignment);
        await db.SaveChangesAsync();
    }

    private static FirmAssignmentService BuildAssignmentService(MasterDbContext db)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        currentUser.SetupGet(u => u.Role).Returns(UserRole.FirmManager);

        return new FirmAssignmentService(
            db,
            new Mock<INotificationService>().Object,
            new Mock<ICompanyProfileSnapshotProvider>().Object,
            new Mock<IFirmDossierAccessService>().Object,
            currentUser.Object,
            NullLogger<FirmAssignmentService>.Instance);
    }

    [Fact]
    public async Task SwitchToClient_succeeds_for_managed_client_without_platform_users()
    {
        await using var db = BuildMaster();
        await SeedFirmManagedClientAsync(db);

        var assignmentService = BuildAssignmentService(db);

        var dossierAccess = new Mock<IFirmDossierAccessService>();
        dossierAccess
            .Setup(a => a.CanAccessClientDossierAsync(FirmId, It.IsAny<FirmDossierAccessScope>(), ManagedClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(u => u.Role).Returns(UserRole.FirmManager);

        var expected = new AuthResponseDto { AccessToken = "token", RefreshToken = "refresh" };
        var tokenService = new Mock<ITenantAuthTokenService>();
        tokenService
            .Setup(t => t.GenerateTokensAsync(FirmUserId, FirmId, ManagedClientId, "Client Géré SARL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var contextService = new FirmContextService(
            db,
            assignmentService,
            dossierAccess.Object,
            currentUser.Object,
            tokenService.Object);

        var response = await contextService.SwitchToClientAsync(FirmUserId, FirmId, ManagedClientId);

        Assert.Same(expected, response);
        tokenService.Verify(
            t => t.GenerateTokensAsync(FirmUserId, FirmId, ManagedClientId, "Client Géré SARL", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetCurrentContext_delegated_resolves_IsFirmManaged_from_master()
    {
        await using var db = BuildMaster();
        await SeedFirmManagedClientAsync(db);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(u => u.TenantId).Returns(FirmId);
        currentUser.SetupGet(u => u.Role).Returns(UserRole.FirmManager);

        var contextService = new FirmContextService(
            db,
            BuildAssignmentService(db),
            new Mock<IFirmDossierAccessService>().Object,
            currentUser.Object,
            new Mock<ITenantAuthTokenService>().Object);

        var identity = new ClaimsIdentity([
            new Claim(AuthClaimTypes.AccessMode, "delegated"),
            new Claim(AuthClaimTypes.ContextTenantId, ManagedClientId.ToString()),
            new Claim(AuthClaimTypes.ContextCompanyName, "Client Géré SARL")
        ], "test");

        var dto = await contextService.GetCurrentContextAsync(new ClaimsPrincipal(identity));

        Assert.Equal("delegated", dto.AccessMode);
        Assert.Equal(ManagedClientId, dto.ClientTenantId);
        Assert.True(dto.IsFirmManaged);
    }

    [Fact]
    public async Task GetCurrentContext_uses_jwt_claim_when_present()
    {
        await using var db = BuildMaster();
        await SeedFirmManagedClientAsync(db);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(u => u.TenantId).Returns(FirmId);

        var contextService = new FirmContextService(
            db,
            BuildAssignmentService(db),
            new Mock<IFirmDossierAccessService>().Object,
            currentUser.Object,
            new Mock<ITenantAuthTokenService>().Object);

        var identity = new ClaimsIdentity([
            new Claim(AuthClaimTypes.AccessMode, "delegated"),
            new Claim(AuthClaimTypes.ContextTenantId, ManagedClientId.ToString()),
            new Claim(AuthClaimTypes.IsFirmManaged, "false")
        ], "test");

        var dto = await contextService.GetCurrentContextAsync(new ClaimsPrincipal(identity));

        Assert.False(dto.IsFirmManaged);
    }

    [Fact]
    public async Task RevokeByFirm_deactivates_managed_tenant()
    {
        await using var db = BuildMaster();
        await SeedFirmManagedClientAsync(db);

        var service = BuildAssignmentService(db);
        var assignment = await db.FirmClientAssignments.SingleAsync();

        var result = await service.RevokeByFirmAsync(FirmId, assignment.Id, FirmUserId);

        Assert.True(result.IsSuccess);
        var tenant = await db.Tenants.SingleAsync(t => t.Id == ManagedClientId);
        Assert.False(tenant.IsActive);
        Assert.Equal(FirmAssignmentStatus.RevokedByFirm, assignment.Status);
    }

    [Fact]
    public async Task RevokeByFirm_keeps_regular_client_tenant_active()
    {
        await using var db = BuildMaster();

        var address = Address.Create("1 rue Test", "Tunis", "Tunis").Value;
        var firm = Tenant.CreateAccountingFirm(
            "Cabinet Test",
            NIF.Create("7654321/A/B/C/000").Value,
            address,
            Email.Create("cabinet@example.com").Value,
            PhoneNumber.Create("71123456").Value).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(firm, FirmId);

        var company = Tenant.Create(
            "Société Autonome",
            NIF.Create("1234567/A/B/C/000").Value,
            address,
            Email.Create("societe@example.com").Value,
            PhoneNumber.Create("70123456").Value,
            TaxRegime.RealRegime).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(company, ManagedClientId);

        var assignment = FirmClientAssignment.Request(ManagedClientId, FirmId, FirmUserId).Value;
        assignment.Accept(FirmUserId);

        db.Tenants.AddRange(firm, company);
        db.FirmClientAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var service = BuildAssignmentService(db);

        var result = await service.RevokeByFirmAsync(FirmId, assignment.Id, FirmUserId);

        Assert.True(result.IsSuccess);
        var tenant = await db.Tenants.SingleAsync(t => t.Id == ManagedClientId);
        Assert.True(tenant.IsActive);
    }
}
