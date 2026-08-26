using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Exchange;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class ExchangeOppositePartyNotifierTests
{
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CompanyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid AssignmentId = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    private static readonly Guid CollabId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private static MasterDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MasterDbContext(options);
    }

    private static ExchangeThread Thread() =>
        ExchangeThread.Create(AssignmentId, FirmId, CompanyId).Value;

    private static (ExchangeOppositePartyNotifier Notifier, Mock<INotificationService> Notifications)
        Build(MasterDbContext db)
    {
        var notifications = new Mock<INotificationService>();
        var notifier = new ExchangeOppositePartyNotifier(
            db, notifications.Object, NullLogger<ExchangeOppositePartyNotifier>.Instance);
        return (notifier, notifications);
    }

    private static ApplicationUser CollabUser(bool isActive = true, Guid? tenantId = null) => new()
    {
        Id = CollabId,
        UserName = "collab@test.com",
        NormalizedUserName = "COLLAB@TEST.COM",
        Email = "collab@test.com",
        NormalizedEmail = "COLLAB@TEST.COM",
        FirstName = "Sami",
        LastName = "Mansour",
        TenantId = tenantId ?? FirmId,
        IsActive = isActive,
        EmailConfirmed = true
    };

    [Fact]
    public async Task Company_actor_notifies_assigned_active_collaborator()
    {
        await using var db = BuildDb();
        db.Users.Add(CollabUser());
        var file = PermanentFile.Create(AssignmentId, FirmId, CompanyId, "Ste").Value;
        file.AssignAccountant(CollabId, "Sami Mansour");
        db.PermanentFiles.Add(file);
        await db.SaveChangesAsync();

        var (notifier, notifications) = Build(db);
        var thread = Thread();
        var target = await notifier.NotifyAsync(
            thread, CompanyId, NotificationType.ExchangeRequestCreated,
            "Nouvelle demande", "Titre", "demandes");

        Assert.Equal(FirmId, target.RecipientTenantId);
        Assert.Null(target.RecipientRole);
        Assert.Equal(CollabId, target.RecipientUserId);
        notifications.Verify(n => n.CreateAsync(
            FirmId, null, NotificationType.ExchangeRequestCreated,
            "Nouvelle demande", "Titre",
            It.Is<string>(l => l.Contains("/firm/exchanges/") && l.Contains("tab=demandes")),
            CollabId, It.IsAny<CancellationToken>()), Times.Once);
        notifications.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<NotificationType>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Company_actor_falls_back_to_firm_manager_when_no_permanent_file()
    {
        await using var db = BuildDb();
        var (notifier, notifications) = Build(db);
        var thread = Thread();

        var target = await notifier.NotifyAsync(
            thread, CompanyId, NotificationType.ExchangeRequestCreated,
            "Nouvelle demande", "Titre", "demandes");

        Assert.Equal(FirmId, target.RecipientTenantId);
        Assert.Equal(nameof(UserRole.FirmManager), target.RecipientRole);
        Assert.Null(target.RecipientUserId);
        notifications.Verify(n => n.CreateAsync(
            FirmId, nameof(UserRole.FirmManager), NotificationType.ExchangeRequestCreated,
            "Nouvelle demande", "Titre",
            It.Is<string>(l => l.Contains("tab=demandes")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Company_actor_falls_back_when_assigned_user_is_inactive()
    {
        await using var db = BuildDb();
        db.Users.Add(CollabUser(isActive: false));
        var file = PermanentFile.Create(AssignmentId, FirmId, CompanyId, "Ste").Value;
        file.AssignAccountant(CollabId, "Sami");
        db.PermanentFiles.Add(file);
        await db.SaveChangesAsync();

        var (notifier, notifications) = Build(db);
        var target = await notifier.NotifyAsync(
            Thread(), CompanyId, NotificationType.ExchangeDocumentShared,
            "Nouveau document", "a.pdf", "documents");

        Assert.Equal(nameof(UserRole.FirmManager), target.RecipientRole);
        Assert.Null(target.RecipientUserId);
        notifications.Verify(n => n.CreateAsync(
            FirmId, nameof(UserRole.FirmManager), NotificationType.ExchangeDocumentShared,
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Company_actor_falls_back_when_assigned_user_belongs_to_other_tenant()
    {
        await using var db = BuildDb();
        db.Users.Add(CollabUser(tenantId: Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")));
        var file = PermanentFile.Create(AssignmentId, FirmId, CompanyId, "Ste").Value;
        file.AssignAccountant(CollabId, "Sami");
        db.PermanentFiles.Add(file);
        await db.SaveChangesAsync();

        var (notifier, _) = Build(db);
        var target = await notifier.NotifyAsync(
            Thread(), CompanyId, NotificationType.ExchangeTaskCreated,
            "Nouvelle tâche", "T", "taches");

        Assert.Equal(nameof(UserRole.FirmManager), target.RecipientRole);
        Assert.Null(target.RecipientUserId);
    }

    [Fact]
    public async Task Firm_actor_notifies_company_administrators()
    {
        await using var db = BuildDb();
        var file = PermanentFile.Create(AssignmentId, FirmId, CompanyId, "Ste").Value;
        file.AssignAccountant(CollabId, "Sami");
        db.PermanentFiles.Add(file);
        db.Users.Add(CollabUser());
        await db.SaveChangesAsync();

        var (notifier, notifications) = Build(db);
        var target = await notifier.NotifyAsync(
            Thread(), FirmId, NotificationType.ExchangeTaskCreated,
            "Nouvelle tâche", "T", "taches");

        Assert.Equal(CompanyId, target.RecipientTenantId);
        Assert.Equal(nameof(UserRole.Administrator), target.RecipientRole);
        Assert.Null(target.RecipientUserId);
        notifications.Verify(n => n.CreateAsync(
            CompanyId, nameof(UserRole.Administrator), NotificationType.ExchangeTaskCreated,
            "Nouvelle tâche", "T",
            It.Is<string>(l => l.StartsWith("/exchanges/") && l.Contains("tab=taches")),
            It.IsAny<CancellationToken>()), Times.Once);
        notifications.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<NotificationType>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Notify_succeeds_when_notification_service_throws()
    {
        await using var db = BuildDb();
        var (notifier, notifications) = Build(db);
        notifications
            .Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<NotificationType>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("panne"));

        var target = await notifier.NotifyAsync(
            Thread(), CompanyId, NotificationType.ExchangeRequestCreated,
            "Nouvelle demande", "Titre", "demandes");

        Assert.Equal(nameof(UserRole.FirmManager), target.RecipientRole);
    }
}
