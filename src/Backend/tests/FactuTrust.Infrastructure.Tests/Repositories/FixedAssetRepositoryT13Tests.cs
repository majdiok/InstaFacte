using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using FactuTrust.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Repositories;

/// <summary>
/// T13/C6 — repository : merge par clé (FiscalYear, PeriodMonth) préserve l'identité des lignes
/// (au lieu d'un delete+recreate) ; <see cref="FixedAssetRepository.ReplaceUnpostedScheduleLinesAsync"/>
/// préserve la ligne de l'année de cession ; projection
/// <see cref="FixedAssetRepository.GetScheduleLinesWithReversalStateAsync"/> (LEFT JOIN JournalEntries).
/// Modèle : <see cref="FixedAssetRepositoryTests"/> + <see cref="SalesOrderRepositoryTests"/>
/// (TestTenantDbContextFactory InMemory).
/// </summary>
public sealed class FixedAssetRepositoryT13Tests : IDisposable
{
    private readonly string _databaseName;
    private readonly TestTenantDbContextFactory _contextFactory;
    private readonly FixedAssetRepository _repository;

    public FixedAssetRepositoryT13Tests()
    {
        _databaseName = $"TestDb_FixedAssetT13_{Guid.NewGuid()}";
        _contextFactory = new TestTenantDbContextFactory(_databaseName);
        _repository = new FixedAssetRepository(_contextFactory);
    }

    private sealed class TestTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly string _databaseName;

        public TestTenantDbContextFactory(string databaseName)
        {
            _databaseName = databaseName;
        }

        public TenantDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(databaseName: _databaseName)
                .Options;

            return new TenantDbContext(options);
        }
    }

    private async Task<FixedAsset> CreatePersistedAssetAsync()
    {
        var category = DepreciationRateCategory.Create(
            "T13_TEST", "Catégorie test T13", 20m, "228", "2828", "68112",
            isNonDepreciable: false, sortOrder: 99);

        await using (var context = _contextFactory.CreateContext())
        {
            context.DepreciationRateCategories.Add(category);
            await context.SaveChangesAsync();
        }

        var asset = FixedAsset.Create(
            "IMMO-T13-0001", "Actif test T13", category.Id,
            20m, 5m, "228", "2828", "68112",
            10_000m, 0m, 0m, new DateTime(2026, 1, 1)).Value;
        asset.PutInService(new DateTime(2026, 1, 1), "404");
        asset.SetAuditInfo("test@factutrust.tn", isUpdate: false);
        await _repository.AddAsync(asset);
        return asset;
    }

    /// <summary>Crée une ligne d'échéancier (annuité pleine, PeriodMonth null).</summary>
    private static DepreciationScheduleLine MakeLine(Guid assetId, int year, decimal amount, decimal priorAcc) =>
        DepreciationScheduleLine.Create(assetId, year, null, 10_000m - priorAcc, 2_000m, priorAcc,
            amount, priorAcc + amount, 10_000m - priorAcc - amount).Value;

    private async Task<List<DepreciationScheduleLine>> LoadLinesAsync(Guid assetId)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.DepreciationScheduleLines
            .Where(l => l.FixedAssetId == assetId)
            .OrderBy(l => l.FiscalYear)
            .ToListAsync();
    }

    // ==================================================================
    // Merge — préservation de l'identité (T13 étape 3)
    // ==================================================================

    [Fact]
    public async Task Merge_AtIdenticalRegeneration_PreservesLineIds()
    {
        var asset = await CreatePersistedAssetAsync();

        // Échéancier initial : 2026, 2027.
        await _repository.ReplaceScheduleLinesAsync(asset.Id, new[]
        {
            MakeLine(asset.Id, 2026, 2_000m, 0m),
            MakeLine(asset.Id, 2027, 2_000m, 2_000m)
        });

        var originalIds = (await LoadLinesAsync(asset.Id)).Select(l => l.Id).ToList();
        originalIds.Should().HaveCount(2);

        // Régénération identique (mêmes clés, NOUVELLES instances avec nouveaux Guids).
        await _repository.ReplaceScheduleLinesAsync(asset.Id, new[]
        {
            MakeLine(asset.Id, 2026, 2_000m, 0m),
            MakeLine(asset.Id, 2027, 2_000m, 2_000m)
        });

        var afterMerge = await LoadLinesAsync(asset.Id);
        afterMerge.Should().HaveCount(2);
        afterMerge[0].Id.Should().Be(originalIds[0], "l'identité de la ligne 2026 est préservée par le merge");
        afterMerge[1].Id.Should().Be(originalIds[1], "l'identité de la ligne 2027 est préservée par le merge");
        // Les montants sont mis à jour en place (même valeurs ici → identiques).
        afterMerge[0].DepreciationAmount.Should().Be(2_000m);
        afterMerge[1].DepreciationAmount.Should().Be(2_000m);
    }

    [Fact]
    public async Task Merge_YearAdded_PreservesExistingLineIdAndAddsNew()
    {
        var asset = await CreatePersistedAssetAsync();

        await _repository.ReplaceScheduleLinesAsync(asset.Id, new[]
        {
            MakeLine(asset.Id, 2026, 2_000m, 0m)
        });
        var id2026 = (await LoadLinesAsync(asset.Id)).Single(l => l.FiscalYear == 2026).Id;

        // Régénération : 2026 + 2027 (année ajoutée).
        await _repository.ReplaceScheduleLinesAsync(asset.Id, new[]
        {
            MakeLine(asset.Id, 2026, 2_000m, 0m),
            MakeLine(asset.Id, 2027, 2_000m, 2_000m)
        });

        var afterMerge = await LoadLinesAsync(asset.Id);
        afterMerge.Should().HaveCount(2);
        afterMerge.Single(l => l.FiscalYear == 2026).Id.Should().Be(id2026, "la ligne 2026 existante conserve son Id");
        afterMerge.Should().Contain(l => l.FiscalYear == 2027, "la nouvelle année 2027 est ajoutée");
    }

    [Fact]
    public async Task Merge_YearRemoved_PreservesExistingLineIdAndRemovesOld()
    {
        var asset = await CreatePersistedAssetAsync();

        await _repository.ReplaceScheduleLinesAsync(asset.Id, new[]
        {
            MakeLine(asset.Id, 2026, 2_000m, 0m),
            MakeLine(asset.Id, 2027, 2_000m, 2_000m)
        });
        var id2026 = (await LoadLinesAsync(asset.Id)).Single(l => l.FiscalYear == 2026).Id;

        // Régénération : seulement 2026 (année 2027 retirée).
        await _repository.ReplaceScheduleLinesAsync(asset.Id, new[]
        {
            MakeLine(asset.Id, 2026, 2_000m, 0m)
        });

        var afterMerge = await LoadLinesAsync(asset.Id);
        afterMerge.Should().HaveCount(1);
        afterMerge.Single(l => l.FiscalYear == 2026).Id.Should().Be(id2026, "la ligne 2026 existante conserve son Id");
        afterMerge.Should().NotContain(l => l.FiscalYear == 2027, "l'année 2027 sans correspondance est supprimée");
    }

    [Fact]
    public async Task Merge_PostedLineAndSkipPostedFalse_Throws()
    {
        var asset = await CreatePersistedAssetAsync();

        // Persister une ligne postée directement dans le contexte.
        var postedLine = MakeLine(asset.Id, 2026, 2_000m, 0m);
        postedLine.MarkPosted(Guid.NewGuid(), Guid.NewGuid());
        await using (var context = _contextFactory.CreateContext())
        {
            context.DepreciationScheduleLines.Add(postedLine);
            await context.SaveChangesAsync();
        }

        // ReplaceScheduleLinesAsync (skipPostedLines: false) doit refuser.
        var act = () => _repository.ReplaceScheduleLinesAsync(asset.Id, new[]
        {
            MakeLine(asset.Id, 2026, 2_000m, 0m)
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ==================================================================
    // ReplaceUnpostedScheduleLinesAsync (T4) — préserve l'identité de la ligne de cession
    // ==================================================================

    [Fact]
    public async Task ReplaceUnposted_PreservesDisposalYearLineIdentity_LeavesPostedUntouched()
    {
        var asset = await CreatePersistedAssetAsync();

        // 2026 postée (cumul), 2027 non postée (année de cession à proratiser).
        var postedLine = MakeLine(asset.Id, 2026, 2_000m, 0m);
        postedLine.MarkPosted(Guid.NewGuid(), Guid.NewGuid());
        var unpostedLine = MakeLine(asset.Id, 2027, 2_000m, 2_000m);
        await using (var context = _contextFactory.CreateContext())
        {
            context.DepreciationScheduleLines.AddRange(postedLine, unpostedLine);
            await context.SaveChangesAsync();
        }

        var idPosted = postedLine.Id;
        var idUnposted = unpostedLine.Id;

        // Flux de cession : met à jour la ligne non postée 2027 (prorata) en place.
        var prorata2027 = MakeLine(asset.Id, 2027, 1_000m, 2_000m); // 180/360 × 2 000 = 1 000
        await _repository.ReplaceUnpostedScheduleLinesAsync(asset.Id, new[] { prorata2027 });

        var after = await LoadLinesAsync(asset.Id);
        after.Should().HaveCount(2);

        var posted = after.Single(l => l.FiscalYear == 2026);
        posted.Id.Should().Be(idPosted, "la ligne postée n'est jamais touchée");
        posted.IsPosted.Should().BeTrue();

        var disposal = after.Single(l => l.FiscalYear == 2027);
        disposal.Id.Should().Be(idUnposted, "la ligne de l'année de cession conserve son Id (merge en place)");
        disposal.IsPosted.Should().BeFalse();
        disposal.DepreciationAmount.Should().Be(1_000m, "le montant prorisé est mis à jour en place");
    }

    // ==================================================================
    // Projection GetScheduleLinesWithReversalStateAsync (T13 étape 5)
    // ==================================================================

    private static JournalEntry MakeEntry(Guid sourceEntityId, string sourceType, bool reversed)
    {
        var lines = new[]
        {
            new JournalLineInput("68112", "Dotation", 2_000m, 0, null, ThirdPartyKind.None),
            new JournalLineInput("2828", "Amort.", 0, 2_000m, null, ThirdPartyKind.None)
        };
        var entry = JournalEntry.Create(
            1, "JO", new DateTime(2026, 12, 31), "Dotation test", Guid.NewGuid(),
            isAutoGenerated: true, sourceEntityType: sourceType, sourceEntityId: sourceEntityId,
            lineInputs: lines).Value;
        if (reversed)
            entry.MarkReversedBy(Guid.NewGuid());
        return entry;
    }

    [Fact]
    public async Task Projection_PostedLineWithReversedEntry_ReturnsIsReversedTrue()
    {
        var asset = await CreatePersistedAssetAsync();
        var line = MakeLine(asset.Id, 2026, 2_000m, 0m);

        var reversedEntry = MakeEntry(line.Id, AccountingService.SourceFixedAssetDepreciation, reversed: true);
        line.MarkPosted(reversedEntry.Id, Guid.NewGuid());

        await using (var context = _contextFactory.CreateContext())
        {
            context.JournalEntries.Add(reversedEntry);
            context.DepreciationScheduleLines.Add(line);
            await context.SaveChangesAsync();
        }

        var projection = await _repository.GetScheduleLinesWithReversalStateAsync(asset.Id);

        projection.Should().HaveCount(1);
        projection[0].Line.Id.Should().Be(line.Id);
        projection[0].IsReversed.Should().BeTrue("l'écriture liée est extournée");
    }

    [Fact]
    public async Task Projection_NullJournalEntryId_ReturnsIsReversedFalse()
    {
        var asset = await CreatePersistedAssetAsync();
        var line = MakeLine(asset.Id, 2026, 2_000m, 0m); // IsPosted=false, JournalEntryId=null

        await using (var context = _contextFactory.CreateContext())
        {
            context.DepreciationScheduleLines.Add(line);
            await context.SaveChangesAsync();
        }

        var projection = await _repository.GetScheduleLinesWithReversalStateAsync(asset.Id);

        projection.Should().HaveCount(1);
        projection[0].IsReversed.Should().BeFalse("une ligne sans JournalEntryId n'a pas d'écriture extournée");
    }

    [Fact]
    public async Task Projection_AbsentEntry_ReturnsIsReversedFalse()
    {
        var asset = await CreatePersistedAssetAsync();
        var line = MakeLine(asset.Id, 2026, 2_000m, 0m);
        // JournalEntryId pointe vers un Guid qui n'existe PAS dans JournalEntries.
        line.MarkPosted(Guid.NewGuid(), Guid.NewGuid());

        await using (var context = _contextFactory.CreateContext())
        {
            context.DepreciationScheduleLines.Add(line);
            await context.SaveChangesAsync();
        }

        var projection = await _repository.GetScheduleLinesWithReversalStateAsync(asset.Id);

        projection.Should().HaveCount(1);
        projection[0].IsReversed.Should().BeFalse("une écriture introuvable → conservateur : false");
    }

    [Fact]
    public async Task Projection_PostedLineWithActiveEntry_ReturnsIsReversedFalse()
    {
        var asset = await CreatePersistedAssetAsync();
        var line = MakeLine(asset.Id, 2026, 2_000m, 0m);

        var activeEntry = MakeEntry(line.Id, AccountingService.SourceFixedAssetDepreciation, reversed: false);
        line.MarkPosted(activeEntry.Id, Guid.NewGuid());

        await using (var context = _contextFactory.CreateContext())
        {
            context.JournalEntries.Add(activeEntry);
            context.DepreciationScheduleLines.Add(line);
            await context.SaveChangesAsync();
        }

        var projection = await _repository.GetScheduleLinesWithReversalStateAsync(asset.Id);

        projection.Should().HaveCount(1);
        projection[0].IsReversed.Should().BeFalse("l'écriture liée est active (non extournée)");
    }

    public void Dispose()
    {
        using var context = _contextFactory.CreateContext();
        context.Database.EnsureDeleted();
    }
}
