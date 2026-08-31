using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.SectorRules;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.SectorRules;

/// <summary>Phase 2 — backoffice admin CRUD over the sector-rule tables (plan §WP-B5).</summary>
public sealed class SectorRuleAdminServiceTests
{
    private static MasterDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static SectorRuleAdminService NewService(MasterDbContext db) => new(db);

    [Fact]
    public async Task Create_segment_with_invalid_code_fails_French()
    {
        await using var db = NewDb();
        var service = NewService(db);

        var result = await service.CreateSegmentAsync(
            new CreateSectorSegmentRequest { Code = "Commerce Invalide!", LabelFr = "x", DescriptionFr = "x", IconKey = "x" },
            actor: "admin", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Code invalide : minuscules, chiffres et tirets uniquement (50 caractères max).", result.Error.Description);
    }

    [Fact]
    public async Task Create_module_rule_with_core_module_fails()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var segment = (await service.CreateSegmentAsync(
            new CreateSectorSegmentRequest { Code = "seg-a", LabelFr = "A", DescriptionFr = "A", IconKey = "icon" },
            actor: "admin", CancellationToken.None)).Value;

        var result = await service.CreateModuleRuleAsync(
            new CreateSectorModuleRuleRequest { RuleKind = "SegmentBase", SegmentId = segment.Id, ModuleId = (int)AppModule.Clients },
            actor: "admin", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Les modules de base sont toujours actifs et ne peuvent pas figurer dans les règles.", result.Error.Description);
    }

    [Fact]
    public async Task Create_module_rule_with_honoraires_fails()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var segment = (await service.CreateSegmentAsync(
            new CreateSectorSegmentRequest { Code = "seg-a", LabelFr = "A", DescriptionFr = "A", IconKey = "icon" },
            actor: "admin", CancellationToken.None)).Value;

        var result = await service.CreateModuleRuleAsync(
            new CreateSectorModuleRuleRequest { RuleKind = "SegmentBase", SegmentId = segment.Id, ModuleId = (int)AppModule.Honoraires },
            actor: "admin", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Module invalide.", result.Error.Description);
    }

    [Fact]
    public async Task Create_dependency_cycle_fails()
    {
        await using var db = NewDb();
        var service = NewService(db);

        var ab = await service.CreateModuleDependencyAsync(
            new CreateSectorModuleDependencyRequest { ModuleId = (int)AppModule.Stock, RequiredModuleId = (int)AppModule.Purchases },
            actor: "admin", CancellationToken.None);
        Assert.True(ab.IsSuccess);

        var bc = await service.CreateModuleDependencyAsync(
            new CreateSectorModuleDependencyRequest { ModuleId = (int)AppModule.Purchases, RequiredModuleId = (int)AppModule.CRM },
            actor: "admin", CancellationToken.None);
        Assert.True(bc.IsSuccess);

        var ca = await service.CreateModuleDependencyAsync(
            new CreateSectorModuleDependencyRequest { ModuleId = (int)AppModule.CRM, RequiredModuleId = (int)AppModule.Stock },
            actor: "admin", CancellationToken.None);

        Assert.True(ca.IsFailure);
        Assert.Equal("Dépendance circulaire détectée entre modules.", ca.Error.Description);
    }

    [Fact]
    public async Task Create_dependency_self_reference_fails()
    {
        await using var db = NewDb();
        var service = NewService(db);

        var result = await service.CreateModuleDependencyAsync(
            new CreateSectorModuleDependencyRequest { ModuleId = (int)AppModule.Stock, RequiredModuleId = (int)AppModule.Stock },
            actor: "admin", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Un module ne peut pas dépendre de lui-même.", result.Error.Description);
    }

    [Fact]
    public async Task Every_write_bumps_rule_set_version()
    {
        await using var db = NewDb();
        var service = NewService(db);

        var versionBefore = await service.GetVersionAsync(CancellationToken.None);

        var created = await service.CreateSegmentAsync(
            new CreateSectorSegmentRequest { Code = "seg-a", LabelFr = "A", DescriptionFr = "A", IconKey = "icon" },
            actor: "admin", CancellationToken.None);
        Assert.True(created.IsSuccess);
        var versionAfterCreate = await service.GetVersionAsync(CancellationToken.None);
        Assert.Equal(versionBefore + 1, versionAfterCreate);

        var updated = await service.UpdateSegmentAsync(created.Value.Id,
            new UpdateSectorSegmentRequest { LabelFr = "A2", DescriptionFr = "A2", IconKey = "icon2" },
            actor: "admin", CancellationToken.None);
        Assert.True(updated.IsSuccess);
        var versionAfterUpdate = await service.GetVersionAsync(CancellationToken.None);
        Assert.Equal(versionAfterCreate + 1, versionAfterUpdate);

        var deactivated = await service.DeactivateSegmentAsync(created.Value.Id, actor: "admin", CancellationToken.None);
        Assert.True(deactivated.IsSuccess);
        var versionAfterDeactivate = await service.GetVersionAsync(CancellationToken.None);
        Assert.Equal(versionAfterUpdate + 1, versionAfterDeactivate);
    }

    [Fact]
    public async Task Soft_delete_sets_IsActive_false_and_row_survives()
    {
        await using var db = NewDb();
        var service = NewService(db);

        var created = await service.CreateSegmentAsync(
            new CreateSectorSegmentRequest { Code = "seg-a", LabelFr = "A", DescriptionFr = "A", IconKey = "icon" },
            actor: "admin", CancellationToken.None);

        var deactivated = await service.DeactivateSegmentAsync(created.Value.Id, actor: "admin", CancellationToken.None);
        Assert.True(deactivated.IsSuccess);

        var fetched = await service.GetSegmentAsync(created.Value.Id, CancellationToken.None);
        Assert.True(fetched.IsSuccess);
        Assert.False(fetched.Value.IsActive);
        Assert.Equal(1, await db.SectorSegments.CountAsync());
    }

    [Fact]
    public async Task Template_item_with_unknown_kind_fails()
    {
        await using var db = NewDb();
        var service = NewService(db);

        var result = await service.CreateTemplateAsync(
            new CreateSectorDataTemplateRequest
            {
                Code = "modele-a",
                LabelFr = "Modèle A",
                Items = new[]
                {
                    new SectorDataTemplateItemRequest { ItemKind = "unknown-kind", PayloadJson = "{}" }
                }
            },
            actor: "admin", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Élément de modèle invalide : type ou contenu non reconnu.", result.Error.Description);
    }

    [Fact]
    public async Task Template_item_with_valid_kind_and_json_succeeds_and_items_are_replaced_on_update()
    {
        await using var db = NewDb();
        var service = NewService(db);

        var created = await service.CreateTemplateAsync(
            new CreateSectorDataTemplateRequest
            {
                Code = "modele-a",
                LabelFr = "Modèle A",
                Items = new[]
                {
                    new SectorDataTemplateItemRequest { ItemKind = "setting", PayloadJson = "{\"key\":\"a\"}" }
                }
            },
            actor: "admin", CancellationToken.None);
        Assert.True(created.IsSuccess);
        Assert.Single(created.Value.Items);

        var updated = await service.UpdateTemplateAsync(created.Value.Id,
            new UpdateSectorDataTemplateRequest
            {
                LabelFr = "Modèle A v2",
                Items = new[]
                {
                    new SectorDataTemplateItemRequest { ItemKind = "chart-account", PayloadJson = "{\"code\":\"512\"}" },
                    new SectorDataTemplateItemRequest { ItemKind = "document-numbering-scheme", PayloadJson = "{\"prefix\":\"FA\"}" }
                }
            },
            actor: "admin", CancellationToken.None);

        Assert.True(updated.IsSuccess);
        Assert.Equal(2, updated.Value.Items.Count);

        var refetched = await service.GetTemplateAsync(created.Value.Id, CancellationToken.None);
        Assert.True(refetched.IsSuccess);
        Assert.Equal(2, refetched.Value.Items.Count(i => i.IsActive));
    }

    [Fact]
    public async Task Segment_domain_with_unknown_segment_or_domain_fails()
    {
        await using var db = NewDb();
        var service = NewService(db);

        var result = await service.CreateSegmentDomainAsync(
            new CreateSectorSegmentDomainRequest { SegmentId = Guid.NewGuid(), DomainId = Guid.NewGuid() },
            actor: "admin", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Segment ou domaine introuvable.", result.Error.Description);
    }

    [Fact]
    public async Task Duplicate_code_fails_French()
    {
        await using var db = NewDb();
        var service = NewService(db);

        var first = await service.CreateDomainAsync(
            new CreateSectorDomainRequest { Code = "domaine-a", LabelFr = "A" }, actor: "admin", CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await service.CreateDomainAsync(
            new CreateSectorDomainRequest { Code = "domaine-a", LabelFr = "A bis" }, actor: "admin", CancellationToken.None);

        Assert.True(second.IsFailure);
        Assert.Equal("Un enregistrement avec ce code existe déjà.", second.Error.Description);
    }

    [Fact]
    public async Task Setting_with_invalid_int_value_fails()
    {
        await using var db = NewDb();
        var service = NewService(db);

        var result = await service.CreateSettingAsync(
            new CreateSectorDefaultSettingRequest { SettingKey = "max-users", SettingValue = "not-a-number", ValueType = "int" },
            actor: "admin", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Valeur de paramètre invalide pour le type déclaré.", result.Error.Description);
    }

    [Fact]
    public async Task SeedFromCatalog_delegates_to_seeder()
    {
        await using var db = NewDb();
        var service = NewService(db);

        var result = await service.SeedFromCatalogAsync(force: false, actor: "admin", CancellationToken.None);

        Assert.Equal(6, await db.SectorSegments.CountAsync());
        Assert.Equal(10, await db.SectorDomains.CountAsync());
        Assert.Equal(1, result.NewVersion);
    }
}
