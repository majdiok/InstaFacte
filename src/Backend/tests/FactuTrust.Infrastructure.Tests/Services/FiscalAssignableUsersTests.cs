using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Features.Accounting.Queries;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Échéancier fiscal — picker « Responsable » : la source renvoie les utilisateurs ACTIFS du
/// tenant demandé (en mode délégué : les collaborateurs du cabinet, pas ceux du dossier client) ;
/// la méthode historique ListActiveAsync reste une simple délégation (CRM non impacté).
/// </summary>
public sealed class FiscalAssignableUsersTests
{
    private static MasterDbContext BuildMaster()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MasterDbContext(options);
    }

    private static ApplicationUser User(Guid tenantId, string first, string last, bool active = true, string? email = null)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FirstName = first,
            LastName = last,
            IsActive = active,
            UserName = email ?? $"{first}.{last}@test.tn".ToLowerInvariant(),
            Email = email ?? $"{first}.{last}@test.tn".ToLowerInvariant()
        };

    [Fact]
    public async Task ListActiveForTenant_ReturnsOnlyActiveUsersOfRequestedTenant_Sorted()
    {
        var cabinetId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        await using var db = BuildMaster();
        db.Users.AddRange(
            User(cabinetId, "Amine", "Zorgati"),
            User(cabinetId, "Sonia", "Ben Ali"),
            User(cabinetId, "Inactif", "Cabinet", active: false),
            User(clientId, "Karim", "Client"));
        await db.SaveChangesAsync();

        var source = new AssignableTenantUsersSource(db, Mock.Of<ITenantContext>());

        var users = await source.ListActiveForTenantAsync(cabinetId);

        Assert.Equal(2, users.Count);
        // Tri Nom puis Prénom : Ben Ali avant Zorgati ; l'inactif et l'utilisateur du client sont exclus.
        Assert.Equal("Sonia Ben Ali", users[0].DisplayName);
        Assert.Equal("Amine Zorgati", users[1].DisplayName);
    }

    [Fact]
    public async Task ListActiveForTenant_FallsBackToEmail_WhenNameEmpty()
    {
        var tenantId = Guid.NewGuid();
        await using var db = BuildMaster();
        db.Users.Add(User(tenantId, "", "", email: "compta@cabinet.tn"));
        await db.SaveChangesAsync();

        var source = new AssignableTenantUsersSource(db, Mock.Of<ITenantContext>());

        var users = await source.ListActiveForTenantAsync(tenantId);

        Assert.Equal("compta@cabinet.tn", Assert.Single(users).DisplayName);
    }

    [Fact]
    public async Task ListActiveAsync_DelegatesToCurrentTenant_UnchangedBehavior()
    {
        var tenantId = Guid.NewGuid();
        await using var db = BuildMaster();
        db.Users.Add(User(tenantId, "Nadia", "Trabelsi"));
        await db.SaveChangesAsync();

        var tenantContext = new Mock<ITenantContext>();
        tenantContext.SetupGet(c => c.TenantId).Returns(tenantId);
        var source = new AssignableTenantUsersSource(db, tenantContext.Object);

        var viaCurrent = await source.ListActiveAsync();
        var viaExplicit = await source.ListActiveForTenantAsync(tenantId);

        Assert.Equal(viaExplicit.Select(u => u.Id), viaCurrent.Select(u => u.Id));
    }

    [Fact]
    public async Task ListActiveAsync_WithoutTenantContext_ReturnsEmpty()
    {
        await using var db = BuildMaster();
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.SetupGet(c => c.TenantId).Returns((Guid?)null);
        var source = new AssignableTenantUsersSource(db, tenantContext.Object);

        Assert.Empty(await source.ListActiveAsync());
    }

    [Fact]
    public async Task QueryHandler_ReturnsUsers_AndRejectsEmptyTenant()
    {
        var cabinetId = Guid.NewGuid();
        await using var db = BuildMaster();
        db.Users.Add(User(cabinetId, "Sami", "Gharbi"));
        await db.SaveChangesAsync();
        var handler = new GetFiscalAssignableUsersQueryHandler(
            new AssignableTenantUsersSource(db, Mock.Of<ITenantContext>()));

        var ok = await handler.Handle(new GetFiscalAssignableUsersQuery(cabinetId), CancellationToken.None);
        Assert.True(ok.IsSuccess);
        Assert.Single(ok.Value);

        var bad = await handler.Handle(new GetFiscalAssignableUsersQuery(Guid.Empty), CancellationToken.None);
        Assert.True(bad.IsFailure);
    }
}
