using System.IdentityModel.Tokens.Jwt;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// T9 (constat B6, décision D10) — usine d'intégration dédiée à l'audit d'isolation tenant du
/// module Immobilisations.
/// <para>
/// L'hôte (<c>Program</c>) démarre <c>Hangfire</c> avec <c>SqlServerStorage</c> sur
/// <c>ConnectionStrings:MasterConnection</c> : un SQL Server joignable est donc requis pour le
/// bootstrap (aucune base métier n'est touchée — les données tenant vivent en EF InMemory). On
/// repointe la master connection vers le SQL Server de test (conteneur Docker
/// <c>factutrust-iso-sql</c>, ou <c>FACTUTRUST_TEST_MASTER_CONNECTION</c> en CI) via
/// <see cref="IWebHostBuilder.UseSetting"/>, lu pendant la construction de l'hôte (même
/// mécanisme que la clé JWT de la factory de base).
/// </para>
/// <para>
/// Les <em>données métier</em> de chaque tenant sont isolées par base EF Core InMemory <c>par
/// tenant</c> : <see cref="ScopedInMemoryTenantDbContextFactory"/> lit le tenant courant via
/// <see cref="ITenantContext"/> (renseigné par <c>TenantMiddleware</c> depuis la revendication JWT
/// <c>tenant_id</c>) et sélectionne le magasin InMemory correspondant. Aucune entrée utilisateur ne
/// peut viser une autre base : la chaîne de connexion reste résolue côté serveur (stub
/// <see cref="StubTenantService"/>), et le seul <c>CreateIsolatedContext</c> du chemin
/// Immobilisations est la surcharge <c> sans argument </c> (liée au tenant courant).
/// </para>
/// </summary>
public sealed class FixedAssetsIsolationFactory : ChannelsDisabledWebApplicationFactory
{
    public static readonly Guid TenantA = Guid.Parse("a1b2c3d4-0000-0000-0000-000000000001");
    public static readonly Guid TenantB = Guid.Parse("a1b2c3d4-0000-0000-0000-000000000002");

    // Marqueurs uniques injectés dans les libellés : détectables tels quels dans les agrégats JSON
    // ET dans les entrées (décompressées) des exports .xlsx (sharedStrings), pour l'assertion
    // d'isolation agrégat et le contrôle positif de détection.
    public const string TenantACategoryLabel = "CAT-ISO-TENANT-A-3K7";
    public const string TenantBCategoryLabel = "CAT-ISO-TENANT-B-8J2";
    public const string TenantAAssetLabel = "ASSET-ISO-TENANT-A-7Q2X";
    public const string TenantBAssetLabel = "ASSET-ISO-TENANT-B-9K5W";

    // Master connection (Hangfire bootstrap). Configurable en CI via FACTUTRUST_TEST_MASTER_CONNECTION ;
    // par défaut, le conteneur Docker factutrust-iso-sql (SQL Server 2022) lancé localement.
    private static readonly string MasterConnectionString =
        Environment.GetEnvironmentVariable("FACTUTRUST_TEST_MASTER_CONNECTION")
        ?? "Server=tcp:localhost,1433;Database=FactuTrust_Master;User Id=sa;Password=IsoTest_Str0ng_Pw1!Xy;TrustServerCertificate=True;Encrypt=True";

    public Guid TenantACategoryId { get; private set; }
    public Guid TenantAAssetId { get; private set; }
    public Guid TenantBCategoryId { get; private set; }
    public Guid TenantBAssetId { get; private set; }

    private int _seeded;

    public static string DatabaseNameFor(Guid tenantId) => $"FixedAssetsIso_{tenantId:N}";

    private static DbContextOptions<TenantDbContext> InMemoryOptions(Guid tenantId)
    {
        return new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase(DatabaseNameFor(tenantId))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Base : flags Channels OFF + clés JWT/Signature de test.
        base.ConfigureWebHost(builder);

        // Master connection : lu par Program.cs pendant la construction de l'hôte (Hangfire).
        // UseSetting rend la valeur visible à ce moment-là (idem clé JWT ci-dessus).
        builder.UseSetting("ConnectionStrings:MasterConnection", MasterConnectionString);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MasterConnection"] = MasterConnectionString,
                ["Features:FixedAssets:Enabled"] = "true"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remplace le factory tenant par un variant InMemory par tenant (routage par
            // ITenantContext.TenantId, renseigné par TenantMiddleware). Les repositories réels
            // (FixedAssetRepository, DepreciationRateCategoryRepository) consomment ce factory.
            services.RemoveAll<ITenantDbContextFactory>();
            services.AddScoped<ITenantDbContextFactory, ScopedInMemoryTenantDbContextFactory>();

            // Stub : la chaîne de connexion est résolue côté serveur (jamais depuis une entrée
            // utilisateur). On renvoie un sentinel non vide pour que TenantMiddleware appelle
            // SetTenant sans court-circuiter en 500.
            services.RemoveAll<ITenantService>();
            services.AddScoped<ITenantService, StubTenantService>();

            // Aucune migration réelle en InMemory.
            services.RemoveAll<ITenantMigrationGuard>();
            services.AddScoped<ITenantMigrationGuard, NoOpMigrationGuard>();
        });
    }

    /// <summary>Seeding idempotent (une fois par instance de fixture) des deux magasins tenant.</summary>
    public async Task SeedAsync()
    {
        if (Interlocked.Exchange(ref _seeded, 1) == 1)
            return;

        await SeedTenantAAsync();
        await SeedTenantBAsync();
    }

    private async Task SeedTenantAAsync()
    {
        var category = DepreciationRateCategory.Create(
            "ROAD_A", TenantACategoryLabel, 20m, "228", "2828", "68112", isNonDepreciable: false, sortOrder: 1);

        var asset = FixedAsset.Create(
            "IMMO-2026-0001", TenantAAssetLabel, category.Id, 20m, 5m,
            "228", "2828", "68112", 50_000m, 0m, 0m, new DateTime(2026, 1, 10)).Value;
        asset.PutInService(new DateTime(2026, 1, 15), "404");

        // Ligne d'amortissement 2026 NON comptabilisée (DepreciationAmount > 0, actif en service) :
        // un run de dotations du tenant B ne doit JAMAIS la voir ni la comptabiliser (endpoint #12).
        var line = DepreciationScheduleLine.Create(
            asset.Id, 2026, 12, 50_000m, 10_000m, 0m, 5_000m, 5_000m, 45_000m).Value;

        await using var context = new TenantDbContext(InMemoryOptions(TenantA));
        context.DepreciationRateCategories.Add(category);
        context.FixedAssets.Add(asset);
        context.DepreciationScheduleLines.Add(line);
        await context.SaveChangesAsync();

        TenantACategoryId = category.Id;
        TenantAAssetId = asset.Id;
    }

    private async Task SeedTenantBAsync()
    {
        var category = DepreciationRateCategory.Create(
            "ROAD_B", TenantBCategoryLabel, 20m, "228", "2828", "68112", isNonDepreciable: false, sortOrder: 1);

        var asset = FixedAsset.Create(
            "IMMO-2026-0001", TenantBAssetLabel, category.Id, 20m, 5m,
            "228", "2828", "68112", 30_000m, 0m, 0m, new DateTime(2026, 2, 1)).Value;
        // Laissé en brouillon : requis pour le test d'isolation du payload catégorie du PUT (#8).

        await using var context = new TenantDbContext(InMemoryOptions(TenantB));
        context.DepreciationRateCategories.Add(category);
        context.FixedAssets.Add(asset);
        await context.SaveChangesAsync();

        TenantBCategoryId = category.Id;
        TenantBAssetId = asset.Id;
    }

    /// <summary>
    /// Factory InMemory par tenant : route vers le magasin InMemory du tenant courant lu via
    /// <see cref="ITenantContext"/> (scoped, renseigné par TenantMiddleware avant l'exécution du
    /// contrôleur). Les surcharges <c>CreateIsolatedContext</c> retournent le même contexte —
    /// aucune chaîne de connexion dérivable d'une entrée utilisateur n'est jamais utilisée.
    /// </summary>
    private sealed class ScopedInMemoryTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly ITenantContext _tenantContext;

        public ScopedInMemoryTenantDbContextFactory(ITenantContext tenantContext)
            => _tenantContext = tenantContext;

        public TenantDbContext CreateContext()
        {
            var tenantId = _tenantContext.TenantId
                ?? throw new InvalidOperationException("Aucun contexte tenant pour la requête courante.");
            return new TenantDbContext(InMemoryOptions(tenantId));
        }

        public TenantDbContext CreateIsolatedContext() => CreateContext();
        public TenantDbContext CreateIsolatedContext(string connectionString) => CreateContext();
    }

    /// <summary>
    /// Stub de <see cref="ITenantService"/> : seule <see cref="GetConnectionStringAsync"/> est
    /// appelée par <c>TenantMiddleware</c> (renvoie un sentinel non vide). Les autres méthodes ne
    /// sont pas sollicitées sur les chemins Immobilisations.
    /// </summary>
    private sealed class StubTenantService : ITenantService
    {
        public Task<string?> GetConnectionStringAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>("Server=in-memory-test;Database=tenant-stub");

        public Task<string> CreateTenantDatabaseAsync(Guid tenantId, string databaseName, string? warehouseName = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<string> CreateAccountingFirmDatabaseAsync(Guid tenantId, string databaseName, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<bool> DatabaseExistsAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
        public Task ApplyMigrationsAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task TryDropDatabaseAsync(string databaseName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task EnsureTenantTemplateAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    /// <summary>Aucune migration réelle en InMemory : le garde-fou réussit toujours.</summary>
    private sealed class NoOpMigrationGuard : ITenantMigrationGuard
    {
        public Task<Result> EnsureMigrationsAppliedAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
        public void Invalidate(Guid tenantId) { }
        public void MarkApplied(Guid tenantId) { }
    }
}

/// <summary>
/// T9 (B6, D10) — Matrice exhaustive d'isolation tenant sur les 16 endpoints du module
/// Immobilisations (<c>api/accounting/fixed-assets</c>). Deux tenants (A, B) provisionnés en
/// intégration (pipeline HTTP complet : auth JWT, TenantMiddleware, politiques de permission,
/// MediatR, repositories). Pour chaque endpoint :
/// <list type="bullet">
/// <item><term>{id}</term><description>D10 : token tenant B + ID du tenant A => réponse
/// strictement identique (statut + corps) à un ID aléatoire inexistant (aucun oracle
/// d'énumération).</description></item>
/// <item><term>agrégat</term><description>les données du tenant A n'apparaissent jamais dans les
/// réponses du tenant B.</description></item>
/// <item><term>permission</term><description>rôle sans permission Accounting => 403.</description></item>
/// </list>
/// Audit : aucune faille découverte — l'isolation est par base de données (un DbContext InMemory
/// par tenant, sélectionné côté serveur depuis la revendication JWT). Ces tests en matérialisent
/// l'absence.
/// </summary>
public sealed class FixedAssetsTenantIsolationTests : IClassFixture<FixedAssetsIsolationFactory>, IAsyncLifetime
{
    private const string Base = "/api/accounting/fixed-assets";

    private readonly FixedAssetsIsolationFactory _factory;
    private readonly HttpClient _client;

    public FixedAssetsTenantIsolationTests(FixedAssetsIsolationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    // Seeding idempotent avant le premier test (les magasins InMemory sont partagés process-wide).
    public Task InitializeAsync() => _factory.SeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private Guid TenantAAssetId => _factory.TenantAAssetId;
    private Guid TenantACategoryId => _factory.TenantACategoryId;
    private Guid TenantBAssetId => _factory.TenantBAssetId;
    private static string LabelA => FixedAssetsIsolationFactory.TenantAAssetLabel;
    private static string LabelB => FixedAssetsIsolationFactory.TenantBAssetLabel;
    private static string CatLabelA => FixedAssetsIsolationFactory.TenantACategoryLabel;
    private static string CatLabelB => FixedAssetsIsolationFactory.TenantBCategoryLabel;

    // Options de désérialisation alignées sur l'API (camelCase + enums en chaînes camelCase).
    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static async Task<ApiResponse<T>?> ReadApiAsync<T>(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<ApiResponse<T>>(ApiJsonOptions);

    private static string CreateSignedJwt(Guid tenantId, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            ChannelsDisabledWebApplicationFactory.TestJwtSecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "FactuTrust",
            audience: "FactuTrust-API",
            claims: new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, role),
                new Claim("tenant_id", tenantId.ToString())
            },
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private HttpClient As(Guid tenantId, string role)
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateSignedJwt(tenantId, role));
        return _client;
    }

    private HttpClient AsTenantBAdmin() => As(FixedAssetsIsolationFactory.TenantB, "Administrator");
    private HttpClient AsTenantAAdmin() => As(FixedAssetsIsolationFactory.TenantA, "Administrator");
    private HttpClient AsTenantBWarehouse() => As(FixedAssetsIsolationFactory.TenantB, "Warehouse");

    /// <summary>
    /// D10 : les deux réponses (ID d'un autre tenant vs ID inexistant) doivent être strictement
    /// identiques (statut + corps JSON), et porter le message d'introuvable attendu.
    /// </summary>
    private static async Task AssertD10EquivalentAsync(
        HttpResponseMessage otherTenant, HttpResponseMessage nonexistent, string expectedFragment)
    {
        Assert.Equal(otherTenant.StatusCode, nonexistent.StatusCode);
        var otherBody = await otherTenant.Content.ReadAsStringAsync();
        var nonBody = await nonexistent.Content.ReadAsStringAsync();
        Assert.Equal(nonBody, otherBody);
        Assert.Contains(expectedFragment, otherBody);
    }

    /// <summary>Rôle sans permission Accounting => 403 (avant la liaison de modèle).</summary>
    private async Task AssertForbiddenAsync(HttpMethod method, string relativeUrl, object? body = null)
    {
        using var req = new HttpRequestMessage(method, relativeUrl);
        if (body is not null)
            req.Content = JsonContent.Create(body);
        using var resp = await AsTenantBWarehouse().SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    /// <summary>Recherche un libellé en clair dans les entrées (décompressées) d'un .xlsx (zip).</summary>
    private static bool ContainsTextInXlsx(byte[] xlsx, string text)
    {
        using var zip = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
        {
            using var reader = new StreamReader(entry.Open());
            if (reader.ReadToEnd().Contains(text, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static CreateFixedAssetRequest CreatePayload(Guid categoryId) => new(
        Label: "T9-Cross", DepreciationRateCategoryId: categoryId,
        AcquisitionCost: 1000m, CapitalizedFees: 0m, ResidualValue: 0m,
        AcquisitionDate: new DateTime(2026, 1, 1), Description: null, VatAmount: 0m,
        Location: null, SupplierId: null,
        AssetAccountNumber: "228", DepreciationAccountNumber: "2828", ExpenseAccountNumber: "68112");

    // ── 1. GET rate-categories (agrégat) ──────────────────────────────────────────────
    [Fact]
    public async Task T09_01_GetRateCategories_AggregateIsolation_AndForbidden()
    {
        var resp = await AsTenantBAdmin().GetAsync($"{Base}/rate-categories");
        resp.EnsureSuccessStatusCode();
        var api = await ReadApiAsync<List<DepreciationRateCategoryDto>>(resp);
        Assert.NotNull(api?.Data);
        var labels = api!.Data!.Select(c => c.Label).ToList();
        Assert.Contains(CatLabelB, labels);
        Assert.DoesNotContain(CatLabelA, labels);

        await AssertForbiddenAsync(HttpMethod.Get, $"{Base}/rate-categories");
    }

    // ── 2. GET (liste, agrégat) ────────────────────────────────────────────────────────
    [Fact]
    public async Task T09_02_GetList_AggregateIsolation_AndForbidden()
    {
        var resp = await AsTenantBAdmin().GetAsync(Base);
        resp.EnsureSuccessStatusCode();
        var api = await ReadApiAsync<FixedAssetListResponse>(resp);
        Assert.NotNull(api?.Data);
        var labels = api!.Data!.Items.Select(a => a.Label).ToList();
        Assert.Contains(LabelB, labels);
        Assert.DoesNotContain(LabelA, labels);

        await AssertForbiddenAsync(HttpMethod.Get, Base);
    }

    // ── 3. GET amortization-table (agrégat) ────────────────────────────────────────────
    [Fact]
    public async Task T09_03_GetAmortizationTable_AggregateIsolation_AndForbidden()
    {
        var resp = await AsTenantBAdmin().GetAsync($"{Base}/amortization-table?fiscalYear=2026");
        resp.EnsureSuccessStatusCode();
        var api = await ReadApiAsync<FixedAssetAmortizationTableResponse>(resp);
        Assert.NotNull(api?.Data);
        var labels = api!.Data!.Items.Select(r => r.Label).ToList();
        Assert.Contains(LabelB, labels);
        Assert.DoesNotContain(LabelA, labels);

        await AssertForbiddenAsync(HttpMethod.Get, $"{Base}/amortization-table?fiscalYear=2026");
    }

    // ── 4. GET amortization-report (agrégat) ───────────────────────────────────────────
    [Fact]
    public async Task T09_04_GetAmortizationReport_AggregateIsolation_AndForbidden()
    {
        var resp = await AsTenantBAdmin().GetAsync($"{Base}/amortization-report?fiscalYear=2026");
        resp.EnsureSuccessStatusCode();
        var api = await ReadApiAsync<AmortizationReportResponse>(resp);
        Assert.NotNull(api?.Data);
        var labels = api!.Data!.Groups.SelectMany(g => g.Rows).Select(r => r.Label).ToList();
        Assert.Contains(LabelB, labels);
        Assert.DoesNotContain(LabelA, labels);

        await AssertForbiddenAsync(HttpMethod.Get, $"{Base}/amortization-report?fiscalYear=2026");
    }

    // ── 5. GET {id} (D10) ──────────────────────────────────────────────────────────────
    [Fact]
    public async Task T09_05_GetById_D10Equivalent_AndForbidden()
    {
        var other = await AsTenantBAdmin().GetAsync($"{Base}/{TenantAAssetId}");
        var nonexistent = await AsTenantBAdmin().GetAsync($"{Base}/{Guid.NewGuid()}");
        await AssertD10EquivalentAsync(other, nonexistent, "Immobilisation introuvable.");

        await AssertForbiddenAsync(HttpMethod.Get, $"{Base}/{TenantAAssetId}");
    }

    // ── 6. GET {id}/schedule (D10) ─────────────────────────────────────────────────────
    [Fact]
    public async Task T09_06_GetSchedule_D10Equivalent_AndForbidden()
    {
        var other = await AsTenantBAdmin().GetAsync($"{Base}/{TenantAAssetId}/schedule");
        var nonexistent = await AsTenantBAdmin().GetAsync($"{Base}/{Guid.NewGuid()}/schedule");
        await AssertD10EquivalentAsync(other, nonexistent, "Immobilisation introuvable.");

        await AssertForbiddenAsync(HttpMethod.Get, $"{Base}/{TenantAAssetId}/schedule");
    }

    // ── 7. POST create (D10 payload DepreciationRateCategoryId d'un autre tenant) ──────
    [Fact]
    public async Task T09_07_Create_D10Equivalent_OnOtherTenantCategory_AndForbidden()
    {
        var other = await AsTenantBAdmin().PostAsJsonAsync(Base, CreatePayload(TenantACategoryId));
        var nonexistent = await AsTenantBAdmin().PostAsJsonAsync(Base, CreatePayload(Guid.NewGuid()));
        await AssertD10EquivalentAsync(other, nonexistent, "Catégorie d'amortissement introuvable.");

        await AssertForbiddenAsync(HttpMethod.Post, Base, new { });
    }

    // ── 8. PUT {id} (D10 : id route + payload DepreciationRateCategoryId) ──────────────
    [Fact]
    public async Task T09_08_Update_D10Equivalent_RouteAndPayload_AndForbidden()
    {
        var body = new UpdateFixedAssetRequest(
            Label: "T9-Upd", AcquisitionCost: 1000m, CapitalizedFees: 0m, ResidualValue: 0m,
            AcquisitionDate: new DateTime(2026, 1, 1), Description: null, Location: null,
            AssetAccountNumber: "228", DepreciationAccountNumber: "2828", ExpenseAccountNumber: "68112");

        // (a) id route = actif du tenant A (introuvable dans B) => idem id inexistant.
        var otherRoute = await AsTenantBAdmin().PutAsJsonAsync($"{Base}/{TenantAAssetId}", body);
        var nonRoute = await AsTenantBAdmin().PutAsJsonAsync($"{Base}/{Guid.NewGuid()}", body);
        await AssertD10EquivalentAsync(otherRoute, nonRoute, "Immobilisation introuvable.");

        // (b) id route = actif propre au tenant B (brouillon), payload catégorie = catégorie du
        // tenant A (introuvable dans B) => idem catégorie inexistante.
        var payloadA = body with
        {
            AcquisitionCost = 30_000m,
            AcquisitionDate = new DateTime(2026, 2, 1),
            DepreciationRateCategoryId = TenantACategoryId
        };
        var otherCat = await AsTenantBAdmin().PutAsJsonAsync($"{Base}/{TenantBAssetId}", payloadA);
        var nonCat = await AsTenantBAdmin().PutAsJsonAsync(
            $"{Base}/{TenantBAssetId}", payloadA with { DepreciationRateCategoryId = Guid.NewGuid() });
        await AssertD10EquivalentAsync(otherCat, nonCat, "Catégorie d'amortissement introuvable.");

        await AssertForbiddenAsync(HttpMethod.Put, $"{Base}/{TenantBAssetId}", new { });
    }

    // ── 9. POST {id}/schedule/preview (D10) ────────────────────────────────────────────
    [Fact]
    public async Task T09_09_PreviewSchedule_D10Equivalent_AndForbidden()
    {
        var body = new PreviewDepreciationScheduleRequest(new DateTime(2026, 3, 1));
        var other = await AsTenantBAdmin().PostAsJsonAsync($"{Base}/{TenantAAssetId}/schedule/preview", body);
        var nonexistent = await AsTenantBAdmin().PostAsJsonAsync($"{Base}/{Guid.NewGuid()}/schedule/preview", body);
        await AssertD10EquivalentAsync(other, nonexistent, "Immobilisation introuvable.");

        await AssertForbiddenAsync(HttpMethod.Post, $"{Base}/{TenantAAssetId}/schedule/preview", new { });
    }

    // ── 10. POST {id}/put-in-service (D10) ─────────────────────────────────────────────
    [Fact]
    public async Task T09_10_PutInService_D10Equivalent_AndForbidden()
    {
        var body = new PutFixedAssetInServiceRequest(new DateTime(2026, 3, 1), "404");
        var other = await AsTenantBAdmin().PostAsJsonAsync($"{Base}/{TenantAAssetId}/put-in-service", body);
        var nonexistent = await AsTenantBAdmin().PostAsJsonAsync($"{Base}/{Guid.NewGuid()}/put-in-service", body);
        await AssertD10EquivalentAsync(other, nonexistent, "Immobilisation introuvable.");

        await AssertForbiddenAsync(HttpMethod.Post, $"{Base}/{TenantAAssetId}/put-in-service", new { });
    }

    // ── 11. POST {id}/generate-schedule (D10) ──────────────────────────────────────────
    [Fact]
    public async Task T09_11_GenerateSchedule_D10Equivalent_AndForbidden()
    {
        var other = await AsTenantBAdmin().PostAsync($"{Base}/{TenantAAssetId}/generate-schedule", content: null);
        var nonexistent = await AsTenantBAdmin().PostAsync($"{Base}/{Guid.NewGuid()}/generate-schedule", content: null);
        await AssertD10EquivalentAsync(other, nonexistent, "Immobilisation introuvable.");

        await AssertForbiddenAsync(HttpMethod.Post, $"{Base}/{TenantAAssetId}/generate-schedule");
    }

    // ── 12. POST depreciation-runs (agrégat : ne comptabilise que le tenant courant) ───
    [Fact]
    public async Task T09_12_PostDepreciationRun_AggregateIsolation_AndForbidden()
    {
        // Le tenant A possède une ligne 2026 non comptabilisée (en service) ; le run du tenant B
        // (sans ligne) doit poster/skiper 0 => aucune fuite cross-tenant.
        var resp = await AsTenantBAdmin().PostAsJsonAsync($"{Base}/depreciation-runs", new PostDepreciationRunRequest(2026));
        resp.EnsureSuccessStatusCode();
        var api = await ReadApiAsync<DepreciationRunResultDto>(resp);
        Assert.NotNull(api?.Data);
        Assert.Equal(0, api!.Data!.PostedCount);
        Assert.Equal(0, api.Data.SkippedCount);
        Assert.Equal(0m, api.Data.TotalDepreciationAmount);

        await AssertForbiddenAsync(HttpMethod.Post, $"{Base}/depreciation-runs", new { });
    }

    // ── 13. POST {id}/dispose (D10) ────────────────────────────────────────────────────
    [Fact]
    public async Task T09_13_Dispose_D10Equivalent_AndForbidden()
    {
        var body = new DisposeFixedAssetRequest(new DateTime(2026, 6, 1), 1000m, "53");
        var other = await AsTenantBAdmin().PostAsJsonAsync($"{Base}/{TenantAAssetId}/dispose", body);
        var nonexistent = await AsTenantBAdmin().PostAsJsonAsync($"{Base}/{Guid.NewGuid()}/dispose", body);
        await AssertD10EquivalentAsync(other, nonexistent, "Immobilisation introuvable.");

        await AssertForbiddenAsync(HttpMethod.Post, $"{Base}/{TenantAAssetId}/dispose", new { });
    }

    // ── 14. GET {id}/schedule/export.xlsx (D10, export) ────────────────────────────────
    [Fact]
    public async Task T09_14_ExportSchedule_D10Equivalent_AndForbidden()
    {
        var other = await AsTenantBAdmin().GetAsync($"{Base}/{TenantAAssetId}/schedule/export.xlsx");
        var nonexistent = await AsTenantBAdmin().GetAsync($"{Base}/{Guid.NewGuid()}/schedule/export.xlsx");
        await AssertD10EquivalentAsync(other, nonexistent, "Immobilisation introuvable.");

        await AssertForbiddenAsync(HttpMethod.Get, $"{Base}/{TenantAAssetId}/schedule/export.xlsx");
    }

    // ── 15. GET depreciation-report/export.xlsx (agrégat, export) ──────────────────────
    [Fact]
    public async Task T09_15_ExportDepreciationReport_AggregateIsolation_AndForbidden()
    {
        var tenantB = await AsTenantBAdmin().GetAsync($"{Base}/depreciation-report/export.xlsx?fiscalYear=2026");
        tenantB.EnsureSuccessStatusCode();
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            tenantB.Content.Headers.ContentType?.MediaType);
        var tenantBBytes = await tenantB.Content.ReadAsByteArrayAsync();
        Assert.False(ContainsTextInXlsx(tenantBBytes, LabelA),
            "L'export de dotations du tenant B ne doit pas contenir le libellé d'un actif du tenant A.");

        // Contrôle positif : l'export du tenant A contient bien son marqueur (la détection fonctionne).
        var tenantA = await AsTenantAAdmin().GetAsync($"{Base}/depreciation-report/export.xlsx?fiscalYear=2026");
        tenantA.EnsureSuccessStatusCode();
        var tenantABytes = await tenantA.Content.ReadAsByteArrayAsync();
        Assert.True(ContainsTextInXlsx(tenantABytes, LabelA),
            "L'export de dotations du tenant A doit contenir son propre libellé (contrôle positif).");

        await AssertForbiddenAsync(HttpMethod.Get, $"{Base}/depreciation-report/export.xlsx?fiscalYear=2026");
    }

    // ── 16. GET amortization-report/export.xlsx (agrégat, export) ─────────────────────
    [Fact]
    public async Task T09_16_ExportAmortizationReport_AggregateIsolation_AndForbidden()
    {
        var tenantB = await AsTenantBAdmin().GetAsync($"{Base}/amortization-report/export.xlsx?fiscalYear=2026");
        tenantB.EnsureSuccessStatusCode();
        var tenantBBytes = await tenantB.Content.ReadAsByteArrayAsync();
        Assert.True(ContainsTextInXlsx(tenantBBytes, LabelB),
            "L'export du tableau d'amortissements du tenant B doit contenir son propre libellé.");
        Assert.False(ContainsTextInXlsx(tenantBBytes, LabelA),
            "L'export du tenant B ne doit pas contenir le libellé d'un actif du tenant A.");

        var tenantA = await AsTenantAAdmin().GetAsync($"{Base}/amortization-report/export.xlsx?fiscalYear=2026");
        tenantA.EnsureSuccessStatusCode();
        var tenantABytes = await tenantA.Content.ReadAsByteArrayAsync();
        Assert.True(ContainsTextInXlsx(tenantABytes, LabelA),
            "L'export du tenant A doit contenir son propre libellé (contrôle positif).");

        await AssertForbiddenAsync(HttpMethod.Get, $"{Base}/amortization-report/export.xlsx?fiscalYear=2026");
    }
}
