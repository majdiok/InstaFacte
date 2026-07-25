using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class FirmCollaboratorServiceTests
{
    private static readonly Guid FirmId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ManagerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private sealed class TestContext
    {
        public required MasterDbContext Db { get; init; }
        public required UserManager<ApplicationUser> UserManager { get; init; }
        public required FirmCollaboratorService Service { get; init; }
        public required Mock<IEmailService> Email { get; init; }
        public required string StorageRoot { get; init; }
    }

    private static async Task<TestContext> CreateContextAsync()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new MasterDbContext(options);

        var nif = NIF.Create("1234567/A/B/C/000").Value;
        var address = Address.Create("12 rue Test", "Tunis", "Tunis", postalCode: "1000", country: "Tunisie").Value;
        var emailVo = Email.Create("cabinet@test.tn").Value;
        var phone = PhoneNumber.Create("20123456").Value;
        var firm = Tenant.CreateAccountingFirm("Cabinet Test", nif, address, emailVo, phone).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(firm, FirmId);
        db.Tenants.Add(firm);

        db.Roles.Add(new ApplicationRole
        {
            Id = Guid.NewGuid(),
            Name = UserRole.FirmAccountant.ToString(),
            NormalizedName = UserRole.FirmAccountant.ToString().ToUpperInvariant()
        });
        db.Roles.Add(new ApplicationRole
        {
            Id = Guid.NewGuid(),
            Name = UserRole.FirmManager.ToString(),
            NormalizedName = UserRole.FirmManager.ToString().ToUpperInvariant()
        });
        await db.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(options);
        services.AddScoped(_ => db);
        services.AddIdentityCore<ApplicationUser>(o =>
            {
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequiredLength = 6;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<MasterDbContext>();

        var sp = services.BuildServiceProvider();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

        var manager = new ApplicationUser
        {
            Id = ManagerId,
            UserName = "manager@test.tn",
            Email = "manager@test.tn",
            FirstName = "Manager",
            LastName = "Test",
            TenantId = FirmId,
            EmailConfirmed = true,
            IsActive = true
        };
        await userManager.CreateAsync(manager, "Password1!");
        await userManager.AddToRoleAsync(manager, UserRole.FirmManager.ToString());

        var email = new Mock<IEmailService>();
        email.Setup(e => e.EnqueueTemplatedAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object?>?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var storageRoot = Path.Combine(Path.GetTempPath(), "ft-collab-tests", Guid.NewGuid().ToString("N"));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["App:FrontendBaseUrl"] = "http://localhost:4200"
        }).Build();

        var service = new FirmCollaboratorService(
            db,
            userManager,
            email.Object,
            config,
            Options.Create(new FirmCollaboratorStorageOptions { BasePath = storageRoot }),
            NullLogger<FirmCollaboratorService>.Instance);

        return new TestContext
        {
            Db = db,
            UserManager = userManager,
            Service = service,
            Email = email,
            StorageRoot = storageRoot
        };
    }

    [Fact]
    public async Task Create_WithPassword_KeepsEmailConfirmed_AndSkipsInvite()
    {
        var ctx = await CreateContextAsync();
        await using var db = ctx.Db;

        var result = await ctx.Service.CreateAsync(FirmId, new CreateFirmUserDto
        {
            Email = "collab@test.tn",
            FirstName = "Ada",
            LastName = "Lovelace",
            Password = "Password1!",
            Role = UserRole.FirmAccountant,
            Qualification = "Comptable",
            UseFirmAddress = true
        }, null, null, null);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");
        Assert.True(result.Value.EmailConfirmed);
        Assert.Contains("12 rue Test", result.Value.AddressLine);
        ctx.Email.Verify(e => e.EnqueueTemplatedAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, object?>?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_DoesNotChangeEmailConfirmed()
    {
        var ctx = await CreateContextAsync();
        await using var db = ctx.Db;

        var created = await ctx.Service.CreateAsync(FirmId, new CreateFirmUserDto
        {
            Email = "unconfirmed@test.tn",
            FirstName = "Jean",
            LastName = "Dupont",
            Role = UserRole.FirmAccountant,
            SendInvite = true,
            Qualification = "Expert"
        }, null, null, null);

        Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Description : "");
        Assert.False(created.Value.EmailConfirmed);

        var updated = await ctx.Service.UpdateAsync(FirmId, created.Value.Id, ManagerId, new UpdateFirmUserDto
        {
            FirstName = "Jean-Pierre",
            Qualification = "Expert comptable"
        });

        Assert.True(updated.IsSuccess);
        Assert.False(updated.Value.EmailConfirmed);
        Assert.Equal("Jean-Pierre", updated.Value.FirstName);
    }

    [Fact]
    public async Task UseFirmAddress_Off_DoesNotMutateTenantAddress()
    {
        var ctx = await CreateContextAsync();
        await using var db = ctx.Db;

        var created = await ctx.Service.CreateAsync(FirmId, new CreateFirmUserDto
        {
            Email = "addr@test.tn",
            FirstName = "Ali",
            LastName = "Ben",
            Password = "Password1!",
            Role = UserRole.FirmAccountant,
            Qualification = "Aide",
            UseFirmAddress = true
        }, null, null, null);

        Assert.True(created.IsSuccess);

        await ctx.Service.UpdateAsync(FirmId, created.Value.Id, ManagerId, new UpdateFirmUserDto
        {
            UseFirmAddress = false,
            AddressLine = "99 rue Perso",
            City = "Sfax",
            PostalCode = "3000",
            Country = "Tunisie"
        });

        var tenant = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == FirmId);
        Assert.Equal("12 rue Test", tenant.Address.Street);
        Assert.Equal("Tunis", tenant.Address.City);

        var detail = await ctx.Service.GetByIdAsync(FirmId, created.Value.Id);
        Assert.Equal("99 rue Perso", detail.Value.AddressLine);
        Assert.Equal("Sfax", detail.Value.City);
    }

    [Fact]
    public async Task SetActive_RejectsSelfDeactivation()
    {
        var ctx = await CreateContextAsync();
        await using var db = ctx.Db;

        var result = await ctx.Service.SetActiveAsync(FirmId, ManagerId, ManagerId, isActive: false);
        Assert.True(result.IsFailure);
        Assert.Contains("propre compte", result.Error.Description);
    }

    [Fact]
    public async Task UploadCni_PersistsFileNameOnProfile()
    {
        var ctx = await CreateContextAsync();
        await using var db = ctx.Db;

        var created = await ctx.Service.CreateAsync(FirmId, new CreateFirmUserDto
        {
            Email = "cni@test.tn",
            FirstName = "Cni",
            LastName = "User",
            Password = "Password1!",
            Role = UserRole.FirmAccountant,
            Qualification = "Comptable"
        }, null, null, null);
        Assert.True(created.IsSuccess);

        await using var pdf = new MemoryStream("%PDF-1.4 test"u8.ToArray());
        var upload = await ctx.Service.UploadCniAsync(
            FirmId, created.Value.Id, pdf, "carte.pdf", "application/pdf", pdf.Length);

        Assert.True(upload.IsSuccess, upload.IsFailure ? upload.Error.Description : "");
        Assert.True(upload.Value.HasCni);

        var profile = await db.FirmCollaboratorProfiles.AsNoTracking()
            .FirstAsync(p => p.UserId == created.Value.Id);
        Assert.False(string.IsNullOrWhiteSpace(profile.CniFileName));
        Assert.EndsWith(".pdf", profile.CniFileName);
    }
}
