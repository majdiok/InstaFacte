using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
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

public sealed class FirmAssignmentSnapshotTests
{
    private static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static MasterDbContext BuildMaster()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MasterDbContext(options);
    }

    private static async Task SeedTenantsAsync(MasterDbContext db)
    {
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        var address = Address.Create("1 rue Test", "Tunis", "Tunis").Value;
        var email = Email.Create("co@example.com").Value;
        var phone = PhoneNumber.Create("20123456").Value;

        var company = Tenant.Create("Société Hadad", nif, address, email, phone, TaxRegime.RealRegime).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(company, CompanyId);

        var firm = Tenant.CreateAccountingFirm("Cabinet Test", nif, address, email, phone).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(firm, FirmId);

        db.Tenants.AddRange(company, firm);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task RequestAssignment_persists_company_profile_json()
    {
        await using var db = BuildMaster();
        await SeedTenantsAsync(db);

        var snapshot = new CompanyProfileSnapshotDto
        {
            SchemaVersion = 1,
            CapturedAtUtc = DateTime.UtcNow,
            CompanyName = "Société Hadad",
            Nif = "1234567/A/B/C/000",
            TaxRegime = 0,
            Street = "1 rue Test",
            City = "Tunis",
            Governorate = "Tunis",
            Email = "co@example.com"
        };

        var snapshotProvider = new Mock<ICompanyProfileSnapshotProvider>();
        snapshotProvider
            .Setup(p => p.CaptureForCompanyTenantAsync(CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        snapshotProvider
            .Setup(p => p.Serialize(snapshot))
            .Returns("{\"companyName\":\"Société Hadad\"}");
        snapshotProvider
            .Setup(p => p.TryDeserialize(It.IsAny<string?>()))
            .Returns(snapshot);

        var notifications = new Mock<INotificationService>();
        var dossierAccess = new Mock<IFirmDossierAccessService>();
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(u => u.IsAuthenticated).Returns(false);
        var service = new FirmAssignmentService(
            db,
            notifications.Object,
            snapshotProvider.Object,
            dossierAccess.Object,
            currentUser.Object,
            NullLogger<FirmAssignmentService>.Instance);

        var result = await service.RequestAssignmentAsync(
            CompanyId,
            UserId,
            new RequestFirmAssignmentDto { FirmTenantId = FirmId, Notes = "Merci" });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.CompanyProfile);
        Assert.Equal("1234567/A/B/C/000", result.Value.CompanyProfile!.Nif);

        var stored = await db.FirmClientAssignments.SingleAsync();
        Assert.False(string.IsNullOrWhiteSpace(stored.CompanyProfileSnapshotJson));
        Assert.NotNull(stored.CompanyProfileCapturedAt);
    }
}
