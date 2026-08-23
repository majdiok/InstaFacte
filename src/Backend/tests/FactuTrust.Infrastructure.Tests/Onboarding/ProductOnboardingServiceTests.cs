using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ProductOnboarding;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Onboarding;

public sealed class ProductOnboardingServiceTests
{
    [Fact]
    public async Task GetMine_without_user_returns_completed_disabled_payload()
    {
        await using var db = BuildDb();
        var current = new Mock<ICurrentUser>();
        current.Setup(c => c.UserId).Returns((Guid?)null);
        var service = BuildService(db, current.Object, enabled: true);

        var dto = await service.GetMineAsync(CancellationToken.None);

        Assert.False(dto.Enabled);
        Assert.Equal(ProductOnboardingStatus.Completed, dto.Status);
    }

    [Fact]
    public async Task Existing_user_without_seed_reads_completed()
    {
        await using var db = BuildDb();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "old@test.c",
            UserName = "old@test.c",
            FirstName = "Old",
            LastName = "User",
            TenantId = Guid.NewGuid()
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var current = new Mock<ICurrentUser>();
        current.Setup(c => c.UserId).Returns(user.Id);
        var service = BuildService(db, current.Object);

        var dto = await service.GetMineAsync(CancellationToken.None);

        Assert.True(dto.Enabled);
        Assert.Equal(ProductOnboardingStatus.Completed, dto.Status);
    }

    [Fact]
    public async Task New_interactive_user_can_be_marked_completed()
    {
        await using var db = BuildDb();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "new@test.c",
            UserName = "new@test.c",
            FirstName = "New",
            LastName = "User",
            TenantId = Guid.NewGuid()
        };
        user.ApplyNewInteractiveProductOnboarding();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var current = new Mock<ICurrentUser>();
        current.Setup(c => c.UserId).Returns(user.Id);
        var service = BuildService(db, current.Object);

        var patched = await service.PatchMineAsync(
            new PatchProductOnboardingRequest { Status = ProductOnboardingStatus.Completed },
            CancellationToken.None);

        Assert.True(patched.IsSuccess);
        Assert.Equal(ProductOnboardingStatus.Completed, patched.Value.Status);
        Assert.Equal(ProductOnboardingStatus.Completed, (await db.Users.FindAsync(user.Id))!.ProductOnboardingStatus);
    }

    [Fact]
    public async Task Patch_checklist_done_id_persists()
    {
        await using var db = BuildDb();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "new2@test.c",
            UserName = "new2@test.c",
            FirstName = "New",
            LastName = "User",
            TenantId = Guid.NewGuid()
        };
        user.ApplyNewInteractiveProductOnboarding();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var current = new Mock<ICurrentUser>();
        current.Setup(c => c.UserId).Returns(user.Id);
        var service = BuildService(db, current.Object);

        var patched = await service.PatchMineAsync(
            new PatchProductOnboardingRequest
            {
                ChecklistDoneId = ProductOnboardingDefaults.CompanyItemIds.CreateClient,
                ChecklistDismissed = true
            },
            CancellationToken.None);

        Assert.True(patched.IsSuccess);
        Assert.True(patched.Value.Checklist.Dismissed);
        Assert.Contains(ProductOnboardingDefaults.CompanyItemIds.CreateClient, patched.Value.Checklist.DoneIds);
    }

    [Fact]
    public async Task Patch_does_not_modify_another_user()
    {
        await using var db = BuildDb();
        var alice = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "alice@test.c",
            UserName = "alice@test.c",
            FirstName = "Alice",
            LastName = "User",
            TenantId = Guid.NewGuid()
        };
        alice.ApplyNewInteractiveProductOnboarding();
        var bob = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "bob@test.c",
            UserName = "bob@test.c",
            FirstName = "Bob",
            LastName = "User",
            TenantId = alice.TenantId
        };
        bob.ApplyNewInteractiveProductOnboarding();
        db.Users.AddRange(alice, bob);
        await db.SaveChangesAsync();

        var current = new Mock<ICurrentUser>();
        current.Setup(c => c.UserId).Returns(alice.Id);
        var service = BuildService(db, current.Object);

        var patched = await service.PatchMineAsync(
            new PatchProductOnboardingRequest { Status = ProductOnboardingStatus.Dismissed },
            CancellationToken.None);

        Assert.True(patched.IsSuccess);
        Assert.Equal(ProductOnboardingStatus.Dismissed, (await db.Users.FindAsync(alice.Id))!.ProductOnboardingStatus);
        Assert.Equal(ProductOnboardingStatus.NotStarted, (await db.Users.FindAsync(bob.Id))!.ProductOnboardingStatus);
    }

    [Fact]
    public async Task Kill_switch_returns_enabled_false_without_changing_status()
    {
        await using var db = BuildDb();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "new3@test.c",
            UserName = "new3@test.c",
            FirstName = "New",
            LastName = "User",
            TenantId = Guid.NewGuid()
        };
        user.ApplyNewInteractiveProductOnboarding();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var current = new Mock<ICurrentUser>();
        current.Setup(c => c.UserId).Returns(user.Id);
        var service = BuildService(db, current.Object, enabled: false);

        var dto = await service.GetMineAsync(CancellationToken.None);

        Assert.False(dto.Enabled);
        Assert.Equal(ProductOnboardingStatus.NotStarted, dto.Status);
    }

    [Fact]
    public async Task Auto_progress_failure_does_not_fail_get()
    {
        await using var db = BuildDb();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "co@test.tn",
            UserName = "co@test.tn",
            FirstName = "Co",
            LastName = "User",
            TenantId = Guid.NewGuid()
        };
        user.ApplyNewInteractiveProductOnboarding();
        db.Users.Add(user);
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        var address = Address.Create("1 rue Test", "Tunis", "Tunis", postalCode: "1000").Value;
        var email = Email.Create("co@test.tn").Value;
        var phone = PhoneNumber.Create("20123456").Value;
        var tenant = Tenant.Create("Ste Co", nif, address, email, phone, TaxRegime.RealRegime).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(tenant, user.TenantId);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var current = new Mock<ICurrentUser>();
        current.Setup(c => c.UserId).Returns(user.Id);

        var factory = new Mock<ITenantDbContextFactory>();
        factory.Setup(f => f.CreateContext()).Throws(new InvalidOperationException("no tenant"));

        var service = new ProductOnboardingService(
            db,
            current.Object,
            factory.Object,
            Options.Create(new ProductOnboardingSettings { Enabled = true }),
            NullLogger<ProductOnboardingService>.Instance);

        var dto = await service.GetMineAsync(CancellationToken.None);

        Assert.Equal(ProductOnboardingStatus.NotStarted, dto.Status);
        Assert.Empty(dto.AutoCompletedIds);
    }

    private static ProductOnboardingService BuildService(
        MasterDbContext db,
        ICurrentUser current,
        bool enabled = true)
    {
        var factory = new Mock<ITenantDbContextFactory>();
        factory.Setup(f => f.CreateContext()).Throws(new InvalidOperationException("unused"));
        return new ProductOnboardingService(
            db,
            current,
            factory.Object,
            Options.Create(new ProductOnboardingSettings { Enabled = enabled }),
            NullLogger<ProductOnboardingService>.Instance);
    }

    private static MasterDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MasterDbContext(options);
    }
}
