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
            DataTemplates = _templates,
            TaxRegimeSuggestions = Array.Empty<TaxRegimeSuggestionSnapshot>()
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

        public TenantDbContext CreateContext() => new TestTenantDbContext(_options);
    }

    /// <summary>
    /// Test-only <see cref="TenantDbContext"/> that seeds <see cref="DocumentNumberingScheme.RowVersion"/>
    /// on Added rows before saving. <c>RowVersion</c> is a SQL Server <c>rowversion</c> column
    /// (<c>.IsRowVersion()</c>) auto-populated by the real provider on insert; the EF InMemory provider
    /// has no such generator and rejects null required values. This mirrors the reflection helper used in
    /// <c>ProductOnboardingServiceAutoCompletionTests.AddNumberingScheme</c> but at the SaveChanges seam so
    /// the applier's internal Add+Save (which has no injection point) still succeeds.
    /// </summary>
    private sealed class TestTenantDbContext : TenantDbContext
    {
        private static readonly System.Reflection.PropertyInfo RowVersionProperty =
            typeof(DocumentNumberingScheme).GetProperty(nameof(DocumentNumberingScheme.RowVersion))!;
        private static readonly byte[] SeedRowVersion = new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 };

        public TestTenantDbContext(DbContextOptions<TenantDbContext> options) : base(options) { }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<DocumentNumberingScheme>())
            {
                if (entry.State == EntityState.Added && entry.Entity.RowVersion is null)
                    RowVersionProperty.SetValue(entry.Entity, SeedRowVersion);
            }

            return await base.SaveChangesAsync(cancellationToken);
        }

        public override int SaveChanges()
        {
            foreach (var entry in ChangeTracker.Entries<DocumentNumberingScheme>())
            {
                if (entry.State == EntityState.Added && entry.Entity.RowVersion is null)
                    RowVersionProperty.SetValue(entry.Entity, SeedRowVersion);
            }

            return base.SaveChanges();
        }
    }

    // « 9999 » ne serait plus créable : un numéro SCE commence par sa classe, 1 à 7.
        private static DataTemplateSnapshot ChartAccountTemplate(string code, int version, string accountNumber = "4999") => new()
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
        Assert.True(await context.ChartOfAccounts.AnyAsync(a => a.AccountNumber == "4999"));
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
            var account = ChartOfAccount.Create("4999", "Libellé personnalisé", 4, null, AccountNatureType.Debit).Value;
            seedContext.ChartOfAccounts.Add(account);
            await seedContext.SaveChangesAsync();
        }

        var provider = new FakeSectorCatalogProvider(new[] { ChartAccountTemplate("tpl-d", 1) });
        var applier = NewApplier(provider, factory);
        var result = await applier.ApplyAsync(Guid.NewGuid(), "unused", null, null, dryRun: false, CancellationToken.None);

        Assert.Contains(result.ItemOutcomes, o => o.Outcome == "existing");

        using var context = factory.CreateContext();
        var reloadedAccount = await context.ChartOfAccounts.SingleAsync(a => a.AccountNumber == "4999");
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

    // ---------- Phase 3 §3.4 — enriched sector data templates ----------

    private static DataTemplateSnapshot ProductCategoryTemplate(string code, string segmentCode, string domainCode,
        string catCode, string catName, int displayOrder) => new()
    {
        Code = code,
        SegmentCode = segmentCode,
        DomainCode = domainCode,
        LabelFr = "Modèle test",
        DescriptionFr = null,
        Version = 1,
        SortOrder = 0,
        Items = new[]
        {
            new DataTemplateItemSnapshot
            {
                ItemKind = "product-category",
                PayloadJson = $$"""{"code":"{{catCode}}","name":"{{catName}}","displayOrder":{{displayOrder}}}""",
                SortOrder = 0
            }
        }
    };

    private static DataTemplateSnapshot WarehouseTemplate(string code, string segmentCode,
        string whCode, string whName) => new()
    {
        Code = code,
        SegmentCode = segmentCode,
        DomainCode = null,
        LabelFr = "Modèle test",
        DescriptionFr = null,
        Version = 1,
        SortOrder = 0,
        Items = new[]
        {
            new DataTemplateItemSnapshot
            {
                ItemKind = "warehouse",
                PayloadJson = $$"""{"code":"{{whCode}}","name":"{{whName}}","address":null,"isDefault":false}""",
                SortOrder = 0
            }
        }
    };

    private static DataTemplateSnapshot NumberingPrefixTemplate(string code, string segmentCode,
        string documentType, string prefix) => new()
    {
        Code = code,
        SegmentCode = segmentCode,
        DomainCode = null,
        LabelFr = "Modèle test",
        DescriptionFr = null,
        Version = 1,
        SortOrder = 0,
        Items = new[]
        {
            new DataTemplateItemSnapshot
            {
                ItemKind = "document-numbering-scheme",
                PayloadJson = $$"""{"documentType":"{{documentType}}","prefix":"{{prefix}}"}""",
                SortOrder = 0
            }
        }
    };

    [Fact]
    public async Task Apply_creates_product_category_and_is_idempotent()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var provider = new FakeSectorCatalogProvider(new[]
        {
            ProductCategoryTemplate("tpl-cat", null, null, "VETEMENTS", "Vêtements", 1)
        });
        var applier = NewApplier(provider, factory);
        var tenantId = Guid.NewGuid();

        var first = await applier.ApplyAsync(tenantId, "unused", null, null, dryRun: false, CancellationToken.None);
        Assert.Contains(first.ItemOutcomes, o => o.Outcome == "created");

        using (var context = factory.CreateContext())
        {
            var category = await context.ProductCategories.SingleAsync(c => c.Code == "VETEMENTS");
            Assert.Equal("Vêtements", category.Name);
            Assert.Equal(1, category.DisplayOrder);
            Assert.True(category.IsActive);
        }

        // Second run: template already applied → skipped, no duplicate category.
        var second = await applier.ApplyAsync(tenantId, "unused", null, null, dryRun: false, CancellationToken.None);
        Assert.Empty(second.Applied);

        using var ctx2 = factory.CreateContext();
        Assert.Single(await ctx2.ProductCategories.Where(c => c.Code == "VETEMENTS").ToListAsync());
    }

    [Fact]
    public async Task Apply_product_category_does_not_overwrite_existing_row()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        await using (var seedContext = factory.CreateContext())
        {
            var existing = ProductCategory.Create("ACCESSOIRES", "Libellé admin", 9).Value;
            seedContext.ProductCategories.Add(existing);
            await seedContext.SaveChangesAsync();
        }

        var provider = new FakeSectorCatalogProvider(new[]
        {
            ProductCategoryTemplate("tpl-cat2", null, null, "ACCESSOIRES", "Accessoires", 2)
        });
        var applier = NewApplier(provider, factory);
        var result = await applier.ApplyAsync(Guid.NewGuid(), "unused", null, null, dryRun: false, CancellationToken.None);

        Assert.Contains(result.ItemOutcomes, o => o.Outcome == "existing");

        using var context = factory.CreateContext();
        var reloaded = await context.ProductCategories.SingleAsync(c => c.Code == "ACCESSOIRES");
        Assert.Equal("Libellé admin", reloaded.Name);
        Assert.Equal(9, reloaded.DisplayOrder);
    }

    [Fact]
    public async Task Apply_creates_extra_warehouse_and_is_idempotent()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var provider = new FakeSectorCatalogProvider(new[]
        {
            WarehouseTemplate("tpl-wh", CompanySegments.Commerce, "BOUTIQUE", "Boutique")
        });
        var applier = NewApplier(provider, factory);
        var tenantId = Guid.NewGuid();

        var first = await applier.ApplyAsync(tenantId, "unused", CompanySegments.Commerce, null, dryRun: false, CancellationToken.None);
        Assert.Contains(first.ItemOutcomes, o => o.Outcome == "created");

        using (var context = factory.CreateContext())
        {
            var warehouse = await context.Warehouses.SingleAsync(w => w.Code == "BOUTIQUE");
            Assert.Equal("Boutique", warehouse.Name);
            Assert.False(warehouse.IsDefault);
        }

        var second = await applier.ApplyAsync(tenantId, "unused", CompanySegments.Commerce, null, dryRun: false, CancellationToken.None);
        Assert.Empty(second.Applied);

        using var ctx2 = factory.CreateContext();
        Assert.Single(await ctx2.Warehouses.Where(w => w.Code == "BOUTIQUE").ToListAsync());
    }

    [Fact]
    public async Task Apply_segment_scoped_template_only_matches_its_segment()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var provider = new FakeSectorCatalogProvider(new[]
        {
            WarehouseTemplate("tpl-wh-seg", CompanySegments.Commerce, "RESERVE", "Réserve")
        });
        var applier = NewApplier(provider, factory);

        // Wrong segment → no matching template → nothing applied.
        var miss = await applier.ApplyAsync(Guid.NewGuid(), "unused", CompanySegments.Services, null, dryRun: false, CancellationToken.None);
        Assert.Empty(miss.Applied);

        using (var context = factory.CreateContext())
        {
            Assert.False(await context.Warehouses.AnyAsync(w => w.Code == "RESERVE"));
        }

        // Right segment → applied.
        var hit = await applier.ApplyAsync(Guid.NewGuid(), "unused", CompanySegments.Commerce, null, dryRun: false, CancellationToken.None);
        Assert.Single(hit.Applied);

        using var ctx2 = factory.CreateContext();
        Assert.True(await ctx2.Warehouses.AnyAsync(w => w.Code == "RESERVE"));
    }

    [Fact]
    public async Task Apply_document_numbering_scheme_applies_custom_prefix_when_new()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var provider = new FakeSectorCatalogProvider(new[]
        {
            NumberingPrefixTemplate("tpl-num", CompanySegments.Commerce, "Invoice", "FAC-COM")
        });
        var applier = NewApplier(provider, factory);
        var tenantId = Guid.NewGuid();

        var first = await applier.ApplyAsync(tenantId, "unused", CompanySegments.Commerce, null, dryRun: false, CancellationToken.None);
        Assert.Contains(first.ItemOutcomes, o => o.Outcome == "created");

        using var context = factory.CreateContext();
        var scheme = await context.DocumentNumberingSchemes
            .SingleAsync(s => s.TenantId == tenantId && s.DocumentType == NumberingDocumentType.Invoice);
        var blocks = scheme.GetBlocks();
        Assert.Equal("FAC-COM", blocks[0].Value);
        Assert.False(scheme.IsFormatLocked);
        Assert.Empty(first.Warnings);
    }

    [Fact]
    public async Task Apply_document_numbering_scheme_prefix_is_idempotent_existing_scheme_untouched()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var provider = new FakeSectorCatalogProvider(new[]
        {
            NumberingPrefixTemplate("tpl-num2", CompanySegments.Commerce, "Invoice", "FAC-COM")
        });
        var applier = NewApplier(provider, factory);
        var tenantId = Guid.NewGuid();

        // Pre-create the scheme with the default prefix (simulates lazy creation at first issuance).
        var fiscalYear = DateTime.UtcNow.Year;
        await using (var seedContext = factory.CreateContext())
        {
            seedContext.DocumentNumberingSchemes.Add(
                DocumentNumberingScheme.CreateDefault(tenantId, NumberingDocumentType.Invoice, fiscalYear, 0));
            await seedContext.SaveChangesAsync();
        }

        var result = await applier.ApplyAsync(tenantId, "unused", CompanySegments.Commerce, null, dryRun: false, CancellationToken.None);

        // Existing scheme → "existing", default prefix preserved (never overwritten).
        Assert.Contains(result.ItemOutcomes, o => o.Outcome == "existing");
        using var context = factory.CreateContext();
        var scheme = await context.DocumentNumberingSchemes
            .SingleAsync(s => s.TenantId == tenantId && s.DocumentType == NumberingDocumentType.Invoice);
        Assert.Equal(NumberingDocumentType.Invoice.DefaultFreeText(), scheme.GetBlocks()[0].Value);
    }

    [Fact]
    public void Catalog_declares_phase3_enriched_templates_with_correct_scopes()
    {
        var byCode = SectorConfigurationCatalog.DataTemplates.ToDictionary(t => t.Code, StringComparer.Ordinal);

        // Product families per domain.
        Assert.True(byCode.ContainsKey("product-categories-textile-habillement"));
        Assert.True(byCode.ContainsKey("product-categories-alimentation-agroalimentaire"));
        var textile = byCode["product-categories-textile-habillement"];
        Assert.Equal(BusinessDomains.TextileHabillement, textile.DomainCode);
        Assert.Null(textile.SegmentCode);
        Assert.Equal(2, textile.Items.Count);
        Assert.All(textile.Items, i => Assert.Equal("product-category", i.ItemKind));

        // Extra warehouses for commerce.
        var warehouses = byCode["extra-warehouses-commerce"];
        Assert.Equal(CompanySegments.Commerce, warehouses.SegmentCode);
        Assert.All(warehouses.Items, i => Assert.Equal("warehouse", i.ItemKind));

        // BTP chart-of-accounts (travaux en cours).
        var btp = byCode["chart-account-btp-travaux-en-cours"];
        Assert.Equal(CompanySegments.BtpConstruction, btp.SegmentCode);
        Assert.All(btp.Items, i => Assert.Equal("chart-account", i.ItemKind));

        // Numbering prefixes for commerce.
        var numbering = byCode["document-numbering-prefixes-commerce"];
        Assert.Equal(CompanySegments.Commerce, numbering.SegmentCode);
        Assert.All(numbering.Items, i => Assert.Equal("document-numbering-scheme", i.ItemKind));
    }

    [Fact]
    public async Task Apply_full_commerce_segment_runs_all_commerce_templates()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());

        // Reuse the real catalog templates scoped to the commerce segment + a commerce domain.
        var templates = SectorConfigurationCatalog.DataTemplates
            .Where(t => t.SegmentCode == CompanySegments.Commerce)
            .Select(t => new DataTemplateSnapshot
            {
                Code = t.Code,
                SegmentCode = t.SegmentCode,
                DomainCode = t.DomainCode,
                LabelFr = t.LabelFr,
                DescriptionFr = t.DescriptionFr,
                Version = t.Version,
                SortOrder = t.SortOrder,
                Items = t.Items.Select(i => new DataTemplateItemSnapshot
                {
                    ItemKind = i.ItemKind,
                    PayloadJson = i.PayloadJson,
                    SortOrder = i.SortOrder
                }).ToArray()
            })
            .ToArray();

        var provider = new FakeSectorCatalogProvider(templates);
        var applier = NewApplier(provider, factory);
        var tenantId = Guid.NewGuid();

        var result = await applier.ApplyAsync(tenantId, "unused", CompanySegments.Commerce, null, dryRun: false, CancellationToken.None);

        Assert.NotEmpty(result.Applied);
        Assert.Empty(result.Warnings);

        using var context = factory.CreateContext();
        Assert.True(await context.Warehouses.AnyAsync(w => w.Code == "BOUTIQUE"));
        Assert.True(await context.Warehouses.AnyAsync(w => w.Code == "RESERVE"));
        Assert.True(await context.DocumentNumberingSchemes.AnyAsync(s =>
            s.TenantId == tenantId && s.DocumentType == NumberingDocumentType.Invoice));
    }
}
