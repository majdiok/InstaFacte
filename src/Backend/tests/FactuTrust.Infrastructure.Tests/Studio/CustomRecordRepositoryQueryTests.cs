using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Features.Studio.RecordViews;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories.Studio;
using FactuTrust.Infrastructure.Services.Studio;
using FactuTrust.Infrastructure.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Studio IA — PR 2.3 : <see cref="CustomRecordRepository.QueryAsync"/> contre un SQL Server RÉEL.
/// Verrouille : filtres eq/contains/gt/between/in/is_empty, tri numérique réel (10 &gt; 9, pas « 10 » &lt; « 9 »),
/// isolation tenant/entité, lignes supprimées ignorées, <c>Total</c> exact via COUNT(*) OVER(),
/// équivalence jx_ présent/absent (le seek ne change pas le résultat). <c>Skipped</c> explicite sans SQL.
/// </summary>
public sealed class CustomRecordRepositoryQueryTests : IClassFixture<CustomRecordRepositoryQueryTests.SqlFixture>
{
    private static readonly Guid Tid = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly SqlFixture _sql;

    public CustomRecordRepositoryQueryTests(SqlFixture sql) => _sql = sql;

    private static readonly IReadOnlyDictionary<string, CustomFieldType> Types =
        new Dictionary<string, CustomFieldType>(StringComparer.Ordinal)
        {
            ["nom"] = CustomFieldType.Text,
            ["statut"] = CustomFieldType.Select,
            ["tags"] = CustomFieldType.MultiSelect,
            ["montant"] = CustomFieldType.Money,
            ["debut"] = CustomFieldType.Date
        };

    private static RecordQuerySpec Spec(
        Guid entityId,
        IReadOnlyList<RecordViewFilter>? filters = null,
        IReadOnlyList<RecordViewSort>? sort = null,
        string? search = null,
        int skip = 0, int take = 25) =>
        new(Tid, entityId,
            filters ?? Array.Empty<RecordViewFilter>(),
            sort ?? Array.Empty<RecordViewSort>(),
            search, new List<string> { "nom" },
            skip, take, new HashSet<string>(StringComparer.Ordinal));

    [SkippableFact]
    public async Task Eq_contains_and_is_empty_filter_server_side_with_exact_total()
    {
        Skip.If(!_sql.CanRun, "SQL Server/LocalDB indisponible dans ce bac à sable.");

        var entityId = Guid.NewGuid();
        await _sql.SeedAsync(entityId, """{"nom":"alpha","statut":"encours"}""");
        await _sql.SeedAsync(entityId, """{"nom":"alphabet","statut":"termine"}""");
        await _sql.SeedAsync(entityId, """{"nom":"beta"}"""); // statut absent
        await _sql.SeedAsync(entityId, """{"nom":"alpha","statut":"encours"}""", deleted: true);
        await _sql.SeedAsync(Guid.NewGuid(), """{"nom":"alpha","statut":"encours"}"""); // autre entité

        var repo = new CustomRecordRepository(_sql.Factory, _sql.JsonIndex);

        var eq = await repo.QueryAsync(Spec(entityId, filters: new[] { new RecordViewFilter("statut", "eq", JsonValue.Create("encours")) }), Types);
        Assert.Equal(1, eq.Total);
        Assert.Contains("alpha", Assert.Single(eq.Items).DataJson);

        var contains = await repo.QueryAsync(Spec(entityId, filters: new[] { new RecordViewFilter("nom", "contains", JsonValue.Create("alpha")) }), Types);
        Assert.Equal(2, contains.Total); // alpha + alphabet (la ligne supprimée est ignorée)

        var empty = await repo.QueryAsync(Spec(entityId, filters: new[] { new RecordViewFilter("statut", "is_empty", null) }), Types);
        Assert.Equal(1, empty.Total);
        Assert.Contains("beta", Assert.Single(empty.Items).DataJson);
    }

    [SkippableFact]
    public async Task Numeric_sort_is_numeric_not_lexicographic_and_total_is_the_full_match_count()
    {
        Skip.If(!_sql.CanRun, "SQL Server/LocalDB indisponible dans ce bac à sable.");

        var entityId = Guid.NewGuid();
        foreach (var amount in new[] { 9, 10, 100, 2 })
            await _sql.SeedAsync(entityId, $$$"""{"montant":{{{amount}}}}""");

        var repo = new CustomRecordRepository(_sql.Factory, _sql.JsonIndex);

        var desc = await repo.QueryAsync(Spec(entityId, sort: new[] { new RecordViewSort("montant", Descending: true) }), Types);
        Assert.Equal(4, desc.Total);
        Assert.Equal(new[] { "100", "10", "9", "2" }, desc.Items.Select(r => AmountOf(r)).ToArray());

        // gt paramétré (TRY_CONVERT) : 10 > 9 en nombre, jamais '10' < '9' en chaîne.
        var gt = await repo.QueryAsync(Spec(entityId,
            filters: new[] { new RecordViewFilter("montant", "gt", JsonValue.Create(9m)) },
            sort: new[] { new RecordViewSort("montant") }), Types);
        Assert.Equal(new[] { "10", "100" }, gt.Items.Select(r => AmountOf(r)).ToArray());

        static string? AmountOf(CustomRecord r) =>
            (System.Text.Json.Nodes.JsonNode.Parse(r.DataJson) as System.Text.Json.Nodes.JsonObject)?["montant"]?.ToJsonString();
    }

    [SkippableFact]
    public async Task Between_on_dates_and_in_on_multiselect_are_applied()
    {
        Skip.If(!_sql.CanRun, "SQL Server/LocalDB indisponible dans ce bac à sable.");

        var entityId = Guid.NewGuid();
        await _sql.SeedAsync(entityId, """{"debut":"2026-01-10","tags":["a","b"]}""");
        await _sql.SeedAsync(entityId, """{"debut":"2026-02-20","tags":["b"]}""");
        await _sql.SeedAsync(entityId, """{"debut":"2026-05-05","tags":["c"]}""");

        var repo = new CustomRecordRepository(_sql.Factory, _sql.JsonIndex);

        var between = await repo.QueryAsync(Spec(entityId, filters: new[]
        {
            new RecordViewFilter("debut", "between",
                new JsonArray(JsonValue.Create("2026-01-01"), JsonValue.Create("2026-01-31")))
        }), Types);
        Assert.Equal(1, between.Total);
        Assert.Contains("2026-01-10", Assert.Single(between.Items).DataJson);

        var inB = await repo.QueryAsync(Spec(entityId, filters: new[]
        {
            new RecordViewFilter("tags", "in", new JsonArray(JsonValue.Create("b")))
        }), Types);
        Assert.Equal(2, inB.Total);
    }

    [SkippableFact]
    public async Task Tenant_isolation_holds_and_search_targets_the_json_document()
    {
        Skip.If(!_sql.CanRun, "SQL Server/LocalDB indisponible dans ce bac à sable.");

        var entityId = Guid.NewGuid();
        await _sql.SeedAsync(entityId, """{"nom":"confidentiel"}""");

        var repo = new CustomRecordRepository(_sql.Factory, _sql.JsonIndex);

        // Autre tenant : jamais de ligne.
        var otherTenant = new RecordQuerySpec(Guid.NewGuid(), entityId,
            Array.Empty<RecordViewFilter>(), Array.Empty<RecordViewSort>(), null, new List<string>(), 0, 25, new HashSet<string>());
        var isolated = await repo.QueryAsync(otherTenant, Types);
        Assert.Equal(0, isolated.Total);

        // Recherche libre sur les clés recherchables.
        var found = await repo.QueryAsync(Spec(entityId, search: "confiden"), Types);
        Assert.Equal(1, found.Total);
        var missed = await repo.QueryAsync(Spec(entityId, search: "inexistant"), Types);
        Assert.Equal(0, missed.Total);
    }

    [SkippableFact]
    public async Task Indexed_jx_seek_returns_the_same_rows_as_the_plain_json_scan()
    {
        Skip.If(!_sql.CanRun, "SQL Server/LocalDB indisponible dans ce bac à sable.");

        var entityId = Guid.NewGuid();
        var target = Guid.NewGuid().ToString();
        await _sql.SeedAsync(entityId, $$$"""{"statut":"{{{target}}}"}""");
        await _sql.SeedAsync(entityId, """{"statut":"autre"}""");

        // Sans index (manager strict qui ne voit jamais la colonne) : scan JSON_VALUE.
        var noIndex = new Mock<IJsonIndexManager>();
        noIndex.Setup(j => j.IndexedColumnExistsAsync(Tid, "statut", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var scan = await new CustomRecordRepository(_sql.Factory, noIndex.Object)
            .QueryAsync(Spec(entityId, filters: new[] { new RecordViewFilter("statut", "eq", JsonValue.Create(target)) }), Types);
        Assert.Equal(1, scan.Total);

        // Index réel créé : la même requête passe par le seek (jx_ résolu) et renvoie la même ligne.
        var freshIndex = new JsonIndexManager(_sql.Factory, new MemoryCache(new MemoryCacheOptions()), NullLogger<JsonIndexManager>.Instance);
        await freshIndex.EnsureFieldIndexAsync(Tid, "statut");
        Assert.True(await freshIndex.IndexedColumnExistsAsync(Tid, "statut"));
        var indexedRepo = new CustomRecordRepository(_sql.Factory, freshIndex);

        var seek = await indexedRepo.QueryAsync(Spec(entityId, filters: new[] { new RecordViewFilter("statut", "eq", JsonValue.Create(target)) }), Types);
        Assert.Equal(1, seek.Total);
        Assert.Equal(scan.Items.Single().Id, seek.Items.Single().Id);
        // Équivalence fonctionnelle : le résultat est identique avec et sans index.
        // (Le texte SQL du seek [jx_statut] est verrouillé par RecordQuerySqlTests, côté constructeur pur ;
        // ici la requête est exécutée en ADO brut, non capturée par le logger EF CommandExecuted.)
    }

    // ---- fixture ----

    /// <summary>Base SQL dédiée à la classe ; capture le texte des commandes EF exécutées.</summary>
    public sealed class SqlFixture : IDisposable
    {
        private readonly SqlTestDatabase _db = new(nameof(CustomRecordRepositoryQueryTests));

        public SqlFixture()
        {
            if (!_db.CanRun)
                return;

            Options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseSqlServer(_db.ConnectionString!)
                .LogTo(message => Commands.Enqueue(message), new[] { RelationalEventId.CommandExecuted })
                .Options;
            Factory = new SingleConnectionTenantDbContextFactory(Options);
            JsonIndex = new JsonIndexManager(Factory, new MemoryCache(new MemoryCacheOptions()), NullLogger<JsonIndexManager>.Instance);
        }

        public bool CanRun => _db.CanRun;
        public DbContextOptions<TenantDbContext> Options { get; } = null!;
        public ITenantDbContextFactory Factory { get; } = null!;
        public JsonIndexManager JsonIndex { get; } = null!;
        public ConcurrentQueue<string> Commands { get; } = new();

        public async Task<Guid> SeedAsync(Guid entityId, string json, bool deleted = false)
        {
            var record = CustomRecord.Create(Tid, entityId, json, null);
            if (deleted) record.SoftDelete(null);
            await using var ctx = new TenantDbContext(Options);
            ctx.CustomRecords.Add(record);
            await ctx.SaveChangesAsync();
            return record.Id;
        }

        public void Dispose() => _db.Dispose();

        private sealed class SingleConnectionTenantDbContextFactory : ITenantDbContextFactory
        {
            private readonly DbContextOptions<TenantDbContext> _options;
            public SingleConnectionTenantDbContextFactory(DbContextOptions<TenantDbContext> options) => _options = options;
            public TenantDbContext CreateContext() => new(_options);
        }
    }
}
