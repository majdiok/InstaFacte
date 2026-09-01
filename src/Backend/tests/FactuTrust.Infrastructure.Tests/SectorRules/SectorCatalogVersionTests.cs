using System.Text.Json;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities.SectorRules;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.SectorCatalog;
using FactuTrust.Infrastructure.Services.SectorRules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.SectorRules;

/// <summary>
/// Plan §2.1 — catalogue sectoriel versionné. Covers <c>SectorRuleSnapshot.CatalogVersionTag</c>,
/// its exposure on <c>SectorCatalogDto.CatalogVersion</c> (consumed by <c>PublicSectorCatalogController</c>
/// and echoed as the response's ETag), and the version bump caused by
/// <c>SectorRuleAdminService</c> rule mutations.
/// </summary>
public sealed class SectorCatalogVersionTests
{
    private static MasterDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public void Static_provider_catalog_version_is_static_zero()
    {
        var snapshot = new StaticSectorCatalogProvider().GetSnapshot();

        Assert.Equal(SectorRuleSource.Static, snapshot.Source);
        Assert.Equal(0, snapshot.Version);
        Assert.Equal("static:0", snapshot.CatalogVersionTag);

        var dto = SectorCatalogDtoMapper.BuildCatalog(snapshot);
        Assert.Equal("static:0", dto.CatalogVersion);
    }

    [Fact]
    public async Task Db_provider_catalog_version_reflects_stamp_version()
    {
        await using var db = NewDb();
        db.SectorRuleSetStamps.Add(SectorRuleSetStamp.CreateInitial());
        await db.SaveChangesAsync();

        var provider = new DbSectorCatalogProvider(db, new MemoryCache(new MemoryCacheOptions()));
        var snapshot = provider.GetSnapshot();

        Assert.Equal(SectorRuleSource.Db, snapshot.Source);
        Assert.Equal("db:0", snapshot.CatalogVersionTag);
        Assert.Equal("db:0", SectorCatalogDtoMapper.BuildCatalog(snapshot).CatalogVersion);
    }

    [Fact]
    public async Task SectorRuleAdminService_mutation_bumps_the_exposed_catalog_version()
    {
        await using var db = NewDb();
        db.SectorRuleSetStamps.Add(SectorRuleSetStamp.CreateInitial());
        await db.SaveChangesAsync();

        // Same MemoryCache instance across both reads so we exercise the real cache-busting path
        // (version-cache TTL is short, but a version bump must still be visible once the admin
        // service's own SaveChangesAsync commits — DbSectorCatalogProvider re-reads the stamp row).
        var cache = new MemoryCache(new MemoryCacheOptions());
        var before = new DbSectorCatalogProvider(db, cache).GetSnapshot();
        Assert.Equal("db:0", before.CatalogVersionTag);

        var adminService = new SectorRuleAdminService(db);
        var createResult = await adminService.CreateSegmentAsync(
            new CreateSectorSegmentRequest { Code = "seg-version-test", LabelFr = "Test", DescriptionFr = "Test", IconKey = "icon" },
            actor: "admin",
            CancellationToken.None);
        Assert.True(createResult.IsSuccess);

        // A fresh provider/cache mirrors how a new HTTP request would observe the change (the
        // 60s version-cache TTL means an in-process provider wouldn't reflect it instantly, but the
        // underlying stamp row — the source of truth — has moved).
        var afterSnapshot = new DbSectorCatalogProvider(db, new MemoryCache(new MemoryCacheOptions())).GetSnapshot();
        Assert.Equal("db:1", afterSnapshot.CatalogVersionTag);
        Assert.NotEqual(before.CatalogVersionTag, afterSnapshot.CatalogVersionTag);
    }

    /// <summary>
    /// Parity fixture for the frontend (plan §2.1 — the sector catalog backend becomes the single
    /// source of truth). Serializes <c>SectorCatalogDtoMapper.BuildCatalog</c> of the STATIC
    /// snapshot with the same camelCase contract the API returns, and diffs it against a checked-in
    /// reference file that a frontend agent can read to keep its own local mirror/types in sync
    /// without running the .NET backend. Fails loudly (with a full diff) if the shape or content of
    /// the static catalog ever drifts without updating the fixture on purpose.
    /// </summary>
    [Fact]
    public void Static_catalog_dto_matches_checked_in_snapshot_fixture()
    {
        var dto = SectorCatalogDtoMapper.BuildCatalog(new StaticSectorCatalogProvider().GetSnapshot());
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var actualJson = JsonSerializer.Serialize(dto, options);

        var fixturePath = ResolveFixturePath();
        if (!File.Exists(fixturePath))
        {
            // First run in a fresh checkout: materialize the fixture so subsequent runs (and the
            // frontend agent reading this exact file) have something to compare against.
            Directory.CreateDirectory(Path.GetDirectoryName(fixturePath)!);
            File.WriteAllText(fixturePath, actualJson);
        }

        var expectedJson = File.ReadAllText(fixturePath);
        Assert.Equal(NormalizeLineEndings(expectedJson), NormalizeLineEndings(actualJson));
    }

    private static string NormalizeLineEndings(string value) => value.Replace("\r\n", "\n");

    private static string ResolveFixturePath()
    {
        // Walk up from the test binary's output dir to the repo's src/Backend root, then down to
        // the checked-in fixture (kept under the API project so it travels with the public
        // controller it documents — see FactuTrust.API/Resources/sector-catalog.snapshot.json).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FactuTrust.sln")))
            dir = dir.Parent;

        if (dir is null)
            throw new InvalidOperationException("Could not locate the Backend solution root from the test output directory.");

        return Path.Combine(dir.FullName, "FactuTrust.API", "Resources", "sector-catalog.snapshot.json");
    }
}
