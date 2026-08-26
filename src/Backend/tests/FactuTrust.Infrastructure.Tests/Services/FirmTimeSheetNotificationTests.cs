using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class FirmTimeSheetNotificationTests
{
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ManagerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid AccountantId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private static readonly DateTimeOffset FixedNow = new(2026, 7, 21, 10, 0, 0, TimeSpan.Zero);

    private static MasterDbContext BuildMaster()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MasterDbContext(options);
    }

    private static FirmGovernanceService BuildService(MasterDbContext db, INotificationService notifications)
    {
        var fiscalOps = new Mock<IFirmFiscalOpsAggregator>();
        fiscalOps
            .Setup(f => f.AggregateAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirmFiscalOpsSummaryDto());

        var store = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManager.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(new List<string> { UserRole.FirmAccountant.ToString() });

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(u => u.IsAuthenticated).Returns(false);

        return new FirmGovernanceService(
            db,
            userManager.Object,
            Mock.Of<ITenantService>(),
            fiscalOps.Object,
            Mock.Of<ICompanyProfileSnapshotProvider>(),
            new FirmDossierAccessService(db),
            currentUser.Object,
            new FakeTimeProvider(FixedNow),
            NullLogger<FirmGovernanceService>.Instance,
            notifications);
    }

    private static async Task<ApplicationUser> SeedAccountantAsync(MasterDbContext db)
    {
        var user = new ApplicationUser
        {
            Id = AccountantId,
            UserName = "accountant@test.tn",
            Email = "accountant@test.tn",
            FirstName = "Ali",
            LastName = "Accountant",
            TenantId = FirmId,
            IsActive = true,
            EmailConfirmed = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static CreateTimeSheetEntryDto Draft(DateTime date, decimal hours) => new()
    {
        WorkDate = date,
        Hours = hours,
        IsBillable = true
    };

    [Fact]
    public async Task Collaborator_submit_notifies_firm_manager()
    {
        await using var db = BuildMaster();
        var accountant = await SeedAccountantAsync(db);
        string? capturedBody = null;
        string? capturedLink = null;
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<NotificationType>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string?, NotificationType, string, string, string?, CancellationToken>(
                (_, _, _, _, body, link, _) => { capturedBody = body; capturedLink = link; })
            .ReturnsAsync(FactuTrust.Domain.Common.Result.Success());

        var service = BuildService(db, notifications.Object);
        var created = await service.CreateTimeSheetAsync(
            FirmId, accountant.Id, "Ali Accountant", isManager: false, Draft(new DateTime(2026, 7, 15), 4m));
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Description : "");

        var submitted = await service.SubmitTimeSheetAsync(FirmId, accountant.Id, isManager: false, created.Value.Id);
        Assert.True(submitted.IsSuccess);

        notifications.Verify(n => n.CreateAsync(
            FirmId,
            nameof(UserRole.FirmManager),
            NotificationType.FirmTimeSheetSubmitted,
            "Feuille de temps soumise",
            It.IsAny<string>(),
            $"/firm/governance/time-sheets?userId={accountant.Id}",
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("Ali Accountant", capturedBody);
        Assert.Contains("15/07/2026", capturedBody);
        Assert.Equal($"/firm/governance/time-sheets?userId={accountant.Id}", capturedLink);
    }

    [Fact]
    public async Task Manager_submit_does_not_notify()
    {
        await using var db = BuildMaster();
        var notifications = new Mock<INotificationService>();
        var service = BuildService(db, notifications.Object);

        var created = await service.CreateTimeSheetAsync(
            FirmId, ManagerId, "Manager Test", isManager: true, Draft(new DateTime(2026, 7, 15), 4m));
        Assert.True(created.IsSuccess);

        var submitted = await service.SubmitTimeSheetAsync(FirmId, ManagerId, isManager: true, created.Value.Id);
        Assert.True(submitted.IsSuccess);

        notifications.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<NotificationType>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Bulk_three_valid_entries_notifies_once_with_count()
    {
        await using var db = BuildMaster();
        var accountant = await SeedAccountantAsync(db);
        string? capturedBody = null;
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<NotificationType>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string?, NotificationType, string, string, string?, CancellationToken>(
                (_, _, _, _, body, _, _) => capturedBody = body)
            .ReturnsAsync(FactuTrust.Domain.Common.Result.Success());

        var service = BuildService(db, notifications.Object);
        var first = await service.CreateTimeSheetAsync(
            FirmId, accountant.Id, "Ali Accountant", false, Draft(new DateTime(2026, 7, 13), 2m));
        var second = await service.CreateTimeSheetAsync(
            FirmId, accountant.Id, "Ali Accountant", false, Draft(new DateTime(2026, 7, 14), 3m));
        var third = await service.CreateTimeSheetAsync(
            FirmId, accountant.Id, "Ali Accountant", false, Draft(new DateTime(2026, 7, 15), 4m));
        Assert.True(first.IsSuccess && second.IsSuccess && third.IsSuccess);

        var bulk = await service.SubmitTimeSheetsBulkAsync(
            FirmId, accountant.Id, false,
            [first.Value.Id, second.Value.Id, third.Value.Id]);

        Assert.True(bulk.IsSuccess);
        Assert.Equal(3, bulk.Value.Submitted);
        Assert.Equal(0, bulk.Value.Skipped);
        notifications.Verify(n => n.CreateAsync(
            FirmId,
            nameof(UserRole.FirmManager),
            NotificationType.FirmTimeSheetSubmitted,
            "Feuilles de temps soumises",
            It.IsAny<string>(),
            $"/firm/governance/time-sheets?userId={accountant.Id}",
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("3", capturedBody);
        Assert.Contains("Ali Accountant", capturedBody);
    }

    [Fact]
    public async Task Bulk_mixed_timer_skips_timer_and_notifies_for_successes()
    {
        await using var db = BuildMaster();
        var accountant = await SeedAccountantAsync(db);
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<NotificationType>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FactuTrust.Domain.Common.Result.Success());

        var service = BuildService(db, notifications.Object);
        var first = await service.CreateTimeSheetAsync(
            FirmId, accountant.Id, "Ali Accountant", false, Draft(new DateTime(2026, 7, 13), 2m));
        var second = await service.CreateTimeSheetAsync(
            FirmId, accountant.Id, "Ali Accountant", false, Draft(new DateTime(2026, 7, 14), 3m));
        var timer = await service.StartTimeSheetTimerAsync(
            FirmId, accountant.Id, "Ali Accountant", false,
            new StartTimeSheetTimerDto { WorkDate = new DateTime(2026, 7, 15) });
        Assert.True(first.IsSuccess && second.IsSuccess && timer.IsSuccess);

        var bulk = await service.SubmitTimeSheetsBulkAsync(
            FirmId, accountant.Id, false,
            [first.Value.Id, second.Value.Id, timer.Value.Id]);

        Assert.True(bulk.IsSuccess);
        Assert.Equal(2, bulk.Value.Submitted);
        Assert.Equal(1, bulk.Value.Skipped);
        var persistedTimer = await db.FirmTimeSheetEntries.SingleAsync(t => t.Id == timer.Value.Id);
        Assert.Equal(FirmTimeSheetStatus.Draft, persistedTimer.Status);
        Assert.NotNull(persistedTimer.TimerStartedAtUtc);
        notifications.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<NotificationType>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Bulk_zero_success_does_not_notify()
    {
        await using var db = BuildMaster();
        var accountant = await SeedAccountantAsync(db);
        var notifications = new Mock<INotificationService>();
        var service = BuildService(db, notifications.Object);

        var timer = await service.StartTimeSheetTimerAsync(
            FirmId, accountant.Id, "Ali Accountant", false,
            new StartTimeSheetTimerDto { WorkDate = new DateTime(2026, 7, 15) });
        Assert.True(timer.IsSuccess);

        var bulk = await service.SubmitTimeSheetsBulkAsync(
            FirmId, accountant.Id, false, [timer.Value.Id]);

        Assert.True(bulk.IsSuccess);
        Assert.Equal(0, bulk.Value.Submitted);
        notifications.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<NotificationType>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Submit_succeeds_when_notification_service_throws()
    {
        await using var db = BuildMaster();
        var accountant = await SeedAccountantAsync(db);
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<NotificationType>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("panne notification"));

        var service = BuildService(db, notifications.Object);
        var created = await service.CreateTimeSheetAsync(
            FirmId, accountant.Id, "Ali Accountant", false, Draft(new DateTime(2026, 7, 15), 4m));
        var submitted = await service.SubmitTimeSheetAsync(FirmId, accountant.Id, false, created.Value.Id);

        Assert.True(submitted.IsSuccess);
        var persisted = await db.FirmTimeSheetEntries.SingleAsync(t => t.Id == created.Value.Id);
        Assert.Equal(FirmTimeSheetStatus.Submitted, persisted.Status);

        var other = await service.CreateTimeSheetAsync(
            FirmId, accountant.Id, "Ali Accountant", false, Draft(new DateTime(2026, 7, 16), 2m));
        var bulk = await service.SubmitTimeSheetsBulkAsync(FirmId, accountant.Id, false, [other.Value.Id]);
        Assert.True(bulk.IsSuccess);
        Assert.Equal(1, bulk.Value.Submitted);
    }

    [Fact]
    public async Task Accountant_cannot_submit_another_users_entry_and_does_not_notify()
    {
        await using var db = BuildMaster();
        var accountant = await SeedAccountantAsync(db);
        var notifications = new Mock<INotificationService>();
        var service = BuildService(db, notifications.Object);

        var created = await service.CreateTimeSheetAsync(
            FirmId, ManagerId, "Manager Test", isManager: true, Draft(new DateTime(2026, 7, 15), 4m));
        Assert.True(created.IsSuccess);

        var forbidden = await service.SubmitTimeSheetAsync(FirmId, accountant.Id, isManager: false, created.Value.Id);
        Assert.True(forbidden.IsFailure);
        notifications.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<NotificationType>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
