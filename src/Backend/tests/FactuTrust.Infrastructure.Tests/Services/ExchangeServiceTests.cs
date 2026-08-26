using System.Runtime.CompilerServices;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Exchange;
using FactuTrust.Domain.Entities.FirmGovernance;
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
    private static readonly Guid AccountantUserId = Guid.Parse("66666666-6666-6666-6666-666666666666");

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
        => BuildServiceWithNotifications(db, dbFactory, currentUserOverride).Svc;

    private static (ExchangeService Svc, Mock<INotificationService> Notifications) BuildServiceWithNotifications(
        MasterDbContext db,
        IDbContextFactory<MasterDbContext>? dbFactory = null,
        Mock<ICurrentUser>? currentUserOverride = null)
    {
        var userManager = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        userManager.Setup(m => m.Users).Returns(db.Users);

        var factory = dbFactory ?? BuildFactoryForScoped(db);

        var dossier = new FirmDossierAccessService(db);
        var notifications = new Mock<INotificationService>();
        var snapshot = new Mock<ICompanyProfileSnapshotProvider>();
        var currentUser = currentUserOverride ?? new Mock<ICurrentUser>();

        var assignments = new FirmAssignmentService(
            db,
            notifications.Object,
            snapshot.Object,
            dossier,
            currentUser.Object,
            NullLogger<FirmAssignmentService>.Instance);

        var opposite = new ExchangeOppositePartyNotifier(
            db, notifications.Object, NullLogger<ExchangeOppositePartyNotifier>.Instance);

        var svc = new ExchangeService(
            db,
            factory,
            userManager.Object,
            dossier,
            assignments,
            notifications.Object,
            opposite,
            Options.Create(new ExchangeAttachmentsOptions
            {
                BasePath = Path.Combine(Path.GetTempPath(), "ft-exchange-tests")
            }),
            NullLogger<ExchangeService>.Instance);
        return (svc, notifications);
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

    private static async Task SeedAssignedAccountantAsync(
        MasterDbContext db, Guid? accountantId = null, bool isActive = true)
    {
        var id = accountantId ?? AccountantUserId;
        if (!await db.Users.AnyAsync(u => u.Id == id))
        {
            db.Users.Add(new ApplicationUser
            {
                Id = id,
                UserName = "collab@test.com",
                NormalizedUserName = "COLLAB@TEST.COM",
                Email = "collab@test.com",
                NormalizedEmail = "COLLAB@TEST.COM",
                FirstName = "Sami",
                LastName = "Mansour",
                TenantId = FirmId,
                IsActive = isActive,
                EmailConfirmed = true
            });
        }

        var existing = await db.PermanentFiles.FirstOrDefaultAsync(p => p.FirmClientAssignmentId == AssignmentId);
        if (existing is null)
        {
            var file = PermanentFile.Create(AssignmentId, FirmId, CompanyId, "Société A").Value;
            file.AssignAccountant(id, "Sami Mansour");
            db.PermanentFiles.Add(file);
        }
        else
        {
            existing.AssignAccountant(id, "Sami Mansour");
        }

        await db.SaveChangesAsync();
    }

    private static CreateExchangeRequestDto SampleRequest() =>
        new("Liasse fiscale", "merci", ExchangeRequestCategory.Information);

    private static MemoryStream TinyPdf() => new(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 });

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

    [Fact]
    public async Task AddRequestComment_company_non_admin_is_forbidden()
    {
        await using var db = BuildDb();
        var (_, threadId) = await SeedThreadAsync(db);
        var req = ExchangeRequest.Create(threadId, 1, "Titre", "desc",
            ExchangeRequestCategory.Information, ExchangeRequestPriority.Normal, CompanyUserId, CompanyId).Value;
        db.ExchangeRequests.Add(req);
        await db.SaveChangesAsync();
        var svc = BuildService(db);

        var result = await svc.AddRequestCommentAsync(
            threadId, req.Id, CompanyId, TenantKind.Company, CompanyUserId, "Admin",
            nameof(UserRole.SalesManager), null, new CreateExchangeRequestCommentDto("Hello"));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task AddRequestComment_and_list_succeed_for_firm()
    {
        await using var db = BuildDb();
        var (_, threadId) = await SeedThreadAsync(db);
        var req = ExchangeRequest.Create(threadId, 1, "Titre", "desc",
            ExchangeRequestCategory.Information, ExchangeRequestPriority.Normal, CompanyUserId, CompanyId).Value;
        db.ExchangeRequests.Add(req);
        await db.SaveChangesAsync();
        var svc = BuildService(db);

        var added = await svc.AddRequestCommentAsync(
            threadId, req.Id, FirmId, TenantKind.AccountingFirm, FirmUserId, "Firm Manager",
            nameof(UserRole.FirmManager), null, new CreateExchangeRequestCommentDto("Merci"));
        var second = await svc.AddRequestCommentAsync(
            threadId, req.Id, FirmId, TenantKind.AccountingFirm, FirmUserId, "Firm Manager",
            nameof(UserRole.FirmManager), null, new CreateExchangeRequestCommentDto("Vu"));

        Assert.True(added.IsSuccess);
        Assert.True(second.IsSuccess);

        var list = await svc.ListRequestCommentsAsync(
            threadId, req.Id, FirmId, TenantKind.AccountingFirm, FirmUserId,
            nameof(UserRole.FirmManager), null);
        Assert.True(list.IsSuccess);
        Assert.Equal(2, list.Value.Count);
        Assert.Equal("Merci", list.Value[0].Body);
        Assert.Equal("Vu", list.Value[1].Body);
        Assert.True(list.Value[0].CreatedAt <= list.Value[1].CreatedAt);
    }

    [Fact]
    public async Task AddRequestComment_closed_thread_is_rejected()
    {
        await using var db = BuildDb();
        var (_, threadId) = await SeedThreadAsync(db);
        var thread = await db.ExchangeThreads.FirstAsync(t => t.Id == threadId);
        Assert.True(thread.Close(FirmUserId).IsSuccess);
        await db.SaveChangesAsync();
        var req = ExchangeRequest.Create(threadId, 1, "Titre", "desc",
            ExchangeRequestCategory.Information, ExchangeRequestPriority.Normal, CompanyUserId, CompanyId).Value;
        db.ExchangeRequests.Add(req);
        await db.SaveChangesAsync();
        var svc = BuildService(db);

        var result = await svc.AddRequestCommentAsync(
            threadId, req.Id, FirmId, TenantKind.AccountingFirm, FirmUserId, "Firm Manager",
            nameof(UserRole.FirmManager), null, new CreateExchangeRequestCommentDto("Trop tard"));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateRequest_from_company_notifies_assigned_collaborator()
    {
        await using var db = BuildDb();
        var (_, threadId) = await SeedThreadAsync(db);
        await SeedAssignedAccountantAsync(db);
        var (svc, notifications) = BuildServiceWithNotifications(db);

        var result = await svc.CreateRequestAsync(
            threadId, CompanyId, TenantKind.Company, CompanyUserId, "Admin",
            nameof(UserRole.Administrator), null, SampleRequest());

        Assert.True(result.IsSuccess);
        notifications.Verify(n => n.CreateAsync(
            FirmId, null, NotificationType.ExchangeRequestCreated,
            "Nouvelle demande", "Liasse fiscale",
            It.Is<string>(l => l.Contains("/firm/exchanges/") && l.Contains("tab=demandes")),
            AccountantUserId, It.IsAny<CancellationToken>()), Times.Once);
        notifications.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<NotificationType>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateRequest_from_company_without_collaborator_notifies_firm_managers()
    {
        await using var db = BuildDb();
        var (_, threadId) = await SeedThreadAsync(db);
        var (svc, notifications) = BuildServiceWithNotifications(db);

        var result = await svc.CreateRequestAsync(
            threadId, CompanyId, TenantKind.Company, CompanyUserId, "Admin",
            nameof(UserRole.Administrator), null, SampleRequest());

        Assert.True(result.IsSuccess);
        notifications.Verify(n => n.CreateAsync(
            FirmId, nameof(UserRole.FirmManager), NotificationType.ExchangeRequestCreated,
            "Nouvelle demande", "Liasse fiscale",
            It.Is<string>(l => l.Contains("tab=demandes")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateRequest_from_firm_notifies_company_administrators()
    {
        await using var db = BuildDb();
        var (_, threadId) = await SeedThreadAsync(db);
        await SeedAssignedAccountantAsync(db);
        var (svc, notifications) = BuildServiceWithNotifications(db);

        var result = await svc.CreateRequestAsync(
            threadId, FirmId, TenantKind.AccountingFirm, FirmUserId, "Manager",
            nameof(UserRole.FirmManager), null, SampleRequest());

        Assert.True(result.IsSuccess);
        notifications.Verify(n => n.CreateAsync(
            CompanyId, nameof(UserRole.Administrator), NotificationType.ExchangeRequestCreated,
            "Nouvelle demande", "Liasse fiscale",
            It.Is<string>(l => l.StartsWith("/exchanges/") && l.Contains("tab=demandes")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadDocument_from_company_notifies_collaborator_with_documents_tab()
    {
        await using var db = BuildDb();
        var (_, threadId) = await SeedThreadAsync(db);
        await SeedAssignedAccountantAsync(db);
        var (svc, notifications) = BuildServiceWithNotifications(db);

        await using var content = TinyPdf();
        var result = await svc.UploadDocumentAsync(
            threadId, CompanyId, TenantKind.Company, CompanyUserId, "Admin",
            nameof(UserRole.Administrator), null, "bilan.pdf", "application/pdf",
            content, messageId: null, requestId: null, taskId: null);

        Assert.True(result.IsSuccess);
        notifications.Verify(n => n.CreateAsync(
            FirmId, null, NotificationType.ExchangeDocumentShared,
            "Nouveau document", "bilan.pdf",
            It.Is<string>(l => l.Contains("tab=documents")),
            AccountantUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadDocument_with_messageId_does_not_emit_document_notification()
    {
        await using var db = BuildDb();
        var (_, threadId) = await SeedThreadAsync(db);
        await SeedAssignedAccountantAsync(db);
        var (svc, notifications) = BuildServiceWithNotifications(db);

        await using var content = TinyPdf();
        var result = await svc.UploadDocumentAsync(
            threadId, CompanyId, TenantKind.Company, CompanyUserId, "Admin",
            nameof(UserRole.Administrator), null, "pj.pdf", "application/pdf",
            content, messageId: Guid.NewGuid(), requestId: null, taskId: null);

        Assert.True(result.IsSuccess);
        notifications.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<string?>(), NotificationType.ExchangeDocumentShared,
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        notifications.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<string?>(), NotificationType.ExchangeDocumentShared,
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateTask_from_company_notifies_collaborator()
    {
        await using var db = BuildDb();
        var (_, threadId) = await SeedThreadAsync(db);
        await SeedAssignedAccountantAsync(db);
        var (svc, notifications) = BuildServiceWithNotifications(db);

        var result = await svc.CreateTaskAsync(
            threadId, CompanyId, TenantKind.Company, CompanyUserId, "Admin",
            nameof(UserRole.Administrator), null, new CreateExchangeTaskDto("Relance TVA", null, null, null));

        Assert.True(result.IsSuccess);
        notifications.Verify(n => n.CreateAsync(
            FirmId, null, NotificationType.ExchangeTaskCreated,
            "Nouvelle tâche", "Relance TVA",
            It.Is<string>(l => l.Contains("tab=taches")),
            AccountantUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateTask_from_firm_notifies_company_administrators()
    {
        await using var db = BuildDb();
        var (_, threadId) = await SeedThreadAsync(db);
        var (svc, notifications) = BuildServiceWithNotifications(db);

        var result = await svc.CreateTaskAsync(
            threadId, FirmId, TenantKind.AccountingFirm, FirmUserId, "Manager",
            nameof(UserRole.FirmManager), null, new CreateExchangeTaskDto("Pièces manquantes", null, null, null));

        Assert.True(result.IsSuccess);
        notifications.Verify(n => n.CreateAsync(
            CompanyId, nameof(UserRole.Administrator), NotificationType.ExchangeTaskCreated,
            "Nouvelle tâche", "Pièces manquantes",
            It.Is<string>(l => l.StartsWith("/exchanges/") && l.Contains("tab=taches")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateRequest_succeeds_when_notification_throws()
    {
        await using var db = BuildDb();
        var (_, threadId) = await SeedThreadAsync(db);
        var (svc, notifications) = BuildServiceWithNotifications(db);
        notifications
            .Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<NotificationType>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("panne notification"));

        var result = await svc.CreateRequestAsync(
            threadId, CompanyId, TenantKind.Company, CompanyUserId, "Admin",
            nameof(UserRole.Administrator), null, SampleRequest());

        Assert.True(result.IsSuccess);
        Assert.Single(await db.ExchangeRequests.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task UploadDocument_succeeds_when_notification_throws()
    {
        await using var db = BuildDb();
        var (_, threadId) = await SeedThreadAsync(db);
        var (svc, notifications) = BuildServiceWithNotifications(db);
        notifications
            .Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<NotificationType>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("panne notification"));

        await using var content = TinyPdf();
        var result = await svc.UploadDocumentAsync(
            threadId, CompanyId, TenantKind.Company, CompanyUserId, "Admin",
            nameof(UserRole.Administrator), null, "bilan.pdf", "application/pdf",
            content, null, null, null);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task SendMessage_from_company_still_broadcasts_to_firm_without_role()
    {
        await using var db = BuildDb();
        var (_, threadId) = await SeedThreadAsync(db);
        await SeedAssignedAccountantAsync(db);
        var (svc, notifications) = BuildServiceWithNotifications(db);

        var result = await svc.SendMessageAsync(
            threadId, CompanyId, TenantKind.Company, CompanyUserId, "Admin",
            nameof(UserRole.Administrator), null, new SendExchangeMessageDto("bonjour"));

        Assert.True(result.IsSuccess);
        notifications.Verify(n => n.CreateAsync(
            FirmId, null, NotificationType.ExchangeMessageReceived,
            "Nouveau message", It.IsAny<string>(),
            It.Is<string>(l => l.Contains($"/firm/exchanges/{threadId}") && !l.Contains("tab=")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessage_from_firm_still_notifies_company_administrators()
    {
        await using var db = BuildDb();
        var (_, threadId) = await SeedThreadAsync(db);
        var (svc, notifications) = BuildServiceWithNotifications(db);

        var result = await svc.SendMessageAsync(
            threadId, FirmId, TenantKind.AccountingFirm, FirmUserId, "Manager",
            nameof(UserRole.FirmManager), null, new SendExchangeMessageDto("reçu"));

        Assert.True(result.IsSuccess);
        notifications.Verify(n => n.CreateAsync(
            CompanyId, nameof(UserRole.Administrator), NotificationType.ExchangeMessageReceived,
            "Nouveau message", It.IsAny<string>(),
            It.Is<string>(l => l.StartsWith("/exchanges/") && !l.Contains("tab=")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
