using System.Runtime.CompilerServices;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Exchange;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class ExchangeServiceTests
{
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CompanyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid AssignmentId = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    private static readonly Guid FirmUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid CompanyUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid OtherUserId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    // Chaque MasterDbContext produit par BuildDb pointe sur une base InMemory nommée.
    // On mémorise l'association contexte→nom pour que BuildService puisse reconstruire
    // un IDbContextFactory<MasterDbContext> alimentant la même base — indispensable pour
    // que le contexte isolé du bootstrap voie les données seedées via le contexte scoped.
    private static readonly ConditionalWeakTable<MasterDbContext, string> _dbNames = new();

    private static MasterDbContext BuildDb()
    {
        var name = Guid.NewGuid().ToString();
        return BuildDb(name);
    }

    private static MasterDbContext BuildDb(string databaseName)
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var db = new MasterDbContext(options);
        _dbNames.Add(db, databaseName);
        return db;
    }

    private sealed class InMemoryContextFactory : IDbContextFactory<MasterDbContext>
    {
        private readonly string _databaseName;

        public InMemoryContextFactory(string databaseName) => _databaseName = databaseName;

        public MasterDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<MasterDbContext>()
                .UseInMemoryDatabase(_databaseName)
                .Options;
            return new MasterDbContext(options);
        }
    }

    private static async Task<(MasterDbContext Db, Guid ThreadId)> SeedThreadAsync(MasterDbContext db)
    {
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        var address = Address.Create("1 rue Test", "Tunis", "Tunis").Value;
        var email = Email.Create("co@example.com").Value;
        var phone = PhoneNumber.Create("20123456").Value;

        var firm = Tenant.CreateAccountingFirm("Cabinet Test", nif, address, email, phone).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(firm, FirmId);
        var company = Tenant.Create("Société A", nif, address, email, phone, TaxRegime.RealRegime).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(company, CompanyId);
        db.Tenants.AddRange(firm, company);

        var assignment = FirmClientAssignment.Request(CompanyId, FirmId, CompanyUserId).Value;
        typeof(FirmClientAssignment).GetProperty(nameof(FirmClientAssignment.Id))!.SetValue(assignment, AssignmentId);
        assignment.Accept(FirmUserId);
        db.FirmClientAssignments.Add(assignment);

        var thread = ExchangeThread.Create(AssignmentId, FirmId, CompanyId).Value;
        db.ExchangeThreads.Add(thread);

        db.Users.Add(new ApplicationUser
        {
            Id = FirmUserId,
            UserName = "firm@test.com",
            NormalizedUserName = "FIRM@TEST.COM",
            Email = "firm@test.com",
            NormalizedEmail = "FIRM@TEST.COM",
            FirstName = "Firm",
            LastName = "Manager",
            TenantId = FirmId,
            IsActive = true,
            EmailConfirmed = true
        });
        db.Users.Add(new ApplicationUser
        {
            Id = CompanyUserId,
            UserName = "admin@test.com",
            NormalizedUserName = "ADMIN@TEST.COM",
            Email = "admin@test.com",
            NormalizedEmail = "ADMIN@TEST.COM",
            FirstName = "Admin",
            LastName = "Company",
            TenantId = CompanyId,
            IsActive = true,
            EmailConfirmed = true
        });

        var roleAdmin = new ApplicationRole { Id = Guid.NewGuid(), Name = nameof(UserRole.Administrator), NormalizedName = "ADMINISTRATOR" };
        var roleFirm = new ApplicationRole { Id = Guid.NewGuid(), Name = nameof(UserRole.FirmManager), NormalizedName = "FIRMMANAGER" };
        db.Roles.AddRange(roleAdmin, roleFirm);
        db.Set<IdentityUserRole<Guid>>().AddRange(
            new IdentityUserRole<Guid> { UserId = CompanyUserId, RoleId = roleAdmin.Id },
            new IdentityUserRole<Guid> { UserId = FirmUserId, RoleId = roleFirm.Id });

        await db.SaveChangesAsync();
        return (db, thread.Id);
    }

    private static ExchangeService BuildService(
        MasterDbContext db,
        IDbContextFactory<MasterDbContext>? dbFactory = null,
        Mock<ICurrentUser>? currentUserOverride = null)
    {
        var userManager = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        userManager.Setup(m => m.Users).Returns(db.Users);

        // La factory par défaut pointe sur la même base InMemory que le contexte scoped —
        // c'est ce que produit le vrai DI en runtime (mêmes options, base réelle partagée).
        var factory = dbFactory ?? BuildFactoryForScoped(db);

        var dossier = new FirmDossierAccessService(db);
        var notifications = new Mock<INotificationService>();
        var snapshot = new Mock<ICompanyProfileSnapshotProvider>();
        var currentUser = currentUserOverride ?? new Mock<ICurrentUser>();

        // Vraie instance de FirmAssignmentService (au lieu du mock de l'interface) pour que
        // le bootstrap emprunte les surcharges internes GetActiveClientsAsync/GetCompanyCurrentAssignmentAsync
        // qui acceptent un MasterDbContext explicite — cœur du fix concurrence.
        var assignments = new FirmAssignmentService(
            db,
            notifications.Object,
            snapshot.Object,
            dossier,
            currentUser.Object,
            NullLogger<FirmAssignmentService>.Instance);

        return new ExchangeService(
            db,
            factory,
            userManager.Object,
            dossier,
            assignments,
            notifications.Object,
            Options.Create(new ExchangeAttachmentsOptions()),
            NullLogger<ExchangeService>.Instance);
    }

    private static IDbContextFactory<MasterDbContext> BuildFactoryForScoped(MasterDbContext scopedDb)
    {
        var name = _dbNames.TryGetValue(scopedDb, out var known)
            ? known
            : Guid.NewGuid().ToString();
        return new InMemoryContextFactory(name);
    }

    private static async Task<Guid> AddMessageAsync(
        MasterDbContext db, Guid threadId, Guid authorUserId, Guid authorTenantId, string body,
        ExchangeMessageVisibility visibility = ExchangeMessageVisibility.ClientVisible,
        DateTime? sentAt = null)
    {
        var msg = ExchangeMessage.Create(threadId, authorUserId, authorTenantId, "Author", body, visibility).Value;
        if (sentAt is { } at)
            typeof(ExchangeMessage).GetProperty(nameof(ExchangeMessage.SentAt))!.SetValue(msg, at);
        db.ExchangeMessages.Add(msg);
        await db.SaveChangesAsync();
        return msg.Id;
    }

    [Fact]
    public async Task ListThreads_unread_counts_exclude_own_and_already_read_and_internal_for_company()
    {
        await using var db = BuildDb();
        var (_, threadId) = await SeedThreadAsync(db);
        var svc = BuildService(db);

        var unreadFromFirm = await AddMessageAsync(db, threadId, FirmUserId, FirmId, "Hello client");
        await AddMessageAsync(db, threadId, CompanyUserId, CompanyId, "Reply from admin");
        await AddMessageAsync(db, threadId, FirmUserId, FirmId, "Internal", ExchangeMessageVisibility.InternalNote);
        var alreadyRead = await AddMessageAsync(db, threadId, FirmUserId, FirmId, "Already read");
        db.ExchangeMessageReads.Add(ExchangeMessageRead.Create(alreadyRead, CompanyUserId).Value);
        await db.SaveChangesAsync();

        var companyList = await svc.ListThreadsAsync(CompanyId, TenantKind.Company, null, CompanyUserId);
        Assert.Single(companyList);
        Assert.Equal(1, companyList[0].UnreadCount);

        var firmList = await svc.ListThreadsAsync(FirmId, TenantKind.AccountingFirm, null, FirmUserId);
        Assert.Single(firmList);
        // Firm sees company reply as unread (own messages excluded; internal from self excluded)
        Assert.Equal(1, firmList[0].UnreadCount);

        Assert.Equal(unreadFromFirm, unreadFromFirm); // keep analyzer happy on seed
    }

    [Fact]
    public async Task GetUnreadSummary_does_not_double_count_and_matches_list()
    {
        await using var db = BuildDb();
        var (_, threadId) = await SeedThreadAsync(db);
        var svc = BuildService(db);
        await AddMessageAsync(db, threadId, FirmUserId, FirmId, "A");
        await AddMessageAsync(db, threadId, FirmUserId, FirmId, "B");

        var list = await svc.ListThreadsAsync(CompanyId, TenantKind.Company, null, CompanyUserId);
        var summary = await svc.GetUnreadSummaryAsync(
            CompanyId, TenantKind.Company, CompanyUserId, nameof(UserRole.Administrator), null);

        Assert.Equal(list[0].UnreadCount, summary.TotalUnreadMessages);
        Assert.Equal(2, summary.TotalUnreadMessages);
    }

    [Fact]
    public async Task GetMessages_with_limit_returns_newest_page_and_hasMore()
    {
        await using var db = BuildDb();
        var (_, threadId) = await SeedThreadAsync(db);
        var svc = BuildService(db);

        for (var i = 0; i < 5; i++)
        {
            await AddMessageAsync(db, threadId, FirmUserId, FirmId, $"m{i}",
                sentAt: DateTime.UtcNow.AddMinutes(i));
        }

        var page = await svc.GetMessagesAsync(
            threadId, CompanyId, TenantKind.Company, CompanyUserId, nameof(UserRole.Administrator),
            null, after: null, before: null, limit: 3);

        Assert.True(page.IsSuccess);
        Assert.Equal(3, page.Value.Items.Count);
        Assert.True(page.Value.HasMore);
        Assert.Equal("m2", page.Value.Items[0].Body);
        Assert.Equal("m4", page.Value.Items[^1].Body);
    }

    [Fact]
    public async Task MarkMessagesReadBatch_is_idempotent()
    {
        await using var db = BuildDb();
        var (_, threadId) = await SeedThreadAsync(db);
        var svc = BuildService(db);
        var m1 = await AddMessageAsync(db, threadId, FirmUserId, FirmId, "one");
        var m2 = await AddMessageAsync(db, threadId, FirmUserId, FirmId, "two");

        var first = await svc.MarkMessagesReadBatchAsync(
            threadId, new[] { m1, m2 }, CompanyId, TenantKind.Company, CompanyUserId,
            nameof(UserRole.Administrator), null);
        Assert.True(first.IsSuccess);
        Assert.Equal(2, await db.ExchangeMessageReads.CountAsync());

        var second = await svc.MarkMessagesReadBatchAsync(
            threadId, new[] { m1, m2, m1 }, CompanyId, TenantKind.Company, CompanyUserId,
            nameof(UserRole.Administrator), null);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, await db.ExchangeMessageReads.CountAsync());
    }

    [Fact]
    public async Task MapDetail_participants_use_batch_roles()
    {
        await using var db = BuildDb();
        var (_, threadId) = await SeedThreadAsync(db);
        var svc = BuildService(db);

        var detail = await svc.GetThreadAsync(
            threadId, CompanyId, TenantKind.Company, CompanyUserId, nameof(UserRole.Administrator), null);
        Assert.True(detail.IsSuccess);
        Assert.Contains(detail.Value.Participants, p => p.UserId == CompanyUserId && p.Role == nameof(UserRole.Administrator));
        Assert.Contains(detail.Value.Participants, p => p.UserId == FirmUserId && p.Role == nameof(UserRole.FirmManager));
    }

    [Fact]
    public async Task Bootstrap_company_reuses_existing_thread_without_scoped_save()
    {
        // Fast-path: thread + assignment live only on the isolated factory DB.
        // Scoped _db is empty — if EnsureThreadAsync were called it would fail
        // (no assignment on scoped). Success proves read-only path was used.
        var isolatedFactoryDbName = Guid.NewGuid().ToString();
        Guid seededThreadId;
        await using (var seedCtx = BuildDb(isolatedFactoryDbName))
        {
            (_, seededThreadId) = await SeedThreadAsync(seedCtx);
            await AddMessageAsync(seedCtx, seededThreadId, FirmUserId, FirmId, "Existing thread message");
        }

        await using var scopedDb = BuildDb();
        var factory = new InMemoryContextFactory(isolatedFactoryDbName);
        var svc = BuildService(scopedDb, dbFactory: factory);

        var boot = await svc.BootstrapAsync(
            CompanyId, TenantKind.Company, CompanyUserId, "Admin", nameof(UserRole.Administrator),
            null, threadId: null, tab: "conversation");

        Assert.True(boot.IsSuccess);
        Assert.NotNull(boot.Value.ActiveThread);
        Assert.Equal(seededThreadId, boot.Value.ActiveThread!.Id);
        Assert.NotNull(boot.Value.Messages);
        Assert.Single(boot.Value.Messages!.Items);
        Assert.Empty(await scopedDb.ExchangeThreads.AsNoTracking().ToListAsync());
        Assert.Empty(await scopedDb.ExchangeAuditEvents.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Bootstrap_company_creates_thread_when_missing()
    {
        await using var db = BuildDb();
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        var address = Address.Create("1 rue Test", "Tunis", "Tunis").Value;
        var email = Email.Create("co@example.com").Value;
        var phone = PhoneNumber.Create("20123456").Value;

        var firm = Tenant.CreateAccountingFirm("Cabinet Test", nif, address, email, phone).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(firm, FirmId);
        var company = Tenant.Create("Société A", nif, address, email, phone, TaxRegime.RealRegime).Value;
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(company, CompanyId);
        db.Tenants.AddRange(firm, company);

        var assignment = FirmClientAssignment.Request(CompanyId, FirmId, CompanyUserId).Value;
        typeof(FirmClientAssignment).GetProperty(nameof(FirmClientAssignment.Id))!.SetValue(assignment, AssignmentId);
        assignment.Accept(FirmUserId);
        db.FirmClientAssignments.Add(assignment);

        db.Users.Add(new ApplicationUser
        {
            Id = FirmUserId,
            UserName = "firm@test.com",
            NormalizedUserName = "FIRM@TEST.COM",
            Email = "firm@test.com",
            NormalizedEmail = "FIRM@TEST.COM",
            FirstName = "Firm",
            LastName = "Manager",
            TenantId = FirmId,
            IsActive = true,
            EmailConfirmed = true
        });
        db.Users.Add(new ApplicationUser
        {
            Id = CompanyUserId,
            UserName = "admin@test.com",
            NormalizedUserName = "ADMIN@TEST.COM",
            Email = "admin@test.com",
            NormalizedEmail = "ADMIN@TEST.COM",
            FirstName = "Admin",
            LastName = "Company",
            TenantId = CompanyId,
            IsActive = true,
            EmailConfirmed = true
        });
        var roleAdmin = new ApplicationRole { Id = Guid.NewGuid(), Name = nameof(UserRole.Administrator), NormalizedName = "ADMINISTRATOR" };
        var roleFirm = new ApplicationRole { Id = Guid.NewGuid(), Name = nameof(UserRole.FirmManager), NormalizedName = "FIRMMANAGER" };
        db.Roles.AddRange(roleAdmin, roleFirm);
        db.Set<IdentityUserRole<Guid>>().AddRange(
            new IdentityUserRole<Guid> { UserId = CompanyUserId, RoleId = roleAdmin.Id },
            new IdentityUserRole<Guid> { UserId = FirmUserId, RoleId = roleFirm.Id });
        await db.SaveChangesAsync();

        Assert.Empty(await db.ExchangeThreads.AsNoTracking().ToListAsync());

        var svc = BuildService(db);
        var boot = await svc.BootstrapAsync(
            CompanyId, TenantKind.Company, CompanyUserId, "Admin", nameof(UserRole.Administrator),
            null, threadId: null, tab: "conversation");

        Assert.True(boot.IsSuccess);
        Assert.NotNull(boot.Value.ActiveThread);
        Assert.Single(await db.ExchangeThreads.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task GetMessages_with_after_caps_at_max_page_size()
    {
        await using var db = BuildDb();
        var (_, threadId) = await SeedThreadAsync(db);
        var svc = BuildService(db);

        var cursor = DateTime.UtcNow.AddHours(-1);
        for (var i = 0; i < 105; i++)
        {
            await AddMessageAsync(db, threadId, FirmUserId, FirmId, $"m{i}",
                sentAt: cursor.AddMinutes(i + 1));
        }

        var page = await svc.GetMessagesAsync(
            threadId, CompanyId, TenantKind.Company, CompanyUserId, nameof(UserRole.Administrator),
            null, after: cursor, before: null, limit: null);

        Assert.True(page.IsSuccess);
        Assert.Equal(100, page.Value.Items.Count);
        Assert.True(page.Value.HasMore);
        Assert.Equal("m0", page.Value.Items[0].Body);
        Assert.Equal("m99", page.Value.Items[^1].Body);
    }

    [Fact]
    public async Task Bootstrap_company_returns_messages_page()
    {
        await using var db = BuildDb();
        var (_, threadId) = await SeedThreadAsync(db);
        var svc = BuildService(db);
        await AddMessageAsync(db, threadId, FirmUserId, FirmId, "Hi");

        var boot = await svc.BootstrapAsync(
            CompanyId, TenantKind.Company, CompanyUserId, "Admin", nameof(UserRole.Administrator),
            null, threadId, "conversation");

        Assert.True(boot.IsSuccess);
        Assert.NotNull(boot.Value.ActiveThread);
        Assert.NotNull(boot.Value.Messages);
        Assert.Single(boot.Value.Messages!.Items);
        Assert.Null(boot.Value.EmptyHint);
    }

    [Fact]
    public async Task Bootstrap_firm_returns_threads_and_clients_without_dbcontext_concurrency()
    {
        await using var db = BuildDb();
        var (_, threadId) = await SeedThreadAsync(db);
        var svc = BuildService(db);
        await AddMessageAsync(db, threadId, CompanyUserId, CompanyId, "Bonjour cabinet");

        var boot = await svc.BootstrapAsync(
            FirmId, TenantKind.AccountingFirm, FirmUserId, "Firm Manager", nameof(UserRole.FirmManager),
            firmScope: null, threadId: null, tab: "conversation");

        Assert.True(boot.IsSuccess);
        Assert.NotNull(boot.Value.Threads);
        Assert.Contains(boot.Value.Threads!, t => t.Id == threadId);
        Assert.NotNull(boot.Value.FirmClients);
        Assert.Contains(boot.Value.FirmClients!, c => c.AssignmentId == AssignmentId);
        Assert.NotNull(boot.Value.ActiveThread);
        Assert.Equal(threadId, boot.Value.ActiveThread!.Id);
        Assert.Null(boot.Value.EmptyHint);
    }

    [Fact]
    public async Task Bootstrap_firm_returns_threads_and_empty_clients_when_accountant_lacks_dossier_permanent_file()
    {
        // Édge case observée sur le screenshot d'origine :
        //   threads.Count > 0  (visible sur l'onglet Conversations) mais
        //   clients.Count == 0 (bandeau latéral vide → aucune fiche dossier)
        // Ce cas apparaît quand un comptable cabinet (FirmAccountant) a une
        // assignment active mais aucun PermanentFile qui le désigne comme
        // accountant assigné → GetActiveClientsAsync retourne [] à cause du
        // filtre RequiresAccountantAssignmentFilter, tandis que ListThreadsAsync
        // (appelé avec firmScope=null) retourne le thread. Le bootstrap doit
        // rester 200 et exposer threads + firmClients=[] cohérents.
        await using var db = BuildDb();
        var (_, threadId) = await SeedThreadAsync(db);
        var accountantUserId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(u => u.UserId).Returns(accountantUserId);
        currentUser.SetupGet(u => u.Role).Returns(UserRole.FirmAccountant);
        currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        currentUser.SetupGet(u => u.TenantId).Returns(FirmId);
        var svc = BuildService(db, currentUserOverride: currentUser);

        var boot = await svc.BootstrapAsync(
            FirmId, TenantKind.AccountingFirm, FirmUserId, "Firm Manager", nameof(UserRole.FirmManager),
            firmScope: null, threadId: null, tab: "conversation");

        Assert.True(boot.IsSuccess);
        Assert.NotNull(boot.Value.Threads);
        Assert.NotEmpty(boot.Value.Threads!);
        Assert.Contains(boot.Value.Threads!, t => t.Id == threadId);
        Assert.NotNull(boot.Value.FirmClients);
        Assert.Empty(boot.Value.FirmClients!);
        Assert.NotNull(boot.Value.ActiveThread);
        Assert.Equal(threadId, boot.Value.ActiveThread!.Id);
        Assert.Null(boot.Value.EmptyHint);
    }

    [Fact]
    public async Task Bootstrap_uses_isolated_dbcontext_from_scoped_master()
    {
        // Preuve qu'aucune lecture du bootstrap ne passe par le MasterDbContext scoped
        // (source du bug « A second operation was started on this context instance… »).
        //
        // Montage :
        //   - La base InMemory de la factory (isolatedFactoryDbName) est seedée avec
        //     un thread + assignment actif → si le bootstrap lit via cette base, il
        //     voit le thread.
        //   - La base InMemory du contexte scoped (scopedDb) est vide → si le
        //     bootstrap lit accidentellement via _db (scoped), il ne verra rien.
        //
        // Résultat attendu : threads.Count > 0 ⇒ toutes les lectures ont bien
        // emprunté le contexte isolé produit par la factory.
        var isolatedFactoryDbName = Guid.NewGuid().ToString();
        Guid seededThreadId;
        await using (var seedCtx = BuildDb(isolatedFactoryDbName))
        {
            (_, seededThreadId) = await SeedThreadAsync(seedCtx);
            await AddMessageAsync(seedCtx, seededThreadId, CompanyUserId, CompanyId, "Ping cabinet");
        }

        await using var scopedDb = BuildDb();
        var factory = new InMemoryContextFactory(isolatedFactoryDbName);
        var svc = BuildService(scopedDb, dbFactory: factory);

        var boot = await svc.BootstrapAsync(
            FirmId, TenantKind.AccountingFirm, FirmUserId, "Firm Manager", nameof(UserRole.FirmManager),
            firmScope: null, threadId: null, tab: "conversation");

        Assert.True(boot.IsSuccess);
        Assert.NotNull(boot.Value.Threads);
        // Si les lectures utilisaient _db (scoped, base vide), on obtiendrait 0 thread.
        Assert.Contains(boot.Value.Threads!, t => t.Id == seededThreadId);
        Assert.NotNull(boot.Value.FirmClients);
        Assert.Contains(boot.Value.FirmClients!, c => c.AssignmentId == AssignmentId);
        Assert.NotNull(boot.Value.ActiveThread);
        Assert.Equal(seededThreadId, boot.Value.ActiveThread!.Id);
        // Preuve additionnelle : le contexte scoped n'a JAMAIS été utilisé pour lire des threads.
        Assert.Empty(await scopedDb.ExchangeThreads.AsNoTracking().ToListAsync());
    }
}
