using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Émission best-effort des notifications du flux cabinet : une panne de
/// notification ne doit jamais faire échouer l'opération métier commitée.
/// </summary>
public sealed class FirmAssignmentNotificationTests
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

    private static FirmAssignmentService BuildService(MasterDbContext db, INotificationService notifications)
    {
        var dossierAccess = new Mock<IFirmDossierAccessService>();
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(u => u.IsAuthenticated).Returns(false);
        return new FirmAssignmentService(
            db,
            notifications,
            CreateNoopSnapshotProvider(),
            dossierAccess.Object,
            currentUser.Object,
            NullLogger<FirmAssignmentService>.Instance);
    }

    private static ICompanyProfileSnapshotProvider CreateNoopSnapshotProvider()
    {
        var mock = new Mock<ICompanyProfileSnapshotProvider>();
        mock.Setup(p => p.TryDeserialize(It.IsAny<string?>())).Returns((CompanyProfileSnapshotDto?)null);
        return mock.Object;
    }

    private static async Task<FirmClientAssignment> SeedPendingAssignmentAsync(MasterDbContext db)
    {
        var assignment = FirmClientAssignment.Request(CompanyId, FirmId, UserId).Value;
        db.FirmClientAssignments.Add(assignment);
        await db.SaveChangesAsync();
        return assignment;
    }

    [Fact]
    public async Task Accept_succeeds_even_when_notification_service_throws()
    {
        await using var db = BuildMaster();
        var assignment = await SeedPendingAssignmentAsync(db);

        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<NotificationType>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("panne notification"));

        var service = BuildService(db, notifications.Object);
        var result = await service.AcceptAssignmentAsync(FirmId, assignment.Id, UserId);

        Assert.True(result.IsSuccess);
        Assert.Equal(FirmAssignmentStatus.Active,
            (await db.FirmClientAssignments.SingleAsync(a => a.Id == assignment.Id)).Status);
    }

    [Fact]
    public async Task Accept_notifies_company_administrators()
    {
        await using var db = BuildMaster();
        var assignment = await SeedPendingAssignmentAsync(db);

        var notifications = new Mock<INotificationService>();
        var service = BuildService(db, notifications.Object);

        var result = await service.AcceptAssignmentAsync(FirmId, assignment.Id, UserId);

        Assert.True(result.IsSuccess);
        notifications.Verify(n => n.CreateAsync(
            CompanyId,
            nameof(UserRole.Administrator),
            NotificationType.FirmAssignmentAccepted,
            It.IsAny<string>(),
            It.IsAny<string>(),
            "/settings/accounting-firm",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reject_notifies_company_with_reason_in_body()
    {
        await using var db = BuildMaster();
        var assignment = await SeedPendingAssignmentAsync(db);

        string? capturedBody = null;
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<NotificationType>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string?, NotificationType, string, string, string?, CancellationToken>(
                (_, _, _, _, body, _, _) => capturedBody = body);

        var service = BuildService(db, notifications.Object);
        var result = await service.RejectAssignmentAsync(FirmId, assignment.Id, UserId, "Dossier incomplet");

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedBody);
        Assert.Contains("Dossier incomplet", capturedBody);
    }

    [Fact]
    public async Task CancelPending_notifies_firm_managers()
    {
        await using var db = BuildMaster();
        await SeedPendingAssignmentAsync(db);

        var notifications = new Mock<INotificationService>();
        var service = BuildService(db, notifications.Object);

        var result = await service.CancelPendingByCompanyAsync(CompanyId, UserId);

        Assert.True(result.IsSuccess);
        notifications.Verify(n => n.CreateAsync(
            FirmId,
            nameof(UserRole.FirmManager),
            NotificationType.FirmAssignmentCancelled,
            It.IsAny<string>(),
            It.IsAny<string>(),
            "/firm/invitations",
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
