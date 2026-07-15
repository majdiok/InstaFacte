using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Budgeting;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Comptabilité budgétaire — CRUD des postes, enregistrement de la grille (Initial/Révisé selon
/// le statut), validation du budget initial (copie vers Révisé) et garde du feature flag.
/// </summary>
public sealed class BudgetingFeatureTests
{
    private readonly TestTenantDbContextFactory _factory;
    private readonly BudgetRepository _repository;

    public BudgetingFeatureTests()
    {
        _factory = new TestTenantDbContextFactory($"BudgetingDb_{Guid.NewGuid()}");
        _repository = new BudgetRepository(_factory);
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

    private static IOptions<AccountingSettings> Settings(bool enabled = true) =>
        Options.Create(new AccountingSettings { BudgetingEnabled = enabled });

    private static ICurrentUser User()
    {
        var mock = new Mock<ICurrentUser>();
        mock.SetupGet(u => u.Email).Returns("comptable@cabinet.tn");
        return mock.Object;
    }

    private static IAuditService Audit() => new Mock<IAuditService>().Object;

    private async Task<Guid> SeedPostAsync(string code = "61", string label = "Services extérieurs",
        BudgetPostKind kind = BudgetPostKind.Expense, string prefixes = "61")
    {
        var post = BudgetPost.Create(code, label, kind, prefixes, 10).Value;
        post.SetAuditInfo("test", false);
        await _repository.AddPostAsync(post);
        return post.Id;
    }

    // ── CRUD postes ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePost_HappyPath_ThenDuplicateCode_Fails()
    {
        var handler = new CreateBudgetPostCommandHandler(_repository, Audit(), User(), Settings());
        var request = new CreateBudgetPostRequest { Code = "61", Label = "Services extérieurs", Kind = 0, AccountPrefixes = "61;62", DisplayOrder = 20 };

        var first = await handler.Handle(new CreateBudgetPostCommand(request), CancellationToken.None);
        Assert.True(first.IsSuccess);

        var duplicate = await handler.Handle(new CreateBudgetPostCommand(request), CancellationToken.None);
        Assert.True(duplicate.IsFailure);
        Assert.Contains("existe déjà", duplicate.Error.Description);
    }

    [Fact]
    public async Task UpdatePost_ChangesFields_AndTogglePost_FlipsActive()
    {
        var postId = await SeedPostAsync();

        var update = new UpdateBudgetPostCommandHandler(_repository, Audit(), User(), Settings());
        var updateResult = await update.Handle(new UpdateBudgetPostCommand(postId,
            new UpdateBudgetPostRequest { Label = "Services ext. & loyers", Kind = 0, AccountPrefixes = "61;613", DisplayOrder = 25 }),
            CancellationToken.None);
        Assert.True(updateResult.IsSuccess);

        var stored = await _repository.GetPostByIdAsync(postId);
        Assert.Equal("Services ext. & loyers", stored!.Label);
        Assert.Equal("61;613", stored.AccountPrefixes);

        var toggle = new ToggleBudgetPostCommandHandler(_repository, Audit(), Settings());
        Assert.True((await toggle.Handle(new ToggleBudgetPostCommand(postId), CancellationToken.None)).IsSuccess);
        Assert.False((await _repository.GetPostByIdAsync(postId))!.IsActive);
    }

    // ── Grille : enregistrement ─────────────────────────────────────────────

    [Fact]
    public async Task SaveYear_CreatesDraftYearImplicitly_AndWritesInitial()
    {
        var postId = await SeedPostAsync();
        var handler = new SaveBudgetYearCommandHandler(_repository, Audit(), User(), Settings());

        var result = await handler.Handle(new SaveBudgetYearCommand(2026, new SaveBudgetYearRequest
        {
            Lines = new[]
            {
                new SaveBudgetLineRequest { BudgetPostId = postId, Month = 1, Amount = 1000m },
                new SaveBudgetLineRequest { BudgetPostId = postId, Month = 2, Amount = 1200m }
            }
        }), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var year = await _repository.GetYearAsync(2026);
        Assert.NotNull(year);
        Assert.Equal(BudgetYearStatus.Draft, year!.Status);
        var lines = await _repository.GetLinesAsync(2026);
        Assert.Equal(2, lines.Count);
        Assert.All(lines, l => Assert.Equal(BudgetVersion.Initial, l.Version));
    }

    [Fact]
    public async Task SaveYear_ReplacesValues_AndZeroDeletesLine()
    {
        var postId = await SeedPostAsync();
        var handler = new SaveBudgetYearCommandHandler(_repository, Audit(), User(), Settings());
        await handler.Handle(new SaveBudgetYearCommand(2026, new SaveBudgetYearRequest
        {
            Lines = new[]
            {
                new SaveBudgetLineRequest { BudgetPostId = postId, Month = 1, Amount = 1000m },
                new SaveBudgetLineRequest { BudgetPostId = postId, Month = 2, Amount = 1200m }
            }
        }), CancellationToken.None);

        // Janvier passe à 900, février à 0 (suppression), mars ajouté.
        var result = await handler.Handle(new SaveBudgetYearCommand(2026, new SaveBudgetYearRequest
        {
            Lines = new[]
            {
                new SaveBudgetLineRequest { BudgetPostId = postId, Month = 1, Amount = 900m },
                new SaveBudgetLineRequest { BudgetPostId = postId, Month = 2, Amount = 0m },
                new SaveBudgetLineRequest { BudgetPostId = postId, Month = 3, Amount = 500m }
            }
        }), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var lines = await _repository.GetLinesAsync(2026);
        Assert.Equal(2, lines.Count);
        Assert.Equal(900m, lines.Single(l => l.Month == 1).Amount);
        Assert.Equal(500m, lines.Single(l => l.Month == 3).Amount);
    }

    // ── Validation de l'initial ─────────────────────────────────────────────

    [Fact]
    public async Task ValidateInitial_CopiesInitialToRevised_ThenRefusesSecondValidation()
    {
        var postId = await SeedPostAsync();
        var save = new SaveBudgetYearCommandHandler(_repository, Audit(), User(), Settings());
        await save.Handle(new SaveBudgetYearCommand(2026, new SaveBudgetYearRequest
        {
            Lines = new[]
            {
                new SaveBudgetLineRequest { BudgetPostId = postId, Month = 1, Amount = 1000m },
                new SaveBudgetLineRequest { BudgetPostId = postId, Month = 6, Amount = 2000m }
            }
        }), CancellationToken.None);

        var validate = new ValidateInitialBudgetCommandHandler(_repository, Audit(), User(), Settings());
        var first = await validate.Handle(new ValidateInitialBudgetCommand(2026), CancellationToken.None);
        Assert.True(first.IsSuccess);

        var year = await _repository.GetYearAsync(2026);
        Assert.Equal(BudgetYearStatus.Validated, year!.Status);
        Assert.Equal("comptable@cabinet.tn", year.ValidatedBy);

        var lines = await _repository.GetLinesAsync(2026);
        var initial = lines.Where(l => l.Version == BudgetVersion.Initial).OrderBy(l => l.Month).ToList();
        var revised = lines.Where(l => l.Version == BudgetVersion.Revised).OrderBy(l => l.Month).ToList();
        Assert.Equal(2, initial.Count);
        Assert.Equal(2, revised.Count);
        Assert.Equal(initial.Select(l => (l.Month, l.Amount)), revised.Select(l => (l.Month, l.Amount)));

        var second = await validate.Handle(new ValidateInitialBudgetCommand(2026), CancellationToken.None);
        Assert.True(second.IsFailure);
        Assert.Contains("déjà validé", second.Error.Description);
    }

    [Fact]
    public async Task SaveYear_AfterValidation_WritesRevised_InitialUntouched()
    {
        var postId = await SeedPostAsync();
        var save = new SaveBudgetYearCommandHandler(_repository, Audit(), User(), Settings());
        await save.Handle(new SaveBudgetYearCommand(2026, new SaveBudgetYearRequest
        {
            Lines = new[] { new SaveBudgetLineRequest { BudgetPostId = postId, Month = 1, Amount = 1000m } }
        }), CancellationToken.None);
        var validate = new ValidateInitialBudgetCommandHandler(_repository, Audit(), User(), Settings());
        await validate.Handle(new ValidateInitialBudgetCommand(2026), CancellationToken.None);

        // Révision : janvier passe à 1500 — l'initial doit rester à 1000.
        var result = await save.Handle(new SaveBudgetYearCommand(2026, new SaveBudgetYearRequest
        {
            Lines = new[] { new SaveBudgetLineRequest { BudgetPostId = postId, Month = 1, Amount = 1500m } }
        }), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var lines = await _repository.GetLinesAsync(2026);
        Assert.Equal(1000m, lines.Single(l => l.Version == BudgetVersion.Initial && l.Month == 1).Amount);
        Assert.Equal(1500m, lines.Single(l => l.Version == BudgetVersion.Revised && l.Month == 1).Amount);
    }

    // ── Grille : lecture ────────────────────────────────────────────────────

    [Fact]
    public async Task GetYear_ReturnsBothVersions_AndEditableVersion()
    {
        var postId = await SeedPostAsync();
        var save = new SaveBudgetYearCommandHandler(_repository, Audit(), User(), Settings());
        await save.Handle(new SaveBudgetYearCommand(2026, new SaveBudgetYearRequest
        {
            Lines = new[] { new SaveBudgetLineRequest { BudgetPostId = postId, Month = 3, Amount = 750m } }
        }), CancellationToken.None);

        var query = new GetBudgetYearQueryHandler(_repository, Settings());
        var grid = await query.Handle(new GetBudgetYearQuery(2026), CancellationToken.None);

        Assert.True(grid.IsSuccess);
        Assert.Equal((int)BudgetYearStatus.Draft, grid.Value.Status);
        Assert.Equal((int)BudgetVersion.Initial, grid.Value.EditableVersion);
        var row = Assert.Single(grid.Value.Rows);
        Assert.Equal(750m, row.InitialMonths[2]);
        Assert.Equal(0m, row.RevisedMonths[2]);
    }

    // ── Feature flag ────────────────────────────────────────────────────────

    [Fact]
    public async Task FlagOff_AllHandlersFail()
    {
        var postId = await SeedPostAsync();
        var off = Settings(enabled: false);
        var ct = CancellationToken.None;

        Assert.True((await new GetBudgetPostsQueryHandler(_repository, off)
            .Handle(new GetBudgetPostsQuery(false), ct)).IsFailure);
        Assert.True((await new GetBudgetYearQueryHandler(_repository, off)
            .Handle(new GetBudgetYearQuery(2026), ct)).IsFailure);
        Assert.True((await new CreateBudgetPostCommandHandler(_repository, Audit(), User(), off)
            .Handle(new CreateBudgetPostCommand(new CreateBudgetPostRequest { Code = "70", Label = "Ventes", Kind = 1, AccountPrefixes = "70" }), ct)).IsFailure);
        Assert.True((await new UpdateBudgetPostCommandHandler(_repository, Audit(), User(), off)
            .Handle(new UpdateBudgetPostCommand(postId, new UpdateBudgetPostRequest { Label = "X", Kind = 0, AccountPrefixes = "61", DisplayOrder = 1 }), ct)).IsFailure);
        Assert.True((await new ToggleBudgetPostCommandHandler(_repository, Audit(), off)
            .Handle(new ToggleBudgetPostCommand(postId), ct)).IsFailure);
        Assert.True((await new SaveBudgetYearCommandHandler(_repository, Audit(), User(), off)
            .Handle(new SaveBudgetYearCommand(2026, new SaveBudgetYearRequest
            {
                Lines = new[] { new SaveBudgetLineRequest { BudgetPostId = postId, Month = 1, Amount = 1m } }
            }), ct)).IsFailure);
        Assert.True((await new ValidateInitialBudgetCommandHandler(_repository, Audit(), User(), off)
            .Handle(new ValidateInitialBudgetCommand(2026), ct)).IsFailure);
    }
}
