using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
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
/// Garde d'isolation de l'agent « Chef de mission ». Le point vital : un collaborateur ne doit
/// JAMAIS voir un dossier qui ne lui est pas affecté, y compris à travers un outil IA — et le
/// périmètre doit venir du paramètre, jamais du contexte ambiant (indisponible en job Hangfire).
/// </summary>
public sealed class FirmPortfolioReadServiceTests
{
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CompanyA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CompanyB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid CompanyRevoked = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid AssignmentA = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    private static readonly Guid AssignmentB = Guid.Parse("b1111111-1111-1111-1111-111111111111");
    private static readonly Guid AssignmentRevoked = Guid.Parse("c1111111-1111-1111-1111-111111111111");
    private static readonly Guid AccountantA = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid AccountantOrphan = Guid.Parse("dddddddd-1111-1111-1111-111111111111");
    private static readonly Guid ManagerId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly DateTimeOffset FixedNow = new(2026, 8, 12, 9, 0, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => FixedNow;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static MasterDbContext BuildMaster()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MasterDbContext(options);
    }

    private static async Task SeedAsync(MasterDbContext db)
    {
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        var address = Address.Create("1 rue Test", "Tunis", "Tunis").Value;
        var email = Email.Create("co@example.com").Value;
        var phone = PhoneNumber.Create("20123456").Value;

        var firm = Tenant.CreateAccountingFirm("Cabinet Test", nif, address, email, phone).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(firm, FirmId);

        var companyA = Tenant.Create("Société A", nif, address, email, phone, TaxRegime.RealRegime).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(companyA, CompanyA);
        var companyB = Tenant.Create("Société B", nif, address, email, phone, TaxRegime.RealRegime).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(companyB, CompanyB);
        var companyRevoked = Tenant.Create("Société C", nif, address, email, phone, TaxRegime.RealRegime).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(companyRevoked, CompanyRevoked);

        db.Tenants.AddRange(firm, companyA, companyB, companyRevoked);

        var aA = FirmClientAssignment.Request(CompanyA, FirmId, ManagerId).Value;
        typeof(FirmClientAssignment).GetProperty(nameof(FirmClientAssignment.Id))!.SetValue(aA, AssignmentA);
        aA.Accept(ManagerId);

        var aB = FirmClientAssignment.Request(CompanyB, FirmId, ManagerId).Value;
        typeof(FirmClientAssignment).GetProperty(nameof(FirmClientAssignment.Id))!.SetValue(aB, AssignmentB);
        aB.Accept(ManagerId);

        // Dossier révoqué : ne doit apparaître pour personne, pas même le responsable.
        var aRevoked = FirmClientAssignment.Request(CompanyRevoked, FirmId, ManagerId).Value;
        typeof(FirmClientAssignment).GetProperty(nameof(FirmClientAssignment.Id))!.SetValue(aRevoked, AssignmentRevoked);
        aRevoked.Accept(ManagerId);
        aRevoked.RevokeByFirm(ManagerId);

        db.FirmClientAssignments.AddRange(aA, aB, aRevoked);

        var pfA = PermanentFile.Create(AssignmentA, FirmId, CompanyA, "Société A").Value;
        pfA.AssignAccountant(AccountantA, "Accountant A");
        var pfB = PermanentFile.Create(AssignmentB, FirmId, CompanyB, "Société B").Value;
        var pfRevoked = PermanentFile.Create(AssignmentRevoked, FirmId, CompanyRevoked, "Société C").Value;

        db.PermanentFiles.AddRange(pfA, pfB, pfRevoked);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Aucune chaîne de connexion n'est fournie : les dossiers sont donc énumérés et comptés, mais
    /// leur lecture échoue. C'est exactement ce qu'il faut pour éprouver l'ACL et la remontée
    /// d'échec partiel sans dépendre d'une base dossier réelle.
    /// </summary>
    private static FirmPortfolioReadService BuildService(
        MasterDbContext master,
        string? connectionString = null,
        int parallelism = 4)
    {
        var tenantService = new Mock<ITenantService>();
        tenantService
            .Setup(s => s.GetConnectionStringAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(connectionString);

        return new FirmPortfolioReadService(
            master,
            tenantService.Object,
            Mock.Of<ITenantDbContextFactory>(),
            new FirmDossierAccessService(master),
            Options.Create(new AccountingFirmsOptions
            {
                Enabled = true,
                FirmAgentEnabled = true,
                FirmAgentMaxParallelDossiers = parallelism
            }),
            new FixedTimeProvider(),
            NullLogger<FirmPortfolioReadService>.Instance);
    }

    private static FirmDossierAccessScope Accountant(Guid userId) =>
        FirmDossierAccessScope.ForUser(userId, UserRole.FirmAccountant);

    /// <summary>Variante de <see cref="BuildService"/> exposant le mock de <c>ITenantService</c> comme espion d'appels.</summary>
    private static (FirmPortfolioReadService Service, Mock<ITenantService> TenantServiceMock) BuildServiceWithSpy(
        MasterDbContext master,
        string? connectionString = null,
        int parallelism = 4)
    {
        var tenantService = new Mock<ITenantService>();
        tenantService
            .Setup(s => s.GetConnectionStringAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(connectionString);

        var service = new FirmPortfolioReadService(
            master,
            tenantService.Object,
            Mock.Of<ITenantDbContextFactory>(),
            new FirmDossierAccessService(master),
            Options.Create(new AccountingFirmsOptions
            {
                Enabled = true,
                FirmAgentEnabled = true,
                FirmAgentMaxParallelDossiers = parallelism
            }),
            new FixedTimeProvider(),
            NullLogger<FirmPortfolioReadService>.Instance);

        return (service, tenantService);
    }

    [Fact]
    public async Task Null_scope_sees_every_active_dossier_but_not_revoked_ones()
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var service = BuildService(db);

        var health = await service.GetDossierHealthAsync(FirmId, scope: null, topN: 50);

        Assert.Equal(2, health.TotalDossiers);
        Assert.Equal(2, health.Items.Count);
        Assert.DoesNotContain(health.Items, r => r.CompanyTenantId == CompanyRevoked);
    }

    [Fact]
    public async Task Manager_scope_sees_every_active_dossier()
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var service = BuildService(db);

        var health = await service.GetDossierHealthAsync(
            FirmId, FirmDossierAccessScope.ForUser(ManagerId, UserRole.FirmManager), topN: 50);

        Assert.Equal(2, health.Items.Count);
    }

    /// <summary>Le test le plus important du chantier : l'agent ne fuit pas de dossier.</summary>
    [Fact]
    public async Task Accountant_scope_sees_only_assigned_dossier()
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var service = BuildService(db);

        var health = await service.GetDossierHealthAsync(FirmId, Accountant(AccountantA), topN: 50);

        Assert.Single(health.Items);
        Assert.Equal(CompanyA, health.Items[0].CompanyTenantId);
        Assert.DoesNotContain(health.Items, r => r.CompanyTenantId == CompanyB);
    }

    [Fact]
    public async Task Accountant_without_any_assignment_sees_nothing()
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var service = BuildService(db);

        var overview = await service.GetOverviewAsync(FirmId, Accountant(AccountantOrphan));
        var health = await service.GetDossierHealthAsync(FirmId, Accountant(AccountantOrphan), topN: 50);
        var deadlines = await service.GetDeadlinesAsync(FirmId, Accountant(AccountantOrphan), new FirmDeadlineQuery());
        var workload = await service.GetCollaboratorWorkloadAsync(FirmId, Accountant(AccountantOrphan));

        Assert.Equal(0, overview.ActiveDossiersCount);
        Assert.Empty(health.Items);
        Assert.Empty(deadlines.Items);
        Assert.Empty(workload.Items);
        // Aucun dossier n'a été ouvert : le fan-out ne doit pas prétendre à un échec.
        Assert.Equal(0, overview.FanOut.DossiersFailed);
        Assert.False(overview.FanOut.IsPartial);
    }

    /// <summary>
    /// Un compteur à zéro ne doit pas être confondu avec « portefeuille sain » quand les bases sont
    /// injoignables — c'est le défaut de <c>FirmFiscalOpsAggregator</c> que ce service ne reproduit pas.
    /// </summary>
    [Fact]
    public async Task Unreadable_dossiers_are_reported_not_silently_counted_as_healthy()
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var service = BuildService(db);

        var overview = await service.GetOverviewAsync(FirmId, scope: null);

        Assert.Equal(0, overview.OverdueCount);
        Assert.Equal(2, overview.FanOut.DossiersFailed);
        Assert.Equal(0, overview.FanOut.DossiersRead);
        Assert.True(overview.FanOut.IsPartial);
    }

    [Fact]
    public async Task Unreadable_dossier_is_ranked_last_and_flagged()
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var service = BuildService(db);

        var health = await service.GetDossierHealthAsync(FirmId, scope: null, topN: 50);

        Assert.All(health.Items, row =>
        {
            Assert.True(row.ReadFailed);
            Assert.Equal(-1, row.RiskScore);
            // Un dossier illisible ne doit pas être présenté comme inactif : on n'en sait rien.
            Assert.False(row.IsInactive30Days);
        });
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(999, 50)]
    public async Task Top_n_is_bounded(int requested, int expectedMax)
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var service = BuildService(db);

        var health = await service.GetDossierHealthAsync(FirmId, scope: null, topN: requested);
        var deadlines = await service.GetDeadlinesAsync(
            FirmId, scope: null, new FirmDeadlineQuery { TopN = requested });

        Assert.True(health.Items.Count <= expectedMax);
        Assert.True(deadlines.Items.Count <= expectedMax);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(999)] // Borné à 16 : ParallelOptions rejetterait une valeur incohérente.
    public async Task Parallelism_setting_is_clamped_and_never_throws(int configured)
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var service = BuildService(db, parallelism: configured);

        var overview = await service.GetOverviewAsync(FirmId, scope: null);

        Assert.Equal(2, overview.ActiveDossiersCount);
    }

    /// <summary>
    /// Garde structurelle : le service ne prend pas <c>ICurrentUser</c> en dépendance. Si quelqu'un
    /// l'ajoutait, le périmètre pourrait redevenir implicite et le brief Hangfire fuiterait tout le
    /// portefeuille (hors HTTP, <c>IsAuthenticated</c> est faux ⇒ « aucun filtre »).
    /// </summary>
    [Fact]
    public void Service_does_not_depend_on_current_user()
    {
        var ctor = Assert.Single(typeof(FirmPortfolioReadService).GetConstructors());

        Assert.DoesNotContain(ctor.GetParameters(), p => p.ParameterType == typeof(ICurrentUser));
    }

    // ────────────────────── Mémoïsation par requête (Lot 2.3) ──────────────────────

    /// <summary>
    /// overview + health + workload partagent tous DefaultHorizonDays = 30 : un seul fan-out attendu
    /// (espion sur <c>ITenantService.GetConnectionStringAsync</c>, appelé une fois par dossier lu —
    /// 2 dossiers actifs dans le seed — jamais 3× ce total).
    /// </summary>
    [Fact]
    public async Task Overview_Health_Workload_ShareOneFanOut_OnSameInstance()
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var (service, tenantServiceMock) = BuildServiceWithSpy(db);

        await service.GetOverviewAsync(FirmId, scope: null);
        await service.GetDossierHealthAsync(FirmId, scope: null, topN: 50);
        await service.GetCollaboratorWorkloadAsync(FirmId, scope: null);

        tenantServiceMock.Verify(
            s => s.GetConnectionStringAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)); // 2 dossiers actifs, lus UNE seule fois au total.
    }

    /// <summary>
    /// <c>GetDeadlinesAsync</c> avec un <c>within_days</c> distinct de l'horizon par défaut (30) doit
    /// déclencher sa propre entrée de mémoïsation — c'est une lecture légitimement différente.
    /// </summary>
    [Fact]
    public async Task WithinDays_Distinct_Horizon_TriggersSeparateFanOut()
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var (service, tenantServiceMock) = BuildServiceWithSpy(db);

        await service.GetOverviewAsync(FirmId, scope: null); // horizon 30
        await service.GetDeadlinesAsync(FirmId, scope: null, new FirmDeadlineQuery { WithinDays = 7 });

        tenantServiceMock.Verify(
            s => s.GetConnectionStringAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Exactly(4)); // 2 dossiers × 2 horizons distincts (30 puis 7) = 2 fan-out séparés.
    }

    /// <summary>
    /// Deux scopes utilisateur distincts (rôle et/ou UserId différents) ne partagent jamais d'entrée
    /// de mémoïsation, même au même horizon : chacun déclenche son propre fan-out.
    /// </summary>
    [Fact]
    public async Task Distinct_User_Scopes_Produce_Distinct_Memo_Entries()
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var (service, tenantServiceMock) = BuildServiceWithSpy(db);

        await service.GetOverviewAsync(FirmId, Accountant(AccountantA)); // 1 dossier assigné
        await service.GetOverviewAsync(FirmId, FirmDossierAccessScope.ForUser(ManagerId, UserRole.FirmManager)); // 2 dossiers

        tenantServiceMock.Verify(
            s => s.GetConnectionStringAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3)); // 1 (accountant) + 2 (manager), jamais partagé.
    }

    /// <summary>
    /// Le consommateur système (<c>FirmMissionBriefingJob</c>, <c>scope: null</c>) appelant overview
    /// trois fois au même horizon sur la même instance ne déclenche qu'UN SEUL fan-out — la lecture
    /// unique partagée du job de brief est préservée par la clé "system".
    /// </summary>
    [Fact]
    public async Task System_Scope_Three_Calls_Share_One_FanOut()
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var (service, tenantServiceMock) = BuildServiceWithSpy(db);

        await service.GetOverviewAsync(FirmId, scope: null);
        await service.GetOverviewAsync(FirmId, scope: null);
        await service.GetOverviewAsync(FirmId, scope: null);

        tenantServiceMock.Verify(
            s => s.GetConnectionStringAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)); // 2 dossiers, UN seul fan-out malgré 3 appels.
    }

    /// <summary>
    /// Une entrée "system" (scope null) n'est jamais servie à un scope utilisateur : l'accountant
    /// affecté à un seul dossier ne doit JAMAIS voir les 2 dossiers du cabinet entier, même après un
    /// appel système au même horizon sur la même instance.
    /// </summary>
    [Fact]
    public async Task System_Entry_Is_Never_Served_To_A_User_Scope()
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var (service, _) = BuildServiceWithSpy(db);

        var systemHealth = await service.GetDossierHealthAsync(FirmId, scope: null, topN: 50);
        var accountantHealth = await service.GetDossierHealthAsync(FirmId, Accountant(AccountantA), topN: 50);

        Assert.Equal(2, systemHealth.Items.Count);
        Assert.Single(accountantHealth.Items);
        Assert.Equal(CompanyA, accountantHealth.Items[0].CompanyTenantId);
    }

    /// <summary>
    /// Appels concurrents (même clé) sur la même instance : la Task est mémoïsée (pas seulement le
    /// résultat), donc les appelants concurrents partagent la MÊME exécution en vol — un seul
    /// fan-out, même sous concurrence stricte.
    /// </summary>
    [Fact]
    public async Task Concurrent_Calls_With_Same_Key_Share_The_Inflight_Task()
    {
        await using var db = BuildMaster();
        await SeedAsync(db);
        var tenantService = new Mock<ITenantService>();
        tenantService
            .Setup(s => s.GetConnectionStringAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(50);
                return (string?)null;
            });
        var service = new FirmPortfolioReadService(
            db,
            tenantService.Object,
            Mock.Of<ITenantDbContextFactory>(),
            new FirmDossierAccessService(db),
            Options.Create(new AccountingFirmsOptions { Enabled = true, FirmAgentEnabled = true, FirmAgentMaxParallelDossiers = 4 }),
            new FixedTimeProvider(),
            NullLogger<FirmPortfolioReadService>.Instance);

        var overviewTask = service.GetOverviewAsync(FirmId, scope: null);
        var healthTask = service.GetDossierHealthAsync(FirmId, scope: null, topN: 50);
        var workloadTask = service.GetCollaboratorWorkloadAsync(FirmId, scope: null);
        await Task.WhenAll(overviewTask, healthTask, workloadTask);

        tenantService.Verify(
            s => s.GetConnectionStringAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)); // 2 dossiers, un seul fan-out malgré 3 appels concurrents.
    }
}
