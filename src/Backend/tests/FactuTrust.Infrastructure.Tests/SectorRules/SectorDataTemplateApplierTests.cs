using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.SectorCatalog;
using FactuTrust.Infrastructure.Services.SectorRules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.SectorRules;

/// <summary>Phase 2 — application additive des modèles de données sectoriels (plan §WP-B6).</summary>
public sealed class SectorDataTemplateApplierTests
{
    private sealed class FakeSectorCatalogProvider : ISectorCatalogProvider
    {
        private readonly IReadOnlyList<DataTemplateSnapshot> _templates;

        public FakeSectorCatalogProvider(IReadOnlyList<DataTemplateSnapshot> templates) => _templates = templates;

        public SectorRuleSnapshot GetSnapshot() => new()
        {
            Source = SectorRuleSource.Db,
            Version = 1,
            Segments = Array.Empty<SegmentSnapshot>(),
            Domains = Array.Empty<DomainSnapshot>(),
            ModuleDependencies = Array.Empty<ModuleDependencySnapshot>(),
            DefaultSettings = Array.Empty<DefaultSettingSnapshot>(),
            DataTemplates = _templates
        };
    }

    private sealed class InMemoryTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly DbContextOptions<TenantDbContext> _options;

        public InMemoryTenantDbContextFactory(string databaseName)
        {
            _options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(databaseName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        public TenantDbContext CreateContext() => new(_options);
    }

    private static DataTemplateSnapshot ChartAccountTemplate(string code, int version, string accountNumber = "9999") => new()
    {
        Code = code,
        SegmentCode = null,
        DomainCode = null,
        LabelFr = "Modèle test",
        DescriptionFr = null,
        Version = version,
        SortOrder = 0,
        Items = new[]
        {
            new DataTemplateItemSnapshot
            {
                ItemKind = "chart-account",
                PayloadJson = $$"""{"accountNumber":"{{accountNumber}}","label":"Compte test","accountClass":4,"natureType":"Debit"}""",
                SortOrder = 0
            }
        }
    };

    private static SectorDataTemplateApplier NewApplier(FakeSectorCatalogProvider provider, InMemoryTenantDbContextFactory factory) =>
        new(provider, factory, NullLogger<SectorDataTemplateApplier>.Instance);

    [Fact]
    public async Task Apply_creates_missing_chart_account_and_records_applied_template()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var provider = new FakeSectorCatalogProvider(new[] { ChartAccountTemplate("tpl-a", 1) });
        var applier = NewApplier(provider, factory);

        var result = await applier.ApplyAsync(Guid.NewGuid(), "unused", null, null, dryRun: false, CancellationToken.None);

        Assert.Equal(new[] { new FactuTrust.Application.Common.AppliedTemplateInfo("tpl-a", 1) }, result.Applied);
        Assert.Empty(result.Skipped);
        Assert.Contains(result.ItemOutcomes, o => o.Outcome == "created");

        using var context = factory.CreateContext();
        Assert.True(await context.ChartOfAccounts.AnyAsync(a => a.AccountNumber == "9999"));
        Assert.True(await context.AppliedSectorTemplates.AnyAsync(t => t.TemplateCode == "tpl-a" && t.Version == 1));
    }

    [Fact]
    public async Task Apply_skips_template_already_applied_same_version()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var provider = new FakeSectorCatalogProvider(new[] { ChartAccountTemplate("tpl-b", 1) });
        var applier = NewApplier(provider, factory);

        await applier.ApplyAsync(Guid.NewGuid(), "unused", null, null, dryRun: false, CancellationToken.None);
        var second = await applier.ApplyAsync(Guid.NewGuid(), "unused", null, null, dryRun: false, CancellationToken.None);

        Assert.Empty(second.Applied);
        Assert.Equal(new[] { new FactuTrust.Application.Common.AppliedTemplateInfo("tpl-b", 1) }, second.Skipped);

        using var context = factory.CreateContext();
        Assert.Equal(1, await context.AppliedSectorTemplates.CountAsync());
    }

    [Fact]
    public async Task Apply_reapplies_when_version_increases_without_touching_existing_rows()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var v1Provider = new FakeSectorCatalogProvider(new[] { ChartAccountTemplate("tpl-c", 1, "1111") });
        var applier1 = NewApplier(v1Provider, factory);
        await applier1.ApplyAsync(Guid.NewGuid(), "unused", null, null, dryRun: false, CancellationToken.None);

        var v2Provider = new FakeSectorCatalogProvider(new[] { ChartAccountTemplate("tpl-c", 2, "2222") });
        var applier2 = NewApplier(v2Provider, factory);
        var result = await applier2.ApplyAsync(Guid.NewGuid(), "unused", null, null, dryRun: false, CancellationToken.None);

        Assert.Equal(new[] { new FactuTrust.Application.Common.AppliedTemplateInfo("tpl-c", 2) }, result.Applied);

        using var context = factory.CreateContext();
        Assert.True(await context.ChartOfAccounts.AnyAsync(a => a.AccountNumber == "1111"));
        Assert.True(await context.ChartOfAccounts.AnyAsync(a => a.AccountNumber == "2222"));
        Assert.Equal(2, await context.AppliedSectorTemplates.CountAsync());
    }

    [Fact]
    public async Task Apply_never_updates_existing_tenant_row()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        await using (var seedContext = factory.CreateContext())
        {
            var account = ChartOfAccount.Create("9999", "Libellé personnalisé", 4, null, AccountNatureType.Debit).Value;
            seedContext.ChartOfAccounts.Add(account);
            await seedContext.SaveChangesAsync();
        }

        var provider = new FakeSectorCatalogProvider(new[] { ChartAccountTemplate("tpl-d", 1) });
        var applier = NewApplier(provider, factory);
        var result = await applier.ApplyAsync(Guid.NewGuid(), "unused", null, null, dryRun: false, CancellationToken.None);

        Assert.Contains(result.ItemOutcomes, o => o.Outcome == "existing");

        using var context = factory.CreateContext();
        var reloadedAccount = await context.ChartOfAccounts.SingleAsync(a => a.AccountNumber == "9999");
        Assert.Equal("Libellé personnalisé", reloadedAccount.Label);
    }

    [Fact]
    public async Task Apply_with_static_provider_is_noop()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var staticProvider = new StaticSectorCatalogProvider();
        var applier = new SectorDataTemplateApplier(staticProvider, factory, NullLogger<SectorDataTemplateApplier>.Instance);

        var result = await applier.ApplyAsync(Guid.NewGuid(), "unused", "commerce", "retail", dryRun: false, CancellationToken.None);

        Assert.Empty(result.Applied);
        Assert.Empty(result.Skipped);
        Assert.Empty(result.ItemOutcomes);

        using var context = factory.CreateContext();
        Assert.False(await context.ChartOfAccounts.AnyAsync());
    }

    [Fact]
    public async Task Apply_unknown_item_kind_produces_warning_not_failure()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var template = new DataTemplateSnapshot
        {
            Code = "tpl-e",
            SegmentCode = null,
            DomainCode = null,
            LabelFr = "Modèle test",
            DescriptionFr = null,
            Version = 1,
            SortOrder = 0,
            Items = new[]
            {
                new DataTemplateItemSnapshot { ItemKind = "unknown-kind", PayloadJson = "{}", SortOrder = 0 }
            }
        };
        var provider = new FakeSectorCatalogProvider(new[] { template });
        var applier = NewApplier(provider, factory);

        var result = await applier.ApplyAsync(Guid.NewGuid(), "unused", null, null, dryRun: false, CancellationToken.None);

        Assert.NotEmpty(result.Warnings);
        Assert.Contains(result.ItemOutcomes, o => o.Outcome == "skipped");
        Assert.Single(result.Applied); // Le modèle est marqué appliqué même si son seul item est ignoré (forward compat).
    }

    [Fact]
    public async Task DryRun_reports_outcomes_and_writes_nothing()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var provider = new FakeSectorCatalogProvider(new[] { ChartAccountTemplate("tpl-f", 1) });
        var applier = NewApplier(provider, factory);

        var result = await applier.ApplyAsync(Guid.NewGuid(), "unused", null, null, dryRun: true, CancellationToken.None);

        Assert.Single(result.Applied);
        Assert.Contains(result.ItemOutcomes, o => o.Outcome == "created");

        using var context = factory.CreateContext();
        Assert.False(await context.ChartOfAccounts.AnyAsync());
        Assert.False(await context.AppliedSectorTemplates.AnyAsync());
    }
}
