using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Forecasting;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class FirmLeaveNotificationTests
{
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ManagerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid AccountantId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static MasterDbContext BuildMaster()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MasterDbContext(options);
    }

    private static FirmLeaveService BuildService(MasterDbContext db, INotificationService notifications)
    {
        var calendar = new Mock<ITunisianCalendarService>();
        calendar.Setup(c => c.IsHoliday(It.IsAny<DateTime>())).Returns(false);
        var mirror = new Mock<IFirmLeavePayrollMirrorService>();
        mirror.Setup(m => m.MirrorApprovedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirmLeaveMirrorResultDto
            {
                State = (int)FirmLeavePayrollMirrorState.NoPayrollEffect,
                StateDisplay = "Sans effet paie"
            });
        return new FirmLeaveService(
            db, calendar.Object, mirror.Object, notifications, NullLogger<FirmLeaveService>.Instance);
    }

    private static async Task SeedUsersAsync(MasterDbContext db)
    {
        db.Users.Add(new ApplicationUser
        {
            Id = ManagerId,
            UserName = "mgr@test.com",
            NormalizedUserName = "MGR@TEST.COM",
            Email = "mgr@test.com",
            NormalizedEmail = "MGR@TEST.COM",
            FirstName = "Manager",
            LastName = "Test",
            TenantId = FirmId,
            IsActive = true,
            EmailConfirmed = true
        });
        db.Users.Add(new ApplicationUser
        {
            Id = AccountantId,
            UserName = "acc@test.com",
            NormalizedUserName = "ACC@TEST.COM",
            Email = "acc@test.com",
            NormalizedEmail = "ACC@TEST.COM",
            FirstName = "Collab",
            LastName = "Test",
            TenantId = FirmId,
            IsActive = true,
            EmailConfirmed = true
        });
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> PaidTypeIdAsync(FirmLeaveService svc, MasterDbContext db)
    {
        await svc.EnsureDefaultsAsync(FirmId, 2026);
        return await db.FirmLeaveTypes.Where(t => t.FirmTenantId == FirmId && t.Code == "PAID")
            .Select(t => t.Id).FirstAsync();
    }

    private static CreateFirmLeaveRequestDto ImmediateDto(Guid typeId) => new()
    {
        LeaveTypeId = typeId,
        StartDate = new DateTime(2026, 8, 10),
        EndDate = new DateTime(2026, 8, 12),
        StartUnit = 0,
        EndUnit = 0,
        Reason = "Vacances",
        SubmitImmediately = true
    };

    [Fact]
    public async Task Collaborator_create_immediate_notifies_firm_manager()
    {
        await using var db = BuildMaster();
        await SeedUsersAsync(db);
        string? capturedBody = null;
        string? capturedLink = null;
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<NotificationType>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string?, NotificationType, string, string, string?, CancellationToken>(
                (_, _, _, _, body, link, _) => { capturedBody = body; capturedLink = link; })
            .ReturnsAsync(FactuTrust.Domain.Common.Result.Success());

        var svc = BuildService(db, notifications.Object);
        var typeId = await PaidTypeIdAsync(svc, db);

        var create = await svc.CreateAsync(FirmId, AccountantId, isManager: false, ImmediateDto(typeId));

        Assert.True(create.IsSuccess);
        notifications.Verify(n => n.CreateAsync(
            FirmId,
            nameof(UserRole.FirmManager),
            NotificationType.FirmLeaveRequestSubmitted,
            "Nouvelle demande de congé",
            It.IsAny<string>(),
            "/firm/governance/leaves/validation",
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("Collab Test", capturedBody);
        Assert.Contains("Congé payé", capturedBody);
        Assert.Contains("10/08/2026", capturedBody);
        Assert.Contains("12/08/2026", capturedBody);
        Assert.Equal("/firm/governance/leaves/validation", capturedLink);
    }

    [Fact]
    public async Task Collaborator_draft_does_not_notify_submit_does()
    {
        await using var db = BuildMaster();
        await SeedUsersAsync(db);
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<NotificationType>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FactuTrust.Domain.Common.Result.Success());

        var svc = BuildService(db, notifications.Object);
        var typeId = await PaidTypeIdAsync(svc, db);

        var create = await svc.CreateAsync(FirmId, AccountantId, false, ImmediateDto(typeId) with { SubmitImmediately = false });
        Assert.True(create.IsSuccess);
        notifications.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<NotificationType>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);

        var submit = await svc.SubmitAsync(FirmId, AccountantId, false, create.Value.Id);
        Assert.True(submit.IsSuccess);
        notifications.Verify(n => n.CreateAsync(
            FirmId,
            nameof(UserRole.FirmManager),
            NotificationType.FirmLeaveRequestSubmitted,
            It.IsAny<string>(),
            It.IsAny<string>(),
            "/firm/governance/leaves/validation",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Manager_create_or_submit_does_not_notify()
    {
        await using var db = BuildMaster();
        await SeedUsersAsync(db);
        var notifications = new Mock<INotificationService>();
        var svc = BuildService(db, notifications.Object);
        var typeId = await PaidTypeIdAsync(svc, db);

        var forSelf = await svc.CreateAsync(FirmId, ManagerId, true, ImmediateDto(typeId) with { UserId = ManagerId });
        Assert.True(forSelf.IsSuccess);

        var forOther = await svc.CreateAsync(FirmId, ManagerId, true, ImmediateDto(typeId) with
        {
            UserId = AccountantId,
            StartDate = new DateTime(2026, 9, 7),
            EndDate = new DateTime(2026, 9, 8)
        });
        Assert.True(forOther.IsSuccess);

        notifications.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<NotificationType>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Submit_succeeds_when_notification_service_throws()
    {
        await using var db = BuildMaster();
        await SeedUsersAsync(db);
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<NotificationType>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("panne notification"));

        var svc = BuildService(db, notifications.Object);
        var typeId = await PaidTypeIdAsync(svc, db);

        var create = await svc.CreateAsync(FirmId, AccountantId, false, ImmediateDto(typeId) with { SubmitImmediately = false });
        var submit = await svc.SubmitAsync(FirmId, AccountantId, false, create.Value.Id);

        Assert.True(submit.IsSuccess);
        var persisted = await db.FirmLeaveRequests.SingleAsync(r => r.Id == create.Value.Id);
        Assert.Equal(FirmLeaveRequestStatus.Submitted, persisted.Status);
    }

    [Fact]
    public async Task Double_submit_does_not_notify_again()
    {
        await using var db = BuildMaster();
        await SeedUsersAsync(db);
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<NotificationType>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FactuTrust.Domain.Common.Result.Success());

        var svc = BuildService(db, notifications.Object);
        var typeId = await PaidTypeIdAsync(svc, db);

        var create = await svc.CreateAsync(FirmId, AccountantId, false, ImmediateDto(typeId));
        Assert.True(create.IsSuccess);

        var second = await svc.SubmitAsync(FirmId, AccountantId, false, create.Value.Id);
        Assert.True(second.IsFailure);
        notifications.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<NotificationType>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reject_then_resubmit_notifies_once_more()
    {
        await using var db = BuildMaster();
        await SeedUsersAsync(db);
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<NotificationType>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FactuTrust.Domain.Common.Result.Success());

        var svc = BuildService(db, notifications.Object);
        var typeId = await PaidTypeIdAsync(svc, db);

        var create = await svc.CreateAsync(FirmId, AccountantId, false, ImmediateDto(typeId));
        await svc.ProcessAsync(FirmId, ManagerId, "M", create.Value.Id, new ProcessFirmLeaveDto
        {
            Approve = false,
            RejectionReason = "Pas possible"
        });

        var updated = await svc.UpdateAsync(FirmId, AccountantId, false, create.Value.Id, new UpdateFirmLeaveRequestDto
        {
            LeaveTypeId = typeId,
            StartDate = new DateTime(2026, 9, 7),
            EndDate = new DateTime(2026, 9, 8),
            StartUnit = 0,
            EndUnit = 0,
            Reason = "Reporté"
        });
        Assert.True(updated.IsSuccess);

        var submit = await svc.SubmitAsync(FirmId, AccountantId, false, create.Value.Id);
        Assert.True(submit.IsSuccess);
        notifications.Verify(n => n.CreateAsync(
            FirmId,
            nameof(UserRole.FirmManager),
            NotificationType.FirmLeaveRequestSubmitted,
            It.IsAny<string>(),
            It.IsAny<string>(),
            "/firm/governance/leaves/validation",
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
