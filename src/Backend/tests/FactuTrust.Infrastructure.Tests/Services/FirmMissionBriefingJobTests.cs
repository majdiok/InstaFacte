using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.Background;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Gardes du brief quotidien. Le brief est un envoi sortant automatique : il ne doit partir ni
/// deux fois, ni quand la fonctionnalité est éteinte, ni au prix d'interrompre les autres cabinets.
/// </summary>
public sealed class FirmMissionBriefingJobTests
{
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ManagerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ManagerRoleId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTimeOffset FixedNow = new(2026, 8, 12, 6, 30, 0, TimeSpan.Zero);
    private static DateTime Today => FixedNow.UtcDateTime.Date;

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => FixedNow;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static MasterDbContext BuildMaster() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task SeedFirmWithManagerAsync(MasterDbContext db)
    {
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        var address = Address.Create("1 rue Test", "Tunis", "Tunis").Value;
        var email = Email.Create("cabinet@example.com").Value;
        var phone = PhoneNumber.Create("20123456").Value;

        var firm = Tenant.CreateAccountingFirm("Cabinet Test", nif, address, email, phone).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(firm, FirmId);
        db.Tenants.Add(firm);

        db.Roles.Add(new ApplicationRole { Id = ManagerRoleId, Name = "FirmManager", NormalizedName = "FIRMMANAGER" });
        db.Users.Add(new ApplicationUser
        {
            Id = ManagerId,
            TenantId = FirmId,
            Email = "manager@cabinet.tn",
            UserName = "manager@cabinet.tn",
            FirstName = "Responsable",
            LastName = "Cabinet",
            IsActive = true
        });
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = ManagerId, RoleId = ManagerRoleId });

        await db.SaveChangesAsync();
    }

    private static Mock<IFirmPortfolioReadService> PortfolioWith(
        int activeDossiers,
        int overdue = 0,
        bool partial = false)
    {
        var portfolio = new Mock<IFirmPortfolioReadService>();
        portfolio
            .Setup(p => p.GetOverviewAsync(It.IsAny<Guid>(), It.IsAny<FirmDossierAccessScope?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirmPortfolioOverviewDto
            {
                ActiveDossiersCount = activeDossiers,
                OverdueCount = overdue,
                GeneratedAt = Today,
                FanOut = new FirmFanOutHealthDto { DossiersRead = activeDossiers, DossiersFailed = partial ? 2 : 0 }
            });
        portfolio
            .Setup(p => p.GetDossierHealthAsync(It.IsAny<Guid>(), It.IsAny<FirmDossierAccessScope?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirmDossierHealthListDto());
        portfolio
            .Setup(p => p.GetCollaboratorWorkloadAsync(It.IsAny<Guid>(), It.IsAny<FirmDossierAccessScope?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirmCollaboratorWorkloadDto());
        return portfolio;
    }

    private static FirmMissionBriefingJob Build(
        MasterDbContext master,
        Mock<IEmailService> email,
        AccountingFirmsOptions options,
        Mock<IFirmPortfolioReadService>? portfolio = null) =>
        new(
            master,
            (portfolio ?? PortfolioWith(3)).Object,
            email.Object,
            Options.Create(options),
            new FixedTimeProvider(),
            NullLogger<FirmMissionBriefingJob>.Instance);

    private static AccountingFirmsOptions AllOn() => new()
    {
        Enabled = true,
        FirmAgentEnabled = true,
        FirmAgentDailyBriefingEnabled = true
    };

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task No_briefing_is_sent_when_any_flag_is_off(bool enabled, bool agent, bool briefing)
    {
        await using var db = BuildMaster();
        await SeedFirmWithManagerAsync(db);
        var email = new Mock<IEmailService>();

        var job = Build(db, email, new AccountingFirmsOptions
        {
            Enabled = enabled,
            FirmAgentEnabled = agent,
            FirmAgentDailyBriefingEnabled = briefing
        });

        await job.ExecuteAsync();

        email.Verify(
            e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<EmailAttachment>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Briefing_is_sent_once_to_the_firm_manager()
    {
        await using var db = BuildMaster();
        await SeedFirmWithManagerAsync(db);
        var email = new Mock<IEmailService>();

        await Build(db, email, AllOn()).ExecuteAsync();

        email.Verify(
            e => e.SendEmailAsync("manager@cabinet.tn", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<EmailAttachment>?>(), It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.Single(db.FirmMissionBriefingLogs);
    }

    /// <summary>
    /// Anti-doublon applicatif. La garde ultime reste l'index unique en base, que le fournisseur
    /// InMemory n'applique pas — d'où ce test sur la vérification explicite du job.
    /// </summary>
    [Fact]
    public async Task Second_run_on_the_same_day_sends_nothing()
    {
        await using var db = BuildMaster();
        await SeedFirmWithManagerAsync(db);
        var email = new Mock<IEmailService>();

        await Build(db, email, AllOn()).ExecuteAsync();
        await Build(db, email, AllOn()).ExecuteAsync();

        email.Verify(
            e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<EmailAttachment>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Single(db.FirmMissionBriefingLogs);
    }

    [Fact]
    public async Task Firm_without_active_dossier_receives_nothing()
    {
        await using var db = BuildMaster();
        await SeedFirmWithManagerAsync(db);
        var email = new Mock<IEmailService>();

        await Build(db, email, AllOn(), PortfolioWith(activeDossiers: 0)).ExecuteAsync();

        email.Verify(
            e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<EmailAttachment>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Empty(db.FirmMissionBriefingLogs);
    }

    /// <summary>Un envoi en échec ne doit pas laisser croire qu'un brief est parti.</summary>
    [Fact]
    public async Task Failed_send_is_not_traced_and_does_not_throw()
    {
        await using var db = BuildMaster();
        await SeedFirmWithManagerAsync(db);
        var email = new Mock<IEmailService>();
        email
            .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<EmailAttachment>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP indisponible"));

        var exception = await Record.ExceptionAsync(() => Build(db, email, AllOn()).ExecuteAsync());

        Assert.Null(exception);
        Assert.Empty(db.FirmMissionBriefingLogs);
    }

    /// <summary>
    /// Une lecture partielle du portefeuille doit être dite dans le corps du brief : un compteur
    /// incomplet présenté comme complet induit le responsable en erreur.
    /// </summary>
    [Fact]
    public async Task Partial_read_is_disclosed_in_the_body()
    {
        await using var db = BuildMaster();
        await SeedFirmWithManagerAsync(db);

        string? capturedBody = null;
        var email = new Mock<IEmailService>();
        email
            .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<EmailAttachment>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, IEnumerable<EmailAttachment>?, CancellationToken>(
                (_, _, body, _, _) => capturedBody = body)
            .Returns(Task.CompletedTask);

        await Build(db, email, AllOn(), PortfolioWith(3, overdue: 4, partial: true)).ExecuteAsync();

        Assert.NotNull(capturedBody);
        Assert.Contains("Lecture incomplète", capturedBody);
        Assert.True(db.FirmMissionBriefingLogs.Single().PartialRead);
    }

    [Fact]
    public async Task Overdue_count_appears_in_the_subject()
    {
        await using var db = BuildMaster();
        await SeedFirmWithManagerAsync(db);

        string? capturedSubject = null;
        var email = new Mock<IEmailService>();
        email
            .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<EmailAttachment>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, IEnumerable<EmailAttachment>?, CancellationToken>(
                (_, subject, _, _, _) => capturedSubject = subject)
            .Returns(Task.CompletedTask);

        await Build(db, email, AllOn(), PortfolioWith(3, overdue: 7)).ExecuteAsync();

        Assert.NotNull(capturedSubject);
        Assert.Contains("7 en retard", capturedSubject);
    }
}
