using System.Collections.Concurrent;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Entities.Studio;
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
/// Studio IA — PR 2.1 : le filtre serveur exact <c>filterField/filterValue</c> de
/// <see cref="CustomRecordRepository.ListAsync"/> et le contrôle de paire
/// <see cref="CustomRecordRepository.ExistsWithFieldPairAsync"/> contre un SQL Server RÉEL
/// (<c>JSON_VALUE</c>, colonne calculée <c>jx_*</c>, <c>FromSqlRaw</c> composé). Vérifie :
/// clé invalide ⇒ page vide sans SQL ; valeur toujours paramétrée ; seek sur <c>[jx_&lt;clé&gt;]</c>
/// dès que l'index existe (R10) ; lignes supprimées/autres entités ignorées ; exclusion de l'id modifié.
/// Une base dédiée par classe (<see cref="SqlTestDatabase"/>) ; <c>Skipped</c> explicite sans SQL.
/// </summary>
public sealed class CustomRecordRepositoryFilterSqlTests : IClassFixture<CustomRecordRepositoryFilterSqlTests.SqlFixture>
{
    private static readonly Guid Tid = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly SqlFixture _sql;

    public CustomRecordRepositoryFilterSqlTests(SqlFixture sql) => _sql = sql;

    [SkippableFact]
    public async Task Invalid_filter_key_yields_an_empty_page_and_never_reaches_sql()
    {
        Skip.If(!_sql.CanRun, "SQL Server/LocalDB indisponible dans ce bac à sable.");

        var entityId = Guid.NewGuid();
        await _sql.SeedAsync(entityId, """{"client":"A"}""");

        var jsonIndex = new Mock<IJsonIndexManager>(MockBehavior.Strict);
        var repo = new CustomRecordRepository(_sql.Factory, jsonIndex.Object);

        var (items, total) = await repo.ListAsync(Tid, entityId, null, 1, 25, "client; DROP TABLE x", "A");

        Assert.Empty(items);
        Assert.Equal(0, total);
        jsonIndex.VerifyNoOtherCalls();
    }

    [SkippableFact]
    public async Task Filter_returns_only_matching_active_rows_of_the_entity_and_composes_with_search()
    {
        Skip.If(!_sql.CanRun, "SQL Server/LocalDB indisponible dans ce bac à sable.");

        var entityId = Guid.NewGuid();
        var otherEntityId = Guid.NewGuid();
        var clientA = Guid.NewGuid().ToString();
        var clientB = Guid.NewGuid().ToString();
        await _sql.SeedAsync(entityId, $$$"""{"client":"{{{clientA}}}","name":"alpha"}""");
        await _sql.SeedAsync(entityId, $$$"""{"client":"{{{clientA}}}","name":"beta"}""");
        await _sql.SeedAsync(entityId, $$$"""{"client":"{{{clientB}}}","name":"alpha"}""");
        await _sql.SeedAsync(entityId, $$$"""{"client":"{{{clientA}}}","name":"deleted"}""", deleted: true);
        await _sql.SeedAsync(otherEntityId, $$$"""{"client":"{{{clientA}}}","name":"other"}""");

        var jsonIndex = new Mock<IJsonIndexManager>();
        jsonIndex.Setup(j => j.IndexedColumnExistsAsync(Tid, "client", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var repo = new CustomRecordRepository(_sql.Factory, jsonIndex.Object);

        var (items, total) = await repo.ListAsync(Tid, entityId, null, 1, 25, "client", clientA);
        Assert.Equal(2, total);
        Assert.Equal(2, items.Count);
        Assert.All(items, r => Assert.Contains(clientA, r.DataJson));
        Assert.All(items, r => Assert.Equal(entityId, r.EntityDefinitionId));
        Assert.DoesNotContain(items, r => r.DataJson.Contains("deleted"));

        // Cumulative with the free-text search.
        var (searched, searchedTotal) = await repo.ListAsync(Tid, entityId, "beta", 1, 25, "client", clientA);
        Assert.Equal(1, searchedTotal);
        Assert.Contains("beta", Assert.Single(searched).DataJson);

        // Paging still applies on top of the filter.
        var (page2, page2Total) = await repo.ListAsync(Tid, entityId, null, 2, 1, "client", clientA);
        Assert.Equal(2, page2Total);
        Assert.Single(page2);

        // A value matching nothing → empty page, not an unfiltered list.
        var (none, noneTotal) = await repo.ListAsync(Tid, entityId, null, 1, 25, "client", Guid.NewGuid().ToString());
        Assert.Empty(none);
        Assert.Equal(0, noneTotal);
    }

    [SkippableFact]
    public async Task Filter_value_is_parameterised_and_seeks_the_jx_column_once_the_index_exists()
    {
        Skip.If(!_sql.CanRun, "SQL Server/LocalDB indisponible dans ce bac à sable.");

        var entityId = Guid.NewGuid();
        var value = Guid.NewGuid().ToString();
        await _sql.SeedAsync(entityId, $$$"""{"dossier":"{{{value}}}"}""");
        await _sql.SeedAsync(entityId, $$$"""{"dossier":"{{{Guid.NewGuid()}}}"}""");

        // (a) No index yet → JSON_VALUE only, value as a DbParameter (never inlined).
        var noIndex = new Mock<IJsonIndexManager>();
        noIndex.Setup(j => j.IndexedColumnExistsAsync(Tid, "dossier", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _sql.Commands.Clear();
        var (items, total) = await new CustomRecordRepository(_sql.Factory, noIndex.Object)
            .ListAsync(Tid, entityId, null, 1, 25, "dossier", value);
        Assert.Equal(1, total);
        Assert.Contains(value, Assert.Single(items).DataJson);

        var scanSql = string.Join("\n", _sql.Commands);
        Assert.Contains("JSON_VALUE(DataJson, '$.dossier') = @p0", scanSql);
        Assert.DoesNotContain("[jx_dossier]", scanSql);
        Assert.DoesNotContain($"'{value}'", scanSql); // the value is never embedded as a literal

        // (b) Real index manager: EnsureFieldIndexAsync creates jx_dossier (+ its index); the list then seeks it.
        var manager = new JsonIndexManager(_sql.Factory, new MemoryCache(new MemoryCacheOptions()), NullLogger<JsonIndexManager>.Instance);
        await manager.EnsureFieldIndexAsync(Tid, "dossier");
        Assert.True(await manager.IndexedColumnExistsAsync(Tid, "dossier"));

        _sql.Commands.Clear();
        var (indexedItems, indexedTotal) = await new CustomRecordRepository(_sql.Factory, manager)
            .ListAsync(Tid, entityId, null, 1, 25, "dossier", value);
        Assert.Equal(1, indexedTotal);
        Assert.Contains(value, Assert.Single(indexedItems).DataJson);

        var seekSql = string.Join("\n", _sql.Commands);
        Assert.Contains("[jx_dossier] = @p0", seekSql);
        Assert.Contains("JSON_VALUE(DataJson, '$.dossier') = @p0", seekSql); // exact re-check after the seek
        Assert.DoesNotContain($"'{value}'", seekSql);

        // (c) A value longer than the indexed length never consults the index (JSON_VALUE fallback).
        var strict = new Mock<IJsonIndexManager>(MockBehavior.Strict);
        var (longItems, longTotal) = await new CustomRecordRepository(_sql.Factory, strict.Object)
            .ListAsync(Tid, entityId, null, 1, 25, "dossier", new string('x', 451));
        Assert.Empty(longItems);
        Assert.Equal(0, longTotal);
        strict.VerifyNoOtherCalls();
    }

    [SkippableFact]
    public async Task ExistsWithFieldPairAsync_detects_the_pair_ignores_deleted_rows_and_honours_exclusion()
    {
        Skip.If(!_sql.CanRun, "SQL Server/LocalDB indisponible dans ce bac à sable.");

        var junctionId = Guid.NewGuid();
        var e1 = Guid.NewGuid().ToString();
        var e2 = Guid.NewGuid().ToString();
        var p1 = Guid.NewGuid().ToString();
        var p2 = Guid.NewGuid().ToString();
        var existing = await _sql.SeedAsync(junctionId, $$$"""{"employes":"{{{e1}}}","projets":"{{{p1}}}"}""");
        await _sql.SeedAsync(junctionId, $$$"""{"employes":"{{{e1}}}","projets":"{{{p2}}}"}""");
        await _sql.SeedAsync(junctionId, $$$"""{"employes":"{{{e2}}}","projets":"{{{p1}}}"}""", deleted: true);
        await _sql.SeedAsync(Guid.NewGuid(), $$$"""{"employes":"{{{e2}}}","projets":"{{{p2}}}"}""");

        var jsonIndex = new Mock<IJsonIndexManager>();
        jsonIndex.Setup(j => j.IndexedColumnExistsAsync(Tid, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var repo = new CustomRecordRepository(_sql.Factory, jsonIndex.Object);

        // The pair (e1, p1) exists on an active row.
        Assert.True(await repo.ExistsWithFieldPairAsync(Tid, junctionId, "employes", e1, "projets", p1, null));
        // … but not when the only holder is the record being updated.
        Assert.False(await repo.ExistsWithFieldPairAsync(Tid, junctionId, "employes", e1, "projets", p1, existing));
        // A crossed pair is a different link.
        Assert.False(await repo.ExistsWithFieldPairAsync(Tid, junctionId, "employes", e1, "projets", Guid.NewGuid().ToString(), null));
        // Soft-deleted rows do not block re-linking.
        Assert.False(await repo.ExistsWithFieldPairAsync(Tid, junctionId, "employes", e2, "projets", p1, null));
        // Another junction's rows are invisible.
        Assert.False(await repo.ExistsWithFieldPairAsync(Tid, junctionId, "employes", e2, "projets", p2, null));
        // Guards: invalid key shape or the same key twice → false without SQL.
        Assert.False(await repo.ExistsWithFieldPairAsync(Tid, junctionId, "employes;--", e1, "projets", p1, null));
        Assert.False(await repo.ExistsWithFieldPairAsync(Tid, junctionId, "employes", e1, "employes", e1, null));
    }

    [SkippableFact]
    public async Task ExistsWithFieldPairAsync_uses_the_indexed_columns_when_available()
    {
        Skip.If(!_sql.CanRun, "SQL Server/LocalDB indisponible dans ce bac à sable.");

        var junctionId = Guid.NewGuid();
        var e1 = Guid.NewGuid().ToString();
        var p1 = Guid.NewGuid().ToString();
        await _sql.SeedAsync(junctionId, $$$"""{"membres":"{{{e1}}}","equipes":"{{{p1}}}"}""");

        var manager = new JsonIndexManager(_sql.Factory, new MemoryCache(new MemoryCacheOptions()), NullLogger<JsonIndexManager>.Instance);
        await manager.EnsureFieldIndexAsync(Tid, "membres");
        await manager.EnsureFieldIndexAsync(Tid, "equipes");

        var repo = new CustomRecordRepository(_sql.Factory, manager);
        Assert.True(await repo.ExistsWithFieldPairAsync(Tid, junctionId, "membres", e1, "equipes", p1, null));
        Assert.False(await repo.ExistsWithFieldPairAsync(Tid, junctionId, "membres", e1, "equipes", Guid.NewGuid().ToString(), null));
    }

    // ---- fixture ----

    /// <summary>Une base SQL dédiée à la classe ; capture le texte des commandes EF exécutées.</summary>
    public sealed class SqlFixture : IDisposable
    {
        private readonly SqlTestDatabase _db = new(nameof(CustomRecordRepositoryFilterSqlTests));

        public SqlFixture()
        {
            if (!_db.CanRun)
                return;

            Options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseSqlServer(_db.ConnectionString!)
                .LogTo(message => Commands.Enqueue(message), new[] { RelationalEventId.CommandExecuted })
                .Options;
            Factory = new SingleConnectionTenantDbContextFactory(Options);
        }

        public bool CanRun => _db.CanRun;
        public DbContextOptions<TenantDbContext> Options { get; } = null!;
        public ITenantDbContextFactory Factory { get; } = null!;
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
