using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.AI.Tools;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.AI;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Gardes de l'exécuteur des outils cabinet. Toutes les portes fermées doivent renvoyer une
/// <see cref="AiToolResult"/> en échec — jamais lever : une exception dans un outil interrompt
/// le flux SSE et laisse l'utilisateur devant une erreur générique.
/// </summary>
public sealed class FirmAgentToolExecutorTests
{
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static MasterDbContext BuildMaster() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ICurrentUser FirmManager()
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.TenantId).Returns(FirmId);
        user.SetupGet(u => u.UserId).Returns(UserId);
        user.SetupGet(u => u.Role).Returns(UserRole.FirmManager);
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.Email).Returns("manager@cabinet.tn");
        user.Setup(u => u.HasPermission(It.IsAny<string>())).Returns(true);
        return user.Object;
    }

    private static FirmAgentToolExecutor Build(
        MasterDbContext master,
        ICurrentUser currentUser,
        AccountingFirmsOptions options,
        IFirmPortfolioReadService? portfolio = null,
        IFirmDossierAccessService? dossierAccess = null,
        ITenantService? tenantService = null,
        ITenantDbContextFactory? contextFactory = null,
        IEmailService? emailService = null,
        TimeProvider? timeProvider = null,
        IFirmReminderPendingActionStore? pendingActionStore = null)
    {
        var effectiveTimeProvider = timeProvider ?? TimeProvider.System;
        return new(
            portfolio ?? Mock.Of<IFirmPortfolioReadService>(),
            dossierAccess ?? Mock.Of<IFirmDossierAccessService>(),
            tenantService ?? Mock.Of<ITenantService>(),
            contextFactory ?? Mock.Of<ITenantDbContextFactory>(),
            master,
            emailService ?? Mock.Of<IEmailService>(),
            currentUser,
            Options.Create(options),
            effectiveTimeProvider,
            pendingActionStore ?? new FirmReminderPendingActionStore(effectiveTimeProvider),
            NullLogger<FirmAgentToolExecutor>.Instance);
    }

    private static AccountingFirmsOptions Enabled() =>
        new() { Enabled = true, FirmAgentEnabled = true };

    [Fact]
    public async Task Tools_are_refused_when_agent_flag_is_off()
    {
        await using var master = BuildMaster();
        var executor = Build(master, FirmManager(), new AccountingFirmsOptions { Enabled = true, FirmAgentEnabled = false });

        var result = await executor.ExecuteAsync(FirmAgentTools.PortfolioOverview, new Dictionary<string, object?>());

        Assert.False(result.Success);
        Assert.Contains("pas activé", result.ErrorMessage);
    }

    [Fact]
    public async Task Tools_are_refused_without_firm_tenant_context()
    {
        await using var master = BuildMaster();
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.TenantId).Returns((Guid?)null);

        var executor = Build(master, user.Object, Enabled());
        var result = await executor.ExecuteAsync(FirmAgentTools.PortfolioOverview, new Dictionary<string, object?>());

        Assert.False(result.Success);
        Assert.Contains("Contexte cabinet", result.ErrorMessage);
    }

    /// <summary>
    /// Fail-closed : un rôle non cabinet ne doit PAS retomber sur « aucun filtre ». C'est le
    /// pendant, côté outil, de l'invariant que <c>FirmPortfolioReadService</c> impose au job.
    /// </summary>
    [Fact]
    public async Task Tools_are_refused_when_access_scope_cannot_be_resolved()
    {
        await using var master = BuildMaster();
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.TenantId).Returns(FirmId);
        user.SetupGet(u => u.UserId).Returns(UserId);
        user.SetupGet(u => u.Role).Returns(UserRole.Administrator); // ni FirmManager ni FirmAccountant
        user.SetupGet(u => u.IsAuthenticated).Returns(true);

        var executor = Build(master, user.Object, Enabled());
        var result = await executor.ExecuteAsync(FirmAgentTools.PortfolioOverview, new Dictionary<string, object?>());

        Assert.False(result.Success);
        Assert.Contains("Profil cabinet requis", result.ErrorMessage);
    }

    [Fact]
    public async Task Reminder_is_refused_when_its_own_flag_is_off()
    {
        await using var master = BuildMaster();
        var executor = Build(master, FirmManager(), Enabled()); // FirmAgentReminderToolEnabled = false

        var result = await executor.ExecuteAsync(
            FirmAgentTools.SendReminder,
            new Dictionary<string, object?>
            {
                ["deadline_id"] = Guid.NewGuid().ToString(),
                ["company_tenant_id"] = Guid.NewGuid().ToString()
            });

        Assert.False(result.Success);
        Assert.Contains("désactivé", result.ErrorMessage);
    }

    [Fact]
    public async Task Reminder_rejects_malformed_identifiers()
    {
        await using var master = BuildMaster();
        var options = Enabled();
        options.FirmAgentReminderToolEnabled = true;
        var executor = Build(master, FirmManager(), options);

        var result = await executor.ExecuteAsync(
            FirmAgentTools.SendReminder,
            new Dictionary<string, object?> { ["deadline_id"] = "pas-un-guid", ["company_tenant_id"] = "non-plus" });

        Assert.False(result.Success);
        Assert.Contains("invalide", result.ErrorMessage);
    }

    [Fact]
    public async Task Unknown_firm_tool_returns_error_not_exception()
    {
        await using var master = BuildMaster();
        var executor = Build(master, FirmManager(), Enabled());

        var result = await executor.ExecuteAsync("get_firm_unknown_thing", new Dictionary<string, object?>());

        Assert.False(result.Success);
    }

    /// <summary>
    /// L'outil cabinet routé par <c>AiToolExecutor</c> sans exécuteur enregistré (constructions de
    /// test historiques à 5 arguments) doit dégrader proprement.
    /// </summary>
    [Theory]
    [InlineData(FirmAgentTools.PortfolioOverview)]
    [InlineData(FirmAgentTools.FiscalDeadlines)]
    [InlineData(FirmAgentTools.DossierHealth)]
    [InlineData(FirmAgentTools.CollaboratorWorkload)]
    public async Task Ai_tool_executor_degrades_when_firm_executor_is_absent(string toolName)
    {
        var executor = new AiToolExecutor(
            Mock.Of<IMediator>(),
            NullLogger<AiToolExecutor>.Instance,
            TimeProvider.System,
            Mock.Of<ICurrentUser>(),
            Options.Create(new OllamaSettings()));

        var result = await executor.ExecuteAsync(toolName, new Dictionary<string, object?>(), AiToolExecutionContext.Empty);

        Assert.False(result.Success);
        Assert.Contains("Chef de mission", result.ErrorMessage);
    }

    [Fact]
    public void Firm_tool_names_match_the_registry()
    {
        foreach (var name in FirmAgentTools.All)
            Assert.NotNull(AiToolRegistry.GetToolDefinition(name));
    }

    // ───────────────────────── 4.1 : fenêtre anti-doublon fuseau Tunis ─────────────────────────

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void AdvanceUtc(DateTimeOffset utc) => _utcNow = utc;
    }

    private sealed class TestTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly string _databaseName;

        public TestTenantDbContextFactory(string databaseName) => _databaseName = databaseName;

        public TenantDbContext CreateContext() =>
            new(new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(_databaseName)
                .Options);
    }

    private static readonly Guid DeadlineCompanyId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid ResponsibleUserId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    /// <summary>
    /// Prépare un environnement complet (master + dossier client isolé + ACL) pour exercer le
    /// chemin réel de <c>send_fiscal_deadline_reminder</c> — pas seulement ses gardes.
    /// </summary>
    private static async Task<(MasterDbContext Master, ITenantDbContextFactory Factory, ITenantService TenantService, IFirmDossierAccessService DossierAccess, Guid DeadlineId)> SeedReminderFixtureAsync(
        DateTime? lastReminderAtUtc = null)
    {
        var master = BuildMaster();
        master.Users.Add(new ApplicationUser
        {
            Id = ResponsibleUserId,
            TenantId = DeadlineCompanyId,
            FirstName = "Nadia",
            LastName = "Trabelsi",
            UserName = "nadia.trabelsi@cabinet.tn",
            Email = "nadia.trabelsi@cabinet.tn",
            IsActive = true
        });

        var nif = NIF.Create("1234567/A/B/C/000").Value;
        var address = Address.Create("1 rue Test", "Tunis", "Tunis").Value;
        var email = Email.Create("contact@societe.tn").Value;
        var phone = PhoneNumber.Create("20123456").Value;
        var company = Tenant.Create("Société Alpha", nif, address, email, phone, TaxRegime.RealRegime).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(company, DeadlineCompanyId);
        master.Tenants.Add(company);
        await master.SaveChangesAsync();

        var databaseName = Guid.NewGuid().ToString();
        var factory = new TestTenantDbContextFactory(databaseName);

        var entry = FiscalScheduleEntry.Create(
            FiscalObligationType.MonthlyDeclaration,
            "TVA mensuelle",
            2026,
            new DateTime(2026, 8, 28),
            1250.500m,
            responsibleUserId: ResponsibleUserId,
            responsibleName: "Nadia Trabelsi").Value;

        if (lastReminderAtUtc is { } last)
        {
            var mark = entry.MarkReminder(FiscalReminderChannel.Email, last);
            Assert.True(mark.IsSuccess);
        }

        await using (var seedCtx = factory.CreateContext())
        {
            seedCtx.FiscalScheduleEntries.Add(entry);
            await seedCtx.SaveChangesAsync();
        }

        var tenantService = new Mock<ITenantService>();
        tenantService.Setup(t => t.GetConnectionStringAsync(DeadlineCompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("fake-connection-string");

        var dossierAccess = new Mock<IFirmDossierAccessService>();
        dossierAccess.Setup(d => d.CanAccessClientDossierAsync(
                FirmId, It.IsAny<FirmDossierAccessScope>(), DeadlineCompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        return (master, factory, tenantService.Object, dossierAccess.Object, entry.Id);
    }

    /// <summary>
    /// Dernier rappel à 23:30 Tunis (jour J), nouvel appel à 00:30 Tunis le lendemain — même
    /// journée UTC (le décalage Tunis/UTC est de +1h) mais journée Tunis différente : doit être
    /// autorisé. Si le code comparait sur la journée UTC brute, ce cas serait refusé à tort.
    /// </summary>
    [Fact]
    public async Task Reminder_allows_new_reminder_when_tunis_day_changed_even_if_utc_day_did_not()
    {
        // 27/08/2026 23:30 Tunis == 27/08/2026 22:30 UTC (Africa/Tunis = UTC+1, pas d'heure d'été).
        var lastReminderUtc = new DateTime(2026, 8, 27, 22, 30, 0, DateTimeKind.Utc);
        var (master, factory, tenantService, dossierAccess, deadlineId) =
            await SeedReminderFixtureAsync(lastReminderUtc);
        await using var _ = master;

        // 28/08/2026 00:15 Tunis == 27/08/2026 23:15 UTC : même journée UTC (27) que le dernier
        // rappel, mais journée Tunis différente (28 vs 27).
        var now = new DateTimeOffset(2026, 8, 27, 23, 15, 0, TimeSpan.Zero);
        var fakeTime = new FakeTimeProvider(now);

        var options = Enabled();
        options.FirmAgentReminderToolEnabled = true;
        options.FirmAgentReminderRequiresConfirmation = false; // chemin historique, envoi direct
        var executor = Build(
            master, FirmManager(), options,
            dossierAccess: dossierAccess, tenantService: tenantService, contextFactory: factory,
            timeProvider: fakeTime);

        var result = await executor.ExecuteAsync(
            FirmAgentTools.SendReminder,
            new Dictionary<string, object?>
            {
                ["deadline_id"] = deadlineId.ToString(),
                ["company_tenant_id"] = DeadlineCompanyId.ToString()
            });

        Assert.True(result.Success, result.ErrorMessage);
    }

    /// <summary>
    /// Même journée Tunis que le dernier rappel (bien que l'horodatage UTC diffère d'une heure) :
    /// doit être refusé par l'anti-doublon.
    /// </summary>
    [Fact]
    public async Task Reminder_refuses_when_same_tunis_business_day_as_last_reminder()
    {
        // 28/08/2026 08:00 Tunis == 28/08/2026 07:00 UTC.
        var lastReminderUtc = new DateTime(2026, 8, 28, 7, 0, 0, DateTimeKind.Utc);
        var (master, factory, tenantService, dossierAccess, deadlineId) =
            await SeedReminderFixtureAsync(lastReminderUtc);
        await using var _ = master;

        // 28/08/2026 20:00 Tunis == 28/08/2026 19:00 UTC : même journée Tunis (28).
        var now = new DateTimeOffset(2026, 8, 28, 19, 0, 0, TimeSpan.Zero);
        var fakeTime = new FakeTimeProvider(now);

        var options = Enabled();
        options.FirmAgentReminderToolEnabled = true;
        var executor = Build(
            master, FirmManager(), options,
            dossierAccess: dossierAccess, tenantService: tenantService, contextFactory: factory,
            timeProvider: fakeTime);

        var result = await executor.ExecuteAsync(
            FirmAgentTools.SendReminder,
            new Dictionary<string, object?>
            {
                ["deadline_id"] = deadlineId.ToString(),
                ["company_tenant_id"] = DeadlineCompanyId.ToString()
            });

        Assert.False(result.Success);
        Assert.Contains("déjà été envoyé aujourd'hui", result.ErrorMessage);
    }

    // ───────────────────── 4.2 : affichage fr-FR à côté des montants bruts ─────────────────────

    [Fact]
    public async Task Overview_exposes_french_display_amount_alongside_raw_amount()
    {
        await using var master = BuildMaster();
        var overview = new Mock<IFirmPortfolioReadService>();
        overview.Setup(p => p.GetOverviewAsync(FirmId, It.IsAny<FirmDossierAccessScope?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirmPortfolioOverviewDto
            {
                ActiveDossiersCount = 3,
                OverdueCount = 1,
                OverdueEstimatedAmount = 12345.678m,
                DossiersWithOverdueCount = 1,
                UpcomingWithin7DaysCount = 2,
                UpcomingWithin7DaysEstimatedAmount = 999.1m,
                UpcomingAfter7DaysCount = 0,
                InactiveDossiers30DaysCount = 0,
                VatDraftsCount = 0,
                Currency = "TND"
            });

        var executor = Build(master, FirmManager(), Enabled(), portfolio: overview.Object);
        var result = await executor.ExecuteAsync(FirmAgentTools.PortfolioOverview, new Dictionary<string, object?>());

        Assert.True(result.Success, result.ErrorMessage);
        using var doc = System.Text.Json.JsonDocument.Parse(result.Data);
        var root = doc.RootElement;
        // Le JSON échappe U+00A0 en \u00a0 : on compare la valeur décodée, pas le texte brut.
        Assert.Equal("12\u00A0345,678\u00A0TND", root.GetProperty("montantEnRetardAffichage").GetString());
        Assert.Equal("999,100\u00A0TND", root.GetProperty("montantSous7JoursAffichage").GetString());
    }

    // ───────────────────── 4.3 : PREVIEW + confirmation serveur pour la relance ─────────────────

    [Fact]
    public async Task Reminder_returns_pending_confirmation_and_sends_no_email_when_confirmation_required()
    {
        var (master, factory, tenantService, dossierAccess, deadlineId) = await SeedReminderFixtureAsync();
        await using var _ = master;

        var emailService = new Mock<IEmailService>();
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 8, 27, 10, 0, 0, TimeSpan.Zero));
        var store = new FirmReminderPendingActionStore(fakeTime);

        var options = Enabled();
        options.FirmAgentReminderToolEnabled = true; // FirmAgentReminderRequiresConfirmation = true (défaut)
        var executor = Build(
            master, FirmManager(), options,
            dossierAccess: dossierAccess, tenantService: tenantService, contextFactory: factory,
            emailService: emailService.Object, timeProvider: fakeTime, pendingActionStore: store);

        var conversationId = Guid.NewGuid();
        var result = await executor.ExecuteAsync(
            FirmAgentTools.SendReminder,
            new Dictionary<string, object?>
            {
                ["deadline_id"] = deadlineId.ToString(),
                ["company_tenant_id"] = DeadlineCompanyId.ToString()
            },
            new AiToolExecutionContext(CorrelationId: "corr-1", ConversationId: conversationId));

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("enAttenteConfirmation", result.Data);
        Assert.Contains("actionEnAttente", result.Data);
        Assert.Contains("confirm_firm_reminder", result.Data);
        emailService.Verify(
            e => e.SendEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<EmailAttachment>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Confirming_a_valid_nonce_sends_the_email_and_marks_the_reminder()
    {
        var (master, factory, tenantService, dossierAccess, deadlineId) = await SeedReminderFixtureAsync();
        await using var _ = master;

        var emailService = new Mock<IEmailService>();
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 8, 27, 10, 0, 0, TimeSpan.Zero));
        var store = new FirmReminderPendingActionStore(fakeTime);

        var options = Enabled();
        options.FirmAgentReminderToolEnabled = true;
        var currentUser = FirmManager();
        var executor = Build(
            master, currentUser, options,
            dossierAccess: dossierAccess, tenantService: tenantService, contextFactory: factory,
            emailService: emailService.Object, timeProvider: fakeTime, pendingActionStore: store);

        var preview = await executor.ExecuteAsync(
            FirmAgentTools.SendReminder,
            new Dictionary<string, object?>
            {
                ["deadline_id"] = deadlineId.ToString(),
                ["company_tenant_id"] = DeadlineCompanyId.ToString()
            });
        Assert.True(preview.Success, preview.ErrorMessage);

        using var doc = System.Text.Json.JsonDocument.Parse(preview.Data);
        var nonce = doc.RootElement.GetProperty("actionEnAttente").GetProperty("nonce").GetString()!;

        var confirmation = await executor.ConfirmReminderAsync(nonce);

        Assert.True(confirmation.Success, confirmation.ErrorMessage);
        Assert.Contains("envoye", confirmation.Data);
        emailService.Verify(
            e => e.SendEmailAsync(
                "nadia.trabelsi@cabinet.tn", It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<EmailAttachment>?>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Le nonce est consommé : une seconde confirmation doit échouer.
        var secondAttempt = await executor.ConfirmReminderAsync(nonce);
        Assert.False(secondAttempt.Success);
    }

    [Fact]
    public async Task Confirming_an_unknown_or_already_consumed_nonce_is_rejected()
    {
        await using var master = BuildMaster();
        var options = Enabled();
        options.FirmAgentReminderToolEnabled = true;
        var executor = Build(master, FirmManager(), options);

        var result = await executor.ConfirmReminderAsync("nonce-inexistant");

        Assert.False(result.Success);
        Assert.Contains("plus valide", result.ErrorMessage);
    }

    [Fact]
    public async Task Confirming_someone_elses_nonce_is_rejected()
    {
        var (master, factory, tenantService, dossierAccess, deadlineId) = await SeedReminderFixtureAsync();
        await using var _ = master;

        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 8, 27, 10, 0, 0, TimeSpan.Zero));
        var store = new FirmReminderPendingActionStore(fakeTime);

        var options = Enabled();
        options.FirmAgentReminderToolEnabled = true;
        var executor = Build(
            master, FirmManager(), options,
            dossierAccess: dossierAccess, tenantService: tenantService, contextFactory: factory,
            timeProvider: fakeTime, pendingActionStore: store);

        var preview = await executor.ExecuteAsync(
            FirmAgentTools.SendReminder,
            new Dictionary<string, object?>
            {
                ["deadline_id"] = deadlineId.ToString(),
                ["company_tenant_id"] = DeadlineCompanyId.ToString()
            });

        using var doc = System.Text.Json.JsonDocument.Parse(preview.Data);
        var nonce = doc.RootElement.GetProperty("actionEnAttente").GetProperty("nonce").GetString()!;

        // Un second exécuteur, même store, mais utilisateur appelant différent.
        var otherUser = new Mock<ICurrentUser>();
        otherUser.SetupGet(u => u.TenantId).Returns(FirmId);
        otherUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        otherUser.SetupGet(u => u.Role).Returns(UserRole.FirmManager);
        otherUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        otherUser.Setup(u => u.HasPermission(Permissions.Firm.AiRemind)).Returns(true);
        var executorOther = Build(
            master, otherUser.Object, options,
            dossierAccess: dossierAccess, tenantService: tenantService, contextFactory: factory,
            timeProvider: fakeTime, pendingActionStore: store);

        var result = await executorOther.ConfirmReminderAsync(nonce);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Reminder_flag_off_sends_immediately_without_confirmation_step()
    {
        var (master, factory, tenantService, dossierAccess, deadlineId) = await SeedReminderFixtureAsync();
        await using var _ = master;

        var emailService = new Mock<IEmailService>();
        var options = Enabled();
        options.FirmAgentReminderToolEnabled = true;
        options.FirmAgentReminderRequiresConfirmation = false;
        var executor = Build(
            master, FirmManager(), options,
            dossierAccess: dossierAccess, tenantService: tenantService, contextFactory: factory,
            emailService: emailService.Object);

        var result = await executor.ExecuteAsync(
            FirmAgentTools.SendReminder,
            new Dictionary<string, object?>
            {
                ["deadline_id"] = deadlineId.ToString(),
                ["company_tenant_id"] = DeadlineCompanyId.ToString()
            });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("envoye", result.Data);
        Assert.DoesNotContain("enAttenteConfirmation", result.Data);
        emailService.Verify(
            e => e.SendEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<EmailAttachment>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ───────────────────── 1.3 : drapeau GroundedData du grounding gate ─────────────────────
    // Posé mécaniquement depuis le fan-out (DossiersRead/DossiersFailed) : fan-out totalement en échec
    // ⇒ non ancré même si l'outil retourne Ok avec un JSON non vide ; lecture partielle ou portefeuille
    // légitimement vide ⇒ ancré. Distinct de Success (toujours vrai ici) — le gate compte les lectures
    // ancrées, pas les outils tentés.

    private static FirmPortfolioOverviewDto OverviewWithFanOut(int dossiersRead, int dossiersFailed) => new()
    {
        ActiveDossiersCount = dossiersRead,
        FanOut = new FirmFanOutHealthDto { DossiersRead = dossiersRead, DossiersFailed = dossiersFailed }
    };

    [Fact]
    public async Task Overview_with_totally_failed_fanout_is_not_grounded()
    {
        await using var master = BuildMaster();
        var overview = new Mock<IFirmPortfolioReadService>();
        overview.Setup(p => p.GetOverviewAsync(FirmId, It.IsAny<FirmDossierAccessScope?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OverviewWithFanOut(dossiersRead: 0, dossiersFailed: 3));

        var executor = Build(master, FirmManager(), Enabled(), portfolio: overview.Object);
        var result = await executor.ExecuteAsync(FirmAgentTools.PortfolioOverview, new Dictionary<string, object?>());

        // L'outil réussit (Success=true) avec un JSON non vide, mais aucune lecture n'a abouti ⇒ non ancré.
        Assert.True(result.Success, result.ErrorMessage);
        Assert.False(result.GroundedData);
    }

    [Fact]
    public async Task Overview_with_partial_fanout_is_grounded()
    {
        await using var master = BuildMaster();
        var overview = new Mock<IFirmPortfolioReadService>();
        overview.Setup(p => p.GetOverviewAsync(FirmId, It.IsAny<FirmDossierAccessScope?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OverviewWithFanOut(dossiersRead: 2, dossiersFailed: 1));

        var executor = Build(master, FirmManager(), Enabled(), portfolio: overview.Object);
        var result = await executor.ExecuteAsync(FirmAgentTools.PortfolioOverview, new Dictionary<string, object?>());

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.GroundedData); // au moins un dossier lu ⇒ ancré (l'incomplétude est signalée par lectureIncomplete)
    }

    [Fact]
    public async Task Overview_with_legitimately_empty_portfolio_is_grounded()
    {
        await using var master = BuildMaster();
        var overview = new Mock<IFirmPortfolioReadService>();
        overview.Setup(p => p.GetOverviewAsync(FirmId, It.IsAny<FirmDossierAccessScope?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OverviewWithFanOut(dossiersRead: 0, dossiersFailed: 0)); // aucun dossier, aucun échec

        var executor = Build(master, FirmManager(), Enabled(), portfolio: overview.Object);
        var result = await executor.ExecuteAsync(FirmAgentTools.PortfolioOverview, new Dictionary<string, object?>());

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.GroundedData); // « aucun dossier » est une vraie donnée ⇒ ancré
    }

    [Fact]
    public async Task Overview_payload_exposes_dossiersEcheanceSous7Jours_aggregate()
    {
        await using var master = BuildMaster();
        var overview = new Mock<IFirmPortfolioReadService>();
        overview.Setup(p => p.GetOverviewAsync(FirmId, It.IsAny<FirmDossierAccessScope?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirmPortfolioOverviewDto
            {
                ActiveDossiersCount = 5,
                DossiersEcheanceSous7JoursCount = 3,
                FanOut = new FirmFanOutHealthDto { DossiersRead = 5, DossiersFailed = 0 }
            });

        var executor = Build(master, FirmManager(), Enabled(), portfolio: overview.Object);
        var result = await executor.ExecuteAsync(FirmAgentTools.PortfolioOverview, new Dictionary<string, object?>());

        Assert.True(result.Success, result.ErrorMessage);
        using var doc = System.Text.Json.JsonDocument.Parse(result.Data);
        Assert.Equal(3, doc.RootElement.GetProperty("dossiersEcheanceSous7Jours").GetInt32());
    }
}
