using System.Text.Json.Nodes;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Studio.Ai;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Studio.Systems;

/// <summary>
/// Résultat de l'export d'un système Studio (PR 3.3, tranche 3.3c1) : métadonnées + spec ré-importable
/// (<see cref="StudioSystemSpecExporter"/>). Aucun identifiant interne ne sort.
/// </summary>
public sealed record StudioSystemExportDto(
    int SpecVersion,
    string SystemKey,
    string SystemDisplayName,
    DateTime ExportedAt,
    int EntityCount,
    int RelationCount,
    int ViewCount,
    bool IncludesSeed,
    IReadOnlyList<string> Warnings,
    JsonObject Spec);

// ---- Export ----

public sealed record ExportCustomSystemQuery(string Key, bool IncludeSeed = false) : IRequest<Result<StudioSystemExportDto>>;

/// <summary>
/// Export d'un système Studio du tenant courant. Fail-closed : drapeau <c>EnableStudioSystemExport</c>
/// éteint ⇒ 404 ; permission <c>studio:design_entities</c> exigée côté handler (en plus du contrôleur) ;
/// seed bornée par <c>StudioExportMaxSeedRows</c> clampé dans [0, <see cref="StudioAiSystemSpec.MaxSeedRecords"/>].
/// Lecture seule + audit ; les logs ne contiennent que la clé et des compteurs.
/// </summary>
public sealed class ExportCustomSystemQueryHandler : IRequestHandler<ExportCustomSystemQuery, Result<StudioSystemExportDto>>
{
    private readonly ICustomSystemRepository _systems;
    private readonly ICustomEntityRepository _entities;
    private readonly ICustomFieldRepository _fields;
    private readonly ICustomFormRepository _forms;
    private readonly ICustomReportRepository _reports;
    private readonly ICustomRecordViewRepository _views;
    private readonly ICustomRecordRepository _records;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;
    private readonly OllamaSettings _settings;
    private readonly ILogger<ExportCustomSystemQueryHandler>? _logger;

    public ExportCustomSystemQueryHandler(
        ICustomSystemRepository systems,
        ICustomEntityRepository entities,
        ICustomFieldRepository fields,
        ICustomFormRepository forms,
        ICustomReportRepository reports,
        ICustomRecordViewRepository views,
        ICustomRecordRepository records,
        ICurrentUser currentUser,
        IAuditService audit,
        IOptions<OllamaSettings> settings,
        ILogger<ExportCustomSystemQueryHandler>? logger = null)
    {
        _systems = systems;
        _entities = entities;
        _fields = fields;
        _forms = forms;
        _reports = reports;
        _views = views;
        _records = records;
        _currentUser = currentUser;
        _audit = audit;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<Result<StudioSystemExportDto>> Handle(ExportCustomSystemQuery request, CancellationToken cancellationToken)
    {
        if (!_settings.EnableStudioSystemExport)
            return Result.Failure<StudioSystemExportDto>(Error.NotFound("Fonctionnalité non disponible."));

        if (!StudioContext.TryGet(_currentUser, out var tenantId, out _, out var err))
            return Result.Failure<StudioSystemExportDto>(err);

        if (!_currentUser.HasPermission(Permissions.Studio.DesignEntities))
            return Result.Failure<StudioSystemExportDto>(Error.Unauthorized("Permission de conception Studio requise."));

        var key = (request.Key ?? string.Empty).Trim().ToLowerInvariant();
        if (key.Length == 0 || !StudioKey.IsValidShape(key))
            return Result.Failure<StudioSystemExportDto>(Error.Validation("key", "Clé système invalide."));

        var system = await _systems.GetByKeyAsync(tenantId, key, cancellationToken);
        if (system is null || !system.IsActive)
            return Result.Failure<StudioSystemExportDto>(new Error("CustomSystem.NotFound", $"CustomSystem with key '{request.Key}' was not found."));

        // ---- Chargement séquentiel (Standard et Junction : champs ; Standard seulement : formulaire, rapports, vues) ----
        var entities = await _entities.ListBySystemIdAsync(tenantId, system.Id, cancellationToken);

        var fieldsByEntity = new Dictionary<Guid, IReadOnlyList<CustomFieldDefinition>>();
        var formByEntity = new Dictionary<Guid, CustomFormDefinition?>();
        var reportsByEntity = new Dictionary<Guid, IReadOnlyList<CustomReportDefinition>>();
        var viewsByEntity = new Dictionary<Guid, IReadOnlyList<CustomRecordViewDefinition>>();

        foreach (var entity in entities.Where(e => e.IsActive))
        {
            fieldsByEntity[entity.Id] = await _fields.ListByEntityAsync(tenantId, entity.Id, includeInactive: false, cancellationToken);
            if (entity.Kind != CustomEntityKind.Standard) continue;

            formByEntity[entity.Id] = await _forms.GetDefaultByEntityAsync(tenantId, entity.Id, cancellationToken);

            var reports = await _reports.ListAsync(tenantId, entity.Key, cancellationToken);
            reportsByEntity[entity.Id] = reports.Where(r => r.DataSourceKind == CustomReportDataSourceKind.CustomEntity).ToList();

            viewsByEntity[entity.Id] = await _views.ListByEntityAsync(tenantId, entity.Id, false, cancellationToken);
        }

        // ---- Seed (optionnelle, bornée) ----
        var maxRows = Math.Clamp(_settings.StudioExportMaxSeedRows, 0, StudioAiSystemSpec.MaxSeedRecords);
        Dictionary<Guid, IReadOnlyList<CustomRecord>>? seedByEntity = null;
        if (request.IncludeSeed && maxRows > 0)
        {
            seedByEntity = new Dictionary<Guid, IReadOnlyList<CustomRecord>>();
            foreach (var entity in entities.Where(e => e.IsActive && e.Kind == CustomEntityKind.Standard))
                seedByEntity[entity.Id] = await _records.GetAllForReportAsync(tenantId, entity.Id, maxRows, cancellationToken);
        }

        var exportedAt = DateTime.UtcNow;
        var result = StudioSystemSpecExporter.Export(
            new StudioSystemExportInput(system, entities, fieldsByEntity, formByEntity, reportsByEntity, viewsByEntity, seedByEntity),
            maxRows,
            exportedAt);

        await StudioAudit.SafeLogAsync(_audit, "Studio.System.Exported", "CustomSystem", system.Id,
            null, new { systemKey = system.Key, includeSeed = request.IncludeSeed, entityCount = result.EntityCount }, cancellationToken);

        _logger?.LogInformation(
            "Studio export {SystemKey}: {EntityCount} tables, {RelationCount} relations, seed={IncludesSeed}",
            system.Key, result.EntityCount, result.RelationCount, result.IncludesSeed);

        return Result.Success(new StudioSystemExportDto(
            StudioSystemSpecExporter.SpecVersion,
            system.Key,
            system.DisplayName,
            exportedAt,
            result.EntityCount,
            result.RelationCount,
            result.ViewCount,
            result.IncludesSeed,
            result.Warnings,
            result.Spec));
    }
}
