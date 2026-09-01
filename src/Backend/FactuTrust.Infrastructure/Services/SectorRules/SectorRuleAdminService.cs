using System.Text.Json;
using System.Text.RegularExpressions;
using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.SectorRules;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Persistence.Seeds;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.SectorRules;

/// <summary>
/// Phase 2 — backoffice CRUD over the master-DB sector-rule tables (plan §WP-B5). Every
/// successful write bumps <see cref="SectorRuleSetStamp.Version"/> inside the same
/// <c>SaveChangesAsync</c> call as the row change, so <c>DbSectorCatalogProvider</c>'s cache
/// invalidates immediately. Deletes are always soft (<c>IsActive=false</c>) — nothing here ever
/// issues a SQL DELETE.
/// </summary>
public sealed class SectorRuleAdminService : ISectorRuleAdminService
{
    private static readonly Regex CodeRegex = new(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);
    private static readonly IReadOnlySet<int> CoreModuleIds =
        new HashSet<int>(SectorConfigurationCatalog.CoreModules.Select(m => (int)m));
    private static readonly IReadOnlySet<string> AllowedValueTypes = new HashSet<string>(StringComparer.Ordinal) { "string", "int", "bool", "json" };
    private static readonly IReadOnlySet<string> AllowedTemplateItemKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        "document-numbering-scheme", "chart-account", "setting"
    };

    private readonly MasterDbContext _db;

    public SectorRuleAdminService(MasterDbContext db)
    {
        _db = db;
    }

    // ---------- Full dump / version ----------

    public async Task<SectorRuleSetAdminDto> GetFullSetAsync(CancellationToken cancellationToken)
    {
        var stamp = await GetOrCreateStampAsync(cancellationToken);

        var segments = await _db.SectorSegments.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync(cancellationToken);
        var domains = await _db.SectorDomains.AsNoTracking().OrderBy(d => d.SortOrder).ToListAsync(cancellationToken);
        var segmentDomains = await _db.SectorSegmentDomains.AsNoTracking().ToListAsync(cancellationToken);
        var moduleRules = await _db.SectorModuleRules.AsNoTracking().OrderBy(r => r.SortOrder).ToListAsync(cancellationToken);
        var moduleDependencies = await _db.SectorModuleDependencies.AsNoTracking().ToListAsync(cancellationToken);
        var settings = await _db.SectorDefaultSettings.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync(cancellationToken);
        var templates = await _db.SectorDataTemplates.AsNoTracking().OrderBy(t => t.SortOrder).ToListAsync(cancellationToken);
        var templateIds = templates.Select(t => t.Id).ToList();
        var items = await _db.SectorDataTemplateItems.AsNoTracking()
            .Where(i => templateIds.Contains(i.TemplateId))
            .OrderBy(i => i.SortOrder)
            .ToListAsync(cancellationToken);

        return new SectorRuleSetAdminDto
        {
            Version = stamp.Version,
            Segments = segments.Select(ToDto).ToList(),
            Domains = domains.Select(ToDto).ToList(),
            SegmentDomains = segmentDomains.Select(ToDto).ToList(),
            ModuleRules = moduleRules.Select(ToDto).ToList(),
            ModuleDependencies = moduleDependencies.Select(ToDto).ToList(),
            Settings = settings.Select(ToDto).ToList(),
            Templates = templates.Select(t => ToDto(t, items.Where(i => i.TemplateId == t.Id))).ToList()
        };
    }

    public async Task<long> GetVersionAsync(CancellationToken cancellationToken)
    {
        var stamp = await _db.SectorRuleSetStamps.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == SectorRuleSetStamp.SingletonId, cancellationToken);
        return stamp?.Version ?? 0;
    }

    public Task<SectorRuleSeedResult> SeedFromCatalogAsync(bool force, string? actor, CancellationToken cancellationToken)
        => SectorRuleSeeder.SeedAsync(_db, force, actor, cancellationToken);

    // ---------- Segments ----------

    public async Task<IReadOnlyList<SectorSegmentAdminDto>> ListSegmentsAsync(CancellationToken cancellationToken)
        => await _db.SectorSegments.AsNoTracking().OrderBy(s => s.SortOrder).Select(s => ToDto(s)).ToListAsync(cancellationToken);

    public async Task<Result<SectorSegmentAdminDto>> GetSegmentAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _db.SectorSegments.FindAsync(new object[] { id }, cancellationToken);
        return entity is null
            ? Result.Failure<SectorSegmentAdminDto>(NotFound("Segment introuvable."))
            : Result.Success(ToDto(entity));
    }

    public async Task<Result<SectorSegmentAdminDto>> CreateSegmentAsync(CreateSectorSegmentRequest request, string? actor, CancellationToken cancellationToken)
    {
        var codeResult = ValidateCode(request.Code);
        if (codeResult.IsFailure) return Result.Failure<SectorSegmentAdminDto>(codeResult.Error);
        var code = codeResult.Value;

        if (await _db.SectorSegments.AnyAsync(s => s.Code == code, cancellationToken))
            return Result.Failure<SectorSegmentAdminDto>(DuplicateCode());

        var entity = SectorSegment.Create(code, request.LabelFr, request.DescriptionFr, request.IconKey, request.SortOrder, request.DefaultWarehouseName);
        entity.SetAuditInfo(actor ?? "admin");
        entity.MarkAdminManaged();
        _db.SectorSegments.Add(entity);
        await BumpVersionAndSaveAsync(actor, cancellationToken);
        return Result.Success(ToDto(entity));
    }

    public async Task<Result<SectorSegmentAdminDto>> UpdateSegmentAsync(Guid id, UpdateSectorSegmentRequest request, string? actor, CancellationToken cancellationToken)
    {
        var entity = await _db.SectorSegments.FindAsync(new object[] { id }, cancellationToken);
        if (entity is null) return Result.Failure<SectorSegmentAdminDto>(NotFound("Segment introuvable."));

        entity.UpdateDetails(request.LabelFr, request.DescriptionFr, request.IconKey, request.SortOrder, request.DefaultWarehouseName);
        entity.SetAuditInfo(actor ?? "admin", isUpdate: true);
        entity.MarkAdminManaged();
        await BumpVersionAndSaveAsync(actor, cancellationToken);
        return Result.Success(ToDto(entity));
    }

    public async Task<Result<bool>> DeactivateSegmentAsync(Guid id, string? actor, CancellationToken cancellationToken)
    {
        var entity = await _db.SectorSegments.FindAsync(new object[] { id }, cancellationToken);
        if (entity is null) return Result.Failure<bool>(NotFound("Segment introuvable."));

        entity.Deactivate();
        entity.SetAuditInfo(actor ?? "admin", isUpdate: true);
        entity.MarkAdminManaged();
        await BumpVersionAndSaveAsync(actor, cancellationToken);
        return Result.Success(true);
    }

    // ---------- Domains ----------

    public async Task<IReadOnlyList<SectorDomainAdminDto>> ListDomainsAsync(CancellationToken cancellationToken)
        => await _db.SectorDomains.AsNoTracking().OrderBy(d => d.SortOrder).Select(d => ToDto(d)).ToListAsync(cancellationToken);

    public async Task<Result<SectorDomainAdminDto>> GetDomainAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _db.SectorDomains.FindAsync(new object[] { id }, cancellationToken);
        return entity is null
            ? Result.Failure<SectorDomainAdminDto>(NotFound("Domaine introuvable."))
            : Result.Success(ToDto(entity));
    }

    public async Task<Result<SectorDomainAdminDto>> CreateDomainAsync(CreateSectorDomainRequest request, string? actor, CancellationToken cancellationToken)
    {
        var codeResult = ValidateCode(request.Code);
        if (codeResult.IsFailure) return Result.Failure<SectorDomainAdminDto>(codeResult.Error);
        var code = codeResult.Value;

        if (await _db.SectorDomains.AnyAsync(d => d.Code == code, cancellationToken))
            return Result.Failure<SectorDomainAdminDto>(DuplicateCode());

        var entity = SectorDomain.Create(code, request.LabelFr, request.SortOrder);
        entity.SetAuditInfo(actor ?? "admin");
        entity.MarkAdminManaged();
        _db.SectorDomains.Add(entity);
        await BumpVersionAndSaveAsync(actor, cancellationToken);
        return Result.Success(ToDto(entity));
    }

    public async Task<Result<SectorDomainAdminDto>> UpdateDomainAsync(Guid id, UpdateSectorDomainRequest request, string? actor, CancellationToken cancellationToken)
    {
        var entity = await _db.SectorDomains.FindAsync(new object[] { id }, cancellationToken);
        if (entity is null) return Result.Failure<SectorDomainAdminDto>(NotFound("Domaine introuvable."));

        entity.UpdateDetails(request.LabelFr, request.SortOrder);
        entity.SetAuditInfo(actor ?? "admin", isUpdate: true);
        entity.MarkAdminManaged();
        await BumpVersionAndSaveAsync(actor, cancellationToken);
        return Result.Success(ToDto(entity));
    }

    public async Task<Result<bool>> DeactivateDomainAsync(Guid id, string? actor, CancellationToken cancellationToken)
    {
        var entity = await _db.SectorDomains.FindAsync(new object[] { id }, cancellationToken);
        if (entity is null) return Result.Failure<bool>(NotFound("Domaine introuvable."));

        entity.Deactivate();
        entity.SetAuditInfo(actor ?? "admin", isUpdate: true);
        entity.MarkAdminManaged();
        await BumpVersionAndSaveAsync(actor, cancellationToken);
        return Result.Success(true);
    }

    // ---------- Segment-domains ----------

    public async Task<IReadOnlyList<SectorSegmentDomainAdminDto>> ListSegmentDomainsAsync(CancellationToken cancellationToken)
        => await _db.SectorSegmentDomains.AsNoTracking().Select(s => ToDto(s)).ToListAsync(cancellationToken);

    public async Task<Result<SectorSegmentDomainAdminDto>> GetSegmentDomainAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _db.SectorSegmentDomains.FindAsync(new object[] { id }, cancellationToken);
        return entity is null
            ? Result.Failure<SectorSegmentDomainAdminDto>(NotFound("Association segment/domaine introuvable."))
            : Result.Success(ToDto(entity));
    }

    public async Task<Result<SectorSegmentDomainAdminDto>> CreateSegmentDomainAsync(CreateSectorSegmentDomainRequest request, string? actor, CancellationToken cancellationToken)
    {
        var segmentExists = await _db.SectorSegments.AnyAsync(s => s.Id == request.SegmentId && s.IsActive, cancellationToken);
        var domainExists = await _db.SectorDomains.AnyAsync(d => d.Id == request.DomainId && d.IsActive, cancellationToken);
        if (!segmentExists || !domainExists)
            return Result.Failure<SectorSegmentDomainAdminDto>(Error.Validation("SegmentOrDomain", "Segment ou domaine introuvable."));

        if (await _db.SectorSegmentDomains.AnyAsync(s => s.SegmentId == request.SegmentId && s.DomainId == request.DomainId, cancellationToken))
            return Result.Failure<SectorSegmentDomainAdminDto>(DuplicateCode());

        var entity = SectorSegmentDomain.Create(request.SegmentId, request.DomainId, request.SortOrder);
        entity.SetAuditInfo(actor ?? "admin");
        entity.MarkAdminManaged();
        _db.SectorSegmentDomains.Add(entity);
        await BumpVersionAndSaveAsync(actor, cancellationToken);
        return Result.Success(ToDto(entity));
    }

    public async Task<Result<SectorSegmentDomainAdminDto>> UpdateSegmentDomainAsync(Guid id, UpdateSectorSegmentDomainRequest request, string? actor, CancellationToken cancellationToken)
    {
        var entity = await _db.SectorSegmentDomains.FindAsync(new object[] { id }, cancellationToken);
        if (entity is null) return Result.Failure<SectorSegmentDomainAdminDto>(NotFound("Association segment/domaine introuvable."));

        entity.UpdateSortOrder(request.SortOrder);
        entity.SetAuditInfo(actor ?? "admin", isUpdate: true);
        entity.MarkAdminManaged();
        await BumpVersionAndSaveAsync(actor, cancellationToken);
        return Result.Success(ToDto(entity));
    }

    public async Task<Result<bool>> DeactivateSegmentDomainAsync(Guid id, string? actor, CancellationToken cancellationToken)
    {
        var entity = await _db.SectorSegmentDomains.FindAsync(new object[] { id }, cancellationToken);
        if (entity is null) return Result.Failure<bool>(NotFound("Association segment/domaine introuvable."));

        entity.Deactivate();
        entity.SetAuditInfo(actor ?? "admin", isUpdate: true);
        entity.MarkAdminManaged();
        await BumpVersionAndSaveAsync(actor, cancellationToken);
        return Result.Success(true);
    }

    // ---------- Module rules ----------

    public async Task<IReadOnlyList<SectorModuleRuleAdminDto>> ListModuleRulesAsync(CancellationToken cancellationToken)
        => await _db.SectorModuleRules.AsNoTracking().OrderBy(r => r.SortOrder).Select(r => ToDto(r)).ToListAsync(cancellationToken);

    public async Task<Result<SectorModuleRuleAdminDto>> GetModuleRuleAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _db.SectorModuleRules.FindAsync(new object[] { id }, cancellationToken);
        return entity is null
            ? Result.Failure<SectorModuleRuleAdminDto>(NotFound("Règle de module introuvable."))
            : Result.Success(ToDto(entity));
    }

    public async Task<Result<SectorModuleRuleAdminDto>> CreateModuleRuleAsync(CreateSectorModuleRuleRequest request, string? actor, CancellationToken cancellationToken)
    {
        var moduleCheck = ValidateModuleForRuleOrDependency(request.ModuleId);
        if (moduleCheck.IsFailure) return Result.Failure<SectorModuleRuleAdminDto>(moduleCheck.Error);

        SectorModuleRule entity;
        if (string.Equals(request.RuleKind, "SegmentBase", StringComparison.OrdinalIgnoreCase))
        {
            if (request.SegmentId is null || !await _db.SectorSegments.AnyAsync(s => s.Id == request.SegmentId, cancellationToken))
                return Result.Failure<SectorModuleRuleAdminDto>(Error.Validation("SegmentId", "Segment ou domaine introuvable."));
            if (await _db.SectorModuleRules.AnyAsync(r => r.RuleKind == SectorModuleRuleKind.SegmentBase && r.SegmentId == request.SegmentId && r.ModuleId == request.ModuleId, cancellationToken))
                return Result.Failure<SectorModuleRuleAdminDto>(DuplicateCode());
            entity = SectorModuleRule.CreateSegmentBase(request.SegmentId.Value, request.ModuleId, request.SortOrder);
        }
        else if (string.Equals(request.RuleKind, "DomainOverlay", StringComparison.OrdinalIgnoreCase))
        {
            if (request.DomainId is null || !await _db.SectorDomains.AnyAsync(d => d.Id == request.DomainId, cancellationToken))
                return Result.Failure<SectorModuleRuleAdminDto>(Error.Validation("DomainId", "Segment ou domaine introuvable."));
            if (await _db.SectorModuleRules.AnyAsync(r => r.RuleKind == SectorModuleRuleKind.DomainOverlay && r.DomainId == request.DomainId && r.ModuleId == request.ModuleId, cancellationToken))
                return Result.Failure<SectorModuleRuleAdminDto>(DuplicateCode());
            entity = SectorModuleRule.CreateDomainOverlay(request.DomainId.Value, request.ModuleId, request.SortOrder);
        }
        else
        {
            return Result.Failure<SectorModuleRuleAdminDto>(Error.Validation("RuleKind", "Type de règle invalide."));
        }

        entity.SetAuditInfo(actor ?? "admin");
        entity.MarkAdminManaged();
        _db.SectorModuleRules.Add(entity);
        await BumpVersionAndSaveAsync(actor, cancellationToken);
        return Result.Success(ToDto(entity));
    }

    public async Task<Result<SectorModuleRuleAdminDto>> UpdateModuleRuleAsync(Guid id, UpdateSectorModuleRuleRequest request, string? actor, CancellationToken cancellationToken)
    {
        var entity = await _db.SectorModuleRules.FindAsync(new object[] { id }, cancellationToken);
        if (entity is null) return Result.Failure<SectorModuleRuleAdminDto>(NotFound("Règle de module introuvable."));

        entity.UpdateSortOrder(request.SortOrder);
        entity.SetAuditInfo(actor ?? "admin", isUpdate: true);
        entity.MarkAdminManaged();
        await BumpVersionAndSaveAsync(actor, cancellationToken);
        return Result.Success(ToDto(entity));
    }

    public async Task<Result<bool>> DeactivateModuleRuleAsync(Guid id, string? actor, CancellationToken cancellationToken)
    {
        var entity = await _db.SectorModuleRules.FindAsync(new object[] { id }, cancellationToken);
        if (entity is null) return Result.Failure<bool>(NotFound("Règle de module introuvable."));

        entity.Deactivate();
        entity.SetAuditInfo(actor ?? "admin", isUpdate: true);
        entity.MarkAdminManaged();
        await BumpVersionAndSaveAsync(actor, cancellationToken);
        return Result.Success(true);
    }

    // ---------- Module dependencies ----------

    public async Task<IReadOnlyList<SectorModuleDependencyAdminDto>> ListModuleDependenciesAsync(CancellationToken cancellationToken)
        => await _db.SectorModuleDependencies.AsNoTracking().Select(d => ToDto(d)).ToListAsync(cancellationToken);

    public async Task<Result<SectorModuleDependencyAdminDto>> GetModuleDependencyAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _db.SectorModuleDependencies.FindAsync(new object[] { id }, cancellationToken);
        return entity is null
            ? Result.Failure<SectorModuleDependencyAdminDto>(NotFound("Dépendance de module introuvable."))
            : Result.Success(ToDto(entity));
    }

    public async Task<Result<SectorModuleDependencyAdminDto>> CreateModuleDependencyAsync(CreateSectorModuleDependencyRequest request, string? actor, CancellationToken cancellationToken)
    {
        var moduleCheck = ValidateModuleForRuleOrDependency(request.ModuleId);
        if (moduleCheck.IsFailure) return Result.Failure<SectorModuleDependencyAdminDto>(moduleCheck.Error);
        var requiredCheck = ValidateModuleForRuleOrDependency(request.RequiredModuleId);
        if (requiredCheck.IsFailure) return Result.Failure<SectorModuleDependencyAdminDto>(requiredCheck.Error);

        if (request.ModuleId == request.RequiredModuleId)
            return Result.Failure<SectorModuleDependencyAdminDto>(Error.Validation("SelfDependency", "Un module ne peut pas dépendre de lui-même."));

        if (await _db.SectorModuleDependencies.AnyAsync(d => d.ModuleId == request.ModuleId && d.RequiredModuleId == request.RequiredModuleId, cancellationToken))
            return Result.Failure<SectorModuleDependencyAdminDto>(DuplicateCode());

        var existingEdges = await _db.SectorModuleDependencies.AsNoTracking()
            .Where(d => d.IsActive)
            .Select(d => new { d.ModuleId, d.RequiredModuleId })
            .ToListAsync(cancellationToken);

        if (CreatesCycle(existingEdges.Select(e => (e.ModuleId, e.RequiredModuleId)), (request.ModuleId, request.RequiredModuleId)))
            return Result.Failure<SectorModuleDependencyAdminDto>(Error.Validation("Cycle", "Dépendance circulaire détectée entre modules."));

        var entity = SectorModuleDependency.Create(request.ModuleId, request.RequiredModuleId);
        entity.SetAuditInfo(actor ?? "admin");
        entity.MarkAdminManaged();
        _db.SectorModuleDependencies.Add(entity);
        await BumpVersionAndSaveAsync(actor, cancellationToken);
        return Result.Success(ToDto(entity));
    }

    public async Task<Result<bool>> DeactivateModuleDependencyAsync(Guid id, string? actor, CancellationToken cancellationToken)
    {
        var entity = await _db.SectorModuleDependencies.FindAsync(new object[] { id }, cancellationToken);
        if (entity is null) return Result.Failure<bool>(NotFound("Dépendance de module introuvable."));

        entity.Deactivate();
        entity.SetAuditInfo(actor ?? "admin", isUpdate: true);
        entity.MarkAdminManaged();
        await BumpVersionAndSaveAsync(actor, cancellationToken);
        return Result.Success(true);
    }

    // ---------- Default settings ----------

    public async Task<IReadOnlyList<SectorDefaultSettingAdminDto>> ListSettingsAsync(CancellationToken cancellationToken)
        => await _db.SectorDefaultSettings.AsNoTracking().OrderBy(s => s.SortOrder).Select(s => ToDto(s)).ToListAsync(cancellationToken);

    public async Task<Result<SectorDefaultSettingAdminDto>> GetSettingAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _db.SectorDefaultSettings.FindAsync(new object[] { id }, cancellationToken);
        return entity is null
            ? Result.Failure<SectorDefaultSettingAdminDto>(NotFound("Paramètre introuvable."))
            : Result.Success(ToDto(entity));
    }

    public async Task<Result<SectorDefaultSettingAdminDto>> CreateSettingAsync(CreateSectorDefaultSettingRequest request, string? actor, CancellationToken cancellationToken)
    {
        var validation = ValidateSetting(request.SettingKey, request.SettingValue, request.ValueType);
        if (validation.IsFailure) return Result.Failure<SectorDefaultSettingAdminDto>(validation.Error);

        if (await _db.SectorDefaultSettings.AnyAsync(
                s => s.SegmentCode == request.SegmentCode && s.DomainCode == request.DomainCode && s.SettingKey == request.SettingKey,
                cancellationToken))
            return Result.Failure<SectorDefaultSettingAdminDto>(DuplicateCode());

        var entity = SectorDefaultSetting.Create(request.SegmentCode, request.DomainCode, request.SettingKey, request.SettingValue, request.ValueType, request.SortOrder);
        entity.SetAuditInfo(actor ?? "admin");
        entity.MarkAdminManaged();
        _db.SectorDefaultSettings.Add(entity);
        await BumpVersionAndSaveAsync(actor, cancellationToken);
        return Result.Success(ToDto(entity));
    }

    public async Task<Result<SectorDefaultSettingAdminDto>> UpdateSettingAsync(Guid id, UpdateSectorDefaultSettingRequest request, string? actor, CancellationToken cancellationToken)
    {
        var entity = await _db.SectorDefaultSettings.FindAsync(new object[] { id }, cancellationToken);
        if (entity is null) return Result.Failure<SectorDefaultSettingAdminDto>(NotFound("Paramètre introuvable."));

        var validation = ValidateSetting(entity.SettingKey, request.SettingValue, request.ValueType);
        if (validation.IsFailure) return Result.Failure<SectorDefaultSettingAdminDto>(validation.Error);

        entity.UpdateValue(request.SettingValue, request.ValueType, request.SortOrder);
        entity.SetAuditInfo(actor ?? "admin", isUpdate: true);
        entity.MarkAdminManaged();
        await BumpVersionAndSaveAsync(actor, cancellationToken);
        return Result.Success(ToDto(entity));
    }

    public async Task<Result<bool>> DeactivateSettingAsync(Guid id, string? actor, CancellationToken cancellationToken)
    {
        var entity = await _db.SectorDefaultSettings.FindAsync(new object[] { id }, cancellationToken);
        if (entity is null) return Result.Failure<bool>(NotFound("Paramètre introuvable."));

        entity.Deactivate();
        entity.SetAuditInfo(actor ?? "admin", isUpdate: true);
        entity.MarkAdminManaged();
        await BumpVersionAndSaveAsync(actor, cancellationToken);
        return Result.Success(true);
    }

    // ---------- Data templates ----------

    public async Task<IReadOnlyList<SectorDataTemplateAdminDto>> ListTemplatesAsync(CancellationToken cancellationToken)
    {
        var templates = await _db.SectorDataTemplates.AsNoTracking().OrderBy(t => t.SortOrder).ToListAsync(cancellationToken);
        var templateIds = templates.Select(t => t.Id).ToList();
        var items = await _db.SectorDataTemplateItems.AsNoTracking()
            .Where(i => templateIds.Contains(i.TemplateId))
            .OrderBy(i => i.SortOrder)
            .ToListAsync(cancellationToken);
        return templates.Select(t => ToDto(t, items.Where(i => i.TemplateId == t.Id))).ToList();
    }

    public async Task<Result<SectorDataTemplateAdminDto>> GetTemplateAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _db.SectorDataTemplates.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (entity is null) return Result.Failure<SectorDataTemplateAdminDto>(NotFound("Modèle introuvable."));

        var items = await _db.SectorDataTemplateItems.AsNoTracking()
            .Where(i => i.TemplateId == id)
            .OrderBy(i => i.SortOrder)
            .ToListAsync(cancellationToken);
        return Result.Success(ToDto(entity, items));
    }

    public async Task<Result<SectorDataTemplateAdminDto>> CreateTemplateAsync(CreateSectorDataTemplateRequest request, string? actor, CancellationToken cancellationToken)
    {
        var codeResult = ValidateCode(request.Code);
        if (codeResult.IsFailure) return Result.Failure<SectorDataTemplateAdminDto>(codeResult.Error);
        var code = codeResult.Value;

        var itemsValidation = ValidateTemplateItems(request.Items);
        if (itemsValidation.IsFailure) return Result.Failure<SectorDataTemplateAdminDto>(itemsValidation.Error);

        if (await _db.SectorDataTemplates.AnyAsync(t => t.Code == code, cancellationToken))
            return Result.Failure<SectorDataTemplateAdminDto>(DuplicateCode());

        var entity = SectorDataTemplate.Create(code, request.SegmentCode, request.DomainCode, request.LabelFr, request.DescriptionFr, request.Version, request.SortOrder);
        entity.SetAuditInfo(actor ?? "admin");
        entity.MarkAdminManaged();
        _db.SectorDataTemplates.Add(entity);

        var itemEntities = new List<SectorDataTemplateItem>();
        var sortOrder = 0;
        foreach (var item in request.Items)
        {
            var itemEntity = SectorDataTemplateItem.Create(entity.Id, item.ItemKind, item.PayloadJson, item.SortOrder != 0 ? item.SortOrder : sortOrder);
            itemEntity.SetAuditInfo(actor ?? "admin");
            itemEntity.MarkAdminManaged();
            itemEntities.Add(itemEntity);
            sortOrder++;
        }
        _db.SectorDataTemplateItems.AddRange(itemEntities);

        await BumpVersionAndSaveAsync(actor, cancellationToken);
        return Result.Success(ToDto(entity, itemEntities));
    }

    public async Task<Result<SectorDataTemplateAdminDto>> UpdateTemplateAsync(Guid id, UpdateSectorDataTemplateRequest request, string? actor, CancellationToken cancellationToken)
    {
        var entity = await _db.SectorDataTemplates.FindAsync(new object[] { id }, cancellationToken);
        if (entity is null) return Result.Failure<SectorDataTemplateAdminDto>(NotFound("Modèle introuvable."));

        var itemsValidation = ValidateTemplateItems(request.Items);
        if (itemsValidation.IsFailure) return Result.Failure<SectorDataTemplateAdminDto>(itemsValidation.Error);

        entity.UpdateDetails(request.LabelFr, request.DescriptionFr, request.Version, request.SortOrder);
        entity.SetAuditInfo(actor ?? "admin", isUpdate: true);
        entity.MarkAdminManaged();

        // Items are replaced as a set: deactivate the previous generation, insert the new one.
        var previousItems = await _db.SectorDataTemplateItems.Where(i => i.TemplateId == id).ToListAsync(cancellationToken);
        foreach (var previousItem in previousItems)
        {
            previousItem.Deactivate();
            previousItem.MarkAdminManaged();
        }

        var newItems = new List<SectorDataTemplateItem>();
        var sortOrder = 0;
        foreach (var item in request.Items)
        {
            var itemEntity = SectorDataTemplateItem.Create(id, item.ItemKind, item.PayloadJson, item.SortOrder != 0 ? item.SortOrder : sortOrder);
            itemEntity.SetAuditInfo(actor ?? "admin");
            itemEntity.MarkAdminManaged();
            newItems.Add(itemEntity);
            sortOrder++;
        }
        _db.SectorDataTemplateItems.AddRange(newItems);

        await BumpVersionAndSaveAsync(actor, cancellationToken);
        return Result.Success(ToDto(entity, newItems));
    }

    public async Task<Result<bool>> DeactivateTemplateAsync(Guid id, string? actor, CancellationToken cancellationToken)
    {
        var entity = await _db.SectorDataTemplates.FindAsync(new object[] { id }, cancellationToken);
        if (entity is null) return Result.Failure<bool>(NotFound("Modèle introuvable."));

        entity.Deactivate();
        entity.SetAuditInfo(actor ?? "admin", isUpdate: true);
        entity.MarkAdminManaged();
        await BumpVersionAndSaveAsync(actor, cancellationToken);
        return Result.Success(true);
    }

    // ---------- Validation helpers ----------

    private static Result<string> ValidateCode(string? rawCode)
    {
        var code = (rawCode ?? string.Empty).Trim().ToLowerInvariant();
        if (code.Length == 0 || code.Length > 50 || !CodeRegex.IsMatch(code))
            return Result.Failure<string>(Error.Validation("Code", "Code invalide : minuscules, chiffres et tirets uniquement (50 caractères max)."));
        return Result.Success(code);
    }

    private static Result ValidateModuleForRuleOrDependency(int moduleId)
    {
        if (!Enum.IsDefined(typeof(AppModule), moduleId) || (AppModule)moduleId == AppModule.Honoraires)
            return Result.Failure(Error.Validation("ModuleId", "Module invalide."));
        if (CoreModuleIds.Contains(moduleId))
            return Result.Failure(Error.Validation("ModuleId", "Les modules de base sont toujours actifs et ne peuvent pas figurer dans les règles."));
        return Result.Success();
    }

    private static Result ValidateSetting(string settingKey, string settingValue, string valueType)
    {
        if (string.IsNullOrWhiteSpace(settingKey) || settingKey.Length > 100)
            return Result.Failure(Error.Validation("SettingKey", "Clé de paramètre invalide (1 à 100 caractères)."));

        if (!AllowedValueTypes.Contains(valueType))
            return Result.Failure(Error.Validation("ValueType", "Valeur de paramètre invalide pour le type déclaré."));

        var parsesOk = valueType switch
        {
            "int" => int.TryParse(settingValue, out _),
            "bool" => bool.TryParse(settingValue, out _),
            "json" => TryParseJson(settingValue),
            _ => true // "string" accepts anything, including empty
        };

        return parsesOk
            ? Result.Success()
            : Result.Failure(Error.Validation("SettingValue", "Valeur de paramètre invalide pour le type déclaré."));
    }

    private static Result ValidateTemplateItems(IReadOnlyList<SectorDataTemplateItemRequest> items)
    {
        foreach (var item in items)
        {
            if (!AllowedTemplateItemKinds.Contains(item.ItemKind) || !TryParseJson(item.PayloadJson))
                return Result.Failure(Error.Validation("TemplateItem", "Élément de modèle invalide : type ou contenu non reconnu."));
        }
        return Result.Success();
    }

    private static bool TryParseJson(string json)
    {
        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// DFS-based cycle check over the active dependency edges plus the candidate edge being added.
    /// Returns <c>true</c> if adding <paramref name="candidateEdge"/> would create a cycle.
    /// </summary>
    private static bool CreatesCycle(IEnumerable<(int ModuleId, int RequiredModuleId)> existingEdges, (int ModuleId, int RequiredModuleId) candidateEdge)
    {
        var adjacency = existingEdges.Append(candidateEdge)
            .GroupBy(e => e.ModuleId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.RequiredModuleId).ToList());

        var visiting = new HashSet<int>();
        var visited = new HashSet<int>();

        bool Dfs(int node)
        {
            if (visiting.Contains(node)) return true; // back edge -> cycle
            if (visited.Contains(node)) return false;

            visiting.Add(node);
            if (adjacency.TryGetValue(node, out var neighbours))
            {
                foreach (var neighbour in neighbours)
                {
                    if (Dfs(neighbour)) return true;
                }
            }
            visiting.Remove(node);
            visited.Add(node);
            return false;
        }

        return adjacency.Keys.Any(Dfs);
    }

    // ---------- Version stamp ----------

    private async Task<SectorRuleSetStamp> GetOrCreateStampAsync(CancellationToken cancellationToken)
    {
        var stamp = await _db.SectorRuleSetStamps.SingleOrDefaultAsync(s => s.Id == SectorRuleSetStamp.SingletonId, cancellationToken);
        if (stamp is not null) return stamp;

        stamp = SectorRuleSetStamp.CreateInitial();
        _db.SectorRuleSetStamps.Add(stamp);
        await _db.SaveChangesAsync(cancellationToken);
        return stamp;
    }

    private async Task BumpVersionAndSaveAsync(string? actor, CancellationToken cancellationToken)
    {
        var stamp = await GetOrCreateStampAsync(cancellationToken);
        stamp.Bump(actor);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static Error NotFound(string message) => Error.Validation("NotFound", message);
    private static Error DuplicateCode() => Error.Conflict("Un enregistrement avec ce code existe déjà.");

    // ---------- Mapping ----------

    private static SectorSegmentAdminDto ToDto(SectorSegment s) => new()
    {
        Id = s.Id,
        Code = s.Code,
        LabelFr = s.LabelFr,
        DescriptionFr = s.DescriptionFr,
        IconKey = s.IconKey,
        SortOrder = s.SortOrder,
        DefaultWarehouseName = s.DefaultWarehouseName,
        IsActive = s.IsActive
    };

    private static SectorDomainAdminDto ToDto(SectorDomain d) => new()
    {
        Id = d.Id,
        Code = d.Code,
        LabelFr = d.LabelFr,
        SortOrder = d.SortOrder,
        IsActive = d.IsActive
    };

    private static SectorSegmentDomainAdminDto ToDto(SectorSegmentDomain sd) => new()
    {
        Id = sd.Id,
        SegmentId = sd.SegmentId,
        DomainId = sd.DomainId,
        SortOrder = sd.SortOrder,
        IsActive = sd.IsActive
    };

    private static SectorModuleRuleAdminDto ToDto(SectorModuleRule r) => new()
    {
        Id = r.Id,
        RuleKind = r.RuleKind.ToString(),
        SegmentId = r.SegmentId,
        DomainId = r.DomainId,
        ModuleId = r.ModuleId,
        SortOrder = r.SortOrder,
        IsActive = r.IsActive
    };

    private static SectorModuleDependencyAdminDto ToDto(SectorModuleDependency d) => new()
    {
        Id = d.Id,
        ModuleId = d.ModuleId,
        RequiredModuleId = d.RequiredModuleId,
        IsActive = d.IsActive
    };

    private static SectorDefaultSettingAdminDto ToDto(SectorDefaultSetting s) => new()
    {
        Id = s.Id,
        SegmentCode = s.SegmentCode,
        DomainCode = s.DomainCode,
        SettingKey = s.SettingKey,
        SettingValue = s.SettingValue,
        ValueType = s.ValueType,
        SortOrder = s.SortOrder,
        IsActive = s.IsActive
    };

    private static SectorDataTemplateItemAdminDto ToDto(SectorDataTemplateItem i) => new()
    {
        Id = i.Id,
        ItemKind = i.ItemKind,
        PayloadJson = i.PayloadJson,
        SortOrder = i.SortOrder,
        IsActive = i.IsActive
    };

    private static SectorDataTemplateAdminDto ToDto(SectorDataTemplate t, IEnumerable<SectorDataTemplateItem> items) => new()
    {
        Id = t.Id,
        Code = t.Code,
        SegmentCode = t.SegmentCode,
        DomainCode = t.DomainCode,
        LabelFr = t.LabelFr,
        DescriptionFr = t.DescriptionFr,
        Version = t.Version,
        SortOrder = t.SortOrder,
        IsActive = t.IsActive,
        Items = items.Select(ToDto).ToList()
    };
}
