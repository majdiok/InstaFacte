using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Rappels e-mail automatiques de l'échéancier fiscal : seuils J-7/J-1, relance hebdomadaire
/// en retard, anti-doublon quotidien (LastReminderAt), statuts exclus, responsables sans e-mail,
/// marquage MarkReminder + historique « AutoReminder ».
/// </summary>
public sealed class FiscalReminderServiceTests
{
    private static readonly DateTime Today = new(2026, 7, 10);
    private static readonly Guid ResponsibleId = Guid.NewGuid();

    private readonly TestTenantDbContextFactory _factory;
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<IChannelOutboundSender> _channelSender = new();

    public FiscalReminderServiceTests()
    {
        _factory = new TestTenantDbContextFactory($"FiscalReminderDb_{Guid.NewGuid()}");
    }

    private sealed class TestTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly string _databaseName;
        public TestTenantDbContextFactory(string databaseName) => _databaseName = databaseName;
        public TenantDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(databaseName: _databaseName)
                .Options;
            return new TenantDbContext(options);
        }
    }

    private FiscalReminderService BuildService(int[]? leadDays = null) =>
        new(_email.Object,
            _channelSender.Object,
            Options.Create(new AccountingSettings
            {
                FiscalEmailRemindersEnabled = true,
                FiscalReminderLeadDays = leadDays ?? [7, 1]
            }),
            NullLogger<FiscalReminderService>.Instance);

    private static IReadOnlyDictionary<Guid, (string Email, string Name)> Users(string email = "expert@cabinet.tn") =>
        new Dictionary<Guid, (string, string)> { [ResponsibleId] = (email, "Sonia Ben Ali") };

    private Guid SeedEntry(DateTime dueDate, Guid? responsibleId = null,
        DateTime? lastReminderAt = null, bool deposited = false, bool cancelled = false)
    {
        var entry = FiscalScheduleEntry.Create(
            FiscalObligationType.MonthlyDeclaration, "Déclaration TVA", 2026, dueDate, 500m,
            responsibleUserId: responsibleId ?? ResponsibleId, responsibleName: "Sonia Ben Ali").Value;
        if (deposited)
            entry.MarkDeposited(dueDate.AddDays(-1));
        if (lastReminderAt.HasValue)
            entry.MarkReminder(FiscalReminderChannel.Email, lastReminderAt.Value);
        if (cancelled)
            entry.Cancel();
        entry.SetAuditInfo("test", false);
        using var ctx = _factory.CreateContext();
        ctx.FiscalScheduleEntries.Add(entry);
        ctx.SaveChanges();
        return entry.Id;
    }

    private async Task<int> RunAsync(int[]? leadDays = null)
    {
        await using var ctx = _factory.CreateContext();
        return await BuildService(leadDays).ProcessTenantAsync(ctx, Users(), "Ma Société SARL", Today);
    }

    [Fact]
    public async Task Sends_AtLeadDays_7_And_1_ButNot_3()
    {
        SeedEntry(Today.AddDays(7));
        SeedEntry(Today.AddDays(1));
        SeedEntry(Today.AddDays(3));

        var sent = await RunAsync();

        Assert.Equal(2, sent);
        _email.Verify(e => e.SendEmailAsync(
            "expert@cabinet.tn", It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IEnumerable<EmailAttachment>?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Overdue_SendsWeekly_NotDaily()
    {
        // En retard, jamais rappelée → envoi.
        SeedEntry(Today.AddDays(-3));
        // En retard, rappelée il y a 2 jours → pas de relance (hebdomadaire).
        SeedEntry(Today.AddDays(-10), lastReminderAt: Today.AddDays(-2));
        // En retard, rappelée il y a 8 jours → relance.
        SeedEntry(Today.AddDays(-20), lastReminderAt: Today.AddDays(-8));

        var sent = await RunAsync();

        Assert.Equal(2, sent);
    }

    [Fact]
    public async Task SameDayReminder_IsNotDuplicated()
    {
        // Rappel (manuel ou auto) déjà posé aujourd'hui sur une échéance J-7.
        SeedEntry(Today.AddDays(7), lastReminderAt: Today.AddHours(2));

        var sent = await RunAsync();

        Assert.Equal(0, sent);
        _email.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Skips_Deposited_Cancelled_AndWithoutResponsible()
    {
        SeedEntry(Today.AddDays(7), deposited: true);
        SeedEntry(Today.AddDays(7), cancelled: true);
        var noResponsible = FiscalScheduleEntry.Create(
            FiscalObligationType.MonthlyDeclaration, "TVA sans responsable", 2026, Today.AddDays(7), 100m).Value;
        noResponsible.SetAuditInfo("test", false);
        using (var ctx = _factory.CreateContext())
        {
            ctx.FiscalScheduleEntries.Add(noResponsible);
            ctx.SaveChanges();
        }

        var sent = await RunAsync();

        Assert.Equal(0, sent);
    }

    [Fact]
    public async Task Skips_WhenResponsibleEmailMissing()
    {
        SeedEntry(Today.AddDays(1));

        await using var ctx = _factory.CreateContext();
        var sent = await BuildService().ProcessTenantAsync(
            ctx, new Dictionary<Guid, (string, string)>(), "Ma Société SARL", Today);

        Assert.Equal(0, sent);
    }

    [Fact]
    public async Task Sending_MarksReminder_AndWritesAutoReminderHistory()
    {
        var entryId = SeedEntry(Today.AddDays(1));

        var sent = await RunAsync();

        Assert.Equal(1, sent);
        await using var ctx = _factory.CreateContext();
        var entry = await ctx.FiscalScheduleEntries.SingleAsync(e => e.Id == entryId);
        Assert.NotNull(entry.LastReminderAt);
        Assert.Equal(FiscalReminderChannel.Email, entry.LastReminderChannel);
        var history = await ctx.FiscalScheduleHistoryEntries
            .Where(h => h.FiscalScheduleEntryId == entryId && h.Action == "AutoReminder")
            .ToListAsync();
        Assert.Single(history);
    }

    [Fact]
    public async Task OverdueSubject_IsFlagged_AndBodyContainsCompany()
    {
        SeedEntry(Today.AddDays(-3));
        string? subject = null, body = null;
        _email.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<EmailAttachment>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, IEnumerable<EmailAttachment>?, CancellationToken>(
                (_, s, b, _, _) => { subject = s; body = b; })
            .Returns(Task.CompletedTask);

        await RunAsync();

        Assert.NotNull(subject);
        Assert.StartsWith("[EN RETARD]", subject);
        Assert.Contains("Ma Société SARL", body);
        Assert.Contains("Déclaration TVA", body);
    }

    [Fact]
    public async Task CustomLeadDays_AreRespected()
    {
        SeedEntry(Today.AddDays(15));

        var sentDefault = await RunAsync();
        Assert.Equal(0, sentDefault);

        var sentCustom = await RunAsync(leadDays: [15]);
        Assert.Equal(1, sentCustom);
    }

    // ── Canal WhatsApp additif (Channels.FiscalWhatsAppRemindersEnabled) ──

    [Fact]
    public async Task WhatsApp_NeverCalled_WhenNoChatMapping()
    {
        // whatsAppChatsById null (flag OFF côté job) = comportement historique e-mail seul.
        SeedEntry(Today.AddDays(1));

        var sent = await RunAsync();

        Assert.Equal(1, sent);
        _channelSender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task WhatsApp_Sent_WhenResponsibleIsLinked()
    {
        SeedEntry(Today.AddDays(1));
        _channelSender.Setup(s => s.SendWhatsAppTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        string? sentText = null;
        _channelSender.Setup(s => s.SendWhatsAppTextAsync("21612345678@c.us", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, text, _) => sentText = text)
            .ReturnsAsync(true);

        await using var ctx = _factory.CreateContext();
        var sent = await BuildService().ProcessTenantAsync(
            ctx, Users(), "Ma Société SARL", Today,
            new Dictionary<Guid, string> { [ResponsibleId] = "21612345678@c.us" });

        Assert.Equal(1, sent);
        _channelSender.Verify(
            s => s.SendWhatsAppTextAsync("21612345678@c.us", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        // L'e-mail reste le canal primaire : toujours envoyé.
        _email.Verify(e => e.SendEmailAsync(
            "expert@cabinet.tn", It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IEnumerable<EmailAttachment>?>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(sentText);
        Assert.Contains("Ma Société SARL", sentText);
        Assert.Contains("Déclaration TVA", sentText);
    }

    [Fact]
    public async Task WhatsApp_NotSent_ForUnlinkedResponsible()
    {
        SeedEntry(Today.AddDays(1));

        await using var ctx = _factory.CreateContext();
        var sent = await BuildService().ProcessTenantAsync(
            ctx, Users(), "Ma Société SARL", Today,
            new Dictionary<Guid, string> { [Guid.NewGuid()] = "21699999999@c.us" });

        Assert.Equal(1, sent); // e-mail seul
        _channelSender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task WhatsApp_Failure_DoesNotBlock_EmailMarking()
    {
        var entryId = SeedEntry(Today.AddDays(1));
        _channelSender.Setup(s => s.SendWhatsAppTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await using (var ctx = _factory.CreateContext())
        {
            var sent = await BuildService().ProcessTenantAsync(
                ctx, Users(), "Ma Société SARL", Today,
                new Dictionary<Guid, string> { [ResponsibleId] = "21612345678@c.us" });
            Assert.Equal(1, sent);
        }

        await using var verify = _factory.CreateContext();
        var entry = await verify.FiscalScheduleEntries.SingleAsync(e => e.Id == entryId);
        Assert.NotNull(entry.LastReminderAt);
        Assert.Equal(FiscalReminderChannel.Email, entry.LastReminderChannel);
    }
}
