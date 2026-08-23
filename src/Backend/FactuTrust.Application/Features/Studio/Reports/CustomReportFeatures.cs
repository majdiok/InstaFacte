using System.Text.Json.Nodes;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Common.SqlReport;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Studio.Reports;

/// <summary>Loads a data source's fields + rows and runs a report definition. Shared by saved + preview runs.</summary>
internal static class ReportExecutor
{
    public const int MaxRecords = 5000;

    public static async Task<Result<ReportResultDto>> ExecuteAsync(
        Guid tenantId,
        CustomReportDataSourceKind kind,
        string dataSourceRef,
        ReportDefinition def,
        ICustomEntityRepository entities,
        ICustomFieldRepository fields,
        ICustomRecordRepository records,
        IExistingDataSourceProvider existing,
        ISqlReportEngine sqlReports,
        CancellationToken ct)
    {
        // Troisième branche, strictement additive : les deux chemins historiques ci-dessous sont
        // inchangés, y compris leur troncature en mémoire (désormais SIGNALÉE, plus silencieuse).
        if (kind == CustomReportDataSourceKind.SqlQuery)
            return await sqlReports.RunAsync(tenantId, dataSourceRef, def, cancellationToken: ct);

        if (kind == CustomReportDataSourceKind.ExistingSource)
        {
            var source = ExistingDataSourceCatalog.Find(dataSourceRef);
            if (source is null)
                return Result.Failure<ReportResultDto>(Error.Validation("dataSourceRef", "Source de données non autorisée."));

            var rows = await existing.GetRowsAsync(tenantId, dataSourceRef, MaxRecords, ct);
            if (rows is null)
                return Result.Failure<ReportResultDto>(Error.Validation("dataSourceRef", "Source de données non autorisée."));

            // Le chargement est plafonné : au plafond, les agrégats portent sur un échantillon et
            // doivent le DIRE. Un total silencieusement faux est le pire défaut d'un état.
            return Result.Success(CustomReportRunner.Run(source.Fields, rows, def)
                with { Truncated = rows.Count >= MaxRecords });
        }

        var entity = await entities.GetByKeyAsync(tenantId, dataSourceRef, ct);
        if (entity is null)
            return Result.Failure<ReportResultDto>(Error.Validation("dataSourceRef", $"Table « {dataSourceRef} » introuvable."));

        var fieldDefs = await fields.ListByEntityAsync(tenantId, entity.Id, includeInactive: false, ct);
        var meta = fieldDefs
            .OrderBy(f => f.SortOrder)
            .Select(f => new ReportFieldMeta(f.Key, f.Label,
                f.FieldType is CustomFieldType.Number or CustomFieldType.Decimal))
            .ToList();

        var rawRecords = await records.GetAllForReportAsync(tenantId, entity.Id, MaxRecords, ct);
        var parsed = rawRecords.Select(r => ParseRecord(r.DataJson)).ToList();
        return Result.Success(CustomReportRunner.Run(meta, parsed, def)
            with { Truncated = rawRecords.Count >= MaxRecords });
    }

    private static IReadOnlyDictionary<string, object?> ParseRecord(string dataJson)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        try
        {
            if (JsonNode.Parse(dataJson) is JsonObject obj)
                foreach (var kv in obj)
                    dict[kv.Key] = CustomReportRunner.JsonToPrimitive(kv.Value);
        }
        catch (System.Text.Json.JsonException) { /* skip corrupt row */ }
        return dict;
    }
}

internal static class ReportMapper
{
    public static CustomReportDto ToDto(CustomReportDefinition r) =>
        new(r.Id, r.Key, r.DisplayName, r.DataSourceKind, r.DataSourceRef,
            ReportDefinitionJson.Parse(r.DefinitionJson), r.IsActive);
}

// ---- List ----

public sealed record ListCustomReportsQuery(string? DataSourceRef) : IRequest<Result<IReadOnlyList<CustomReportDto>>>;

public sealed class ListCustomReportsQueryHandler : IRequestHandler<ListCustomReportsQuery, Result<IReadOnlyList<CustomReportDto>>>
{
    private readonly ICustomReportRepository _reports;
    private readonly ICurrentUser _currentUser;

    public ListCustomReportsQueryHandler(ICustomReportRepository reports, ICurrentUser currentUser)
    {
        _reports = reports;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<CustomReportDto>>> Handle(ListCustomReportsQuery request, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out _, out var err))
            return Result.Failure<IReadOnlyList<CustomReportDto>>(err);

        var list = await _reports.ListAsync(tenantId, request.DataSourceRef, cancellationToken);
        return Result.Success<IReadOnlyList<CustomReportDto>>(list.Select(ReportMapper.ToDto).ToList());
    }
}

// ---- Get ----

public sealed record GetCustomReportQuery(Guid Id) : IRequest<Result<CustomReportDto>>;

public sealed class GetCustomReportQueryHandler : IRequestHandler<GetCustomReportQuery, Result<CustomReportDto>>
{
    private readonly ICustomReportRepository _reports;
    private readonly ICurrentUser _currentUser;

    public GetCustomReportQueryHandler(ICustomReportRepository reports, ICurrentUser currentUser)
    {
        _reports = reports;
        _currentUser = currentUser;
    }

    public async Task<Result<CustomReportDto>> Handle(GetCustomReportQuery request, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out _, out var err))
            return Result.Failure<CustomReportDto>(err);

        var report = await _reports.GetByIdAsync(tenantId, request.Id, cancellationToken);
        if (report is null || !report.IsActive)
            return Result.Failure<CustomReportDto>(Error.NotFound("CustomReport", request.Id));

        return Result.Success(ReportMapper.ToDto(report));
    }
}

// ---- Upsert ----

public sealed record UpsertCustomReportCommand(Guid? Id, SaveCustomReportRequest Request) : IRequest<Result<CustomReportDto>>;

public sealed class UpsertCustomReportCommandHandler : IRequestHandler<UpsertCustomReportCommand, Result<CustomReportDto>>
{
    private readonly ICustomReportRepository _reports;
    private readonly ICustomEntityRepository _entities;
    private readonly ICurrentUser _currentUser;

    public UpsertCustomReportCommandHandler(ICustomReportRepository reports, ICustomEntityRepository entities, ICurrentUser currentUser)
    {
        _reports = reports;
        _entities = entities;
        _currentUser = currentUser;
    }

    public async Task<Result<CustomReportDto>> Handle(UpsertCustomReportCommand command, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out var userId, out var err))
            return Result.Failure<CustomReportDto>(err);

        var req = command.Request;
        if (string.IsNullOrWhiteSpace(req.DisplayName))
            return Result.Failure<CustomReportDto>(Error.Validation("displayName", "Le nom du rapport est obligatoire."));

        // Validate the data source against its kind (deny-by-default for existing sources).
        if (req.DataSourceKind == CustomReportDataSourceKind.SqlQuery)
        {
            // Même politique qu'à l'exécution : table classée ET permission détenue. On refuse à
            // l'enregistrement plutôt que de laisser un état qui échouera à chaque lancement.
            if (!SqlReportAccessPolicy.TryAuthorize(req.DataSourceRef, _currentUser.HasPermission, out _, out var accessError))
                return Result.Failure<CustomReportDto>(Error.Validation("dataSourceRef", accessError!));
        }
        else if (req.DataSourceKind == CustomReportDataSourceKind.ExistingSource)
        {
            if (!ExistingDataSourceCatalog.IsWhitelisted(req.DataSourceRef))
                return Result.Failure<CustomReportDto>(Error.Validation("dataSourceRef", "Source de données non autorisée."));
        }
        else
        {
            var entity = await _entities.GetByKeyAsync(tenantId, req.DataSourceRef, cancellationToken);
            if (entity is null)
                return Result.Failure<CustomReportDto>(Error.Validation("dataSourceRef", $"Table « {req.DataSourceRef} » introuvable."));
        }

        var definitionJson = ReportDefinitionJson.Serialize(req.Definition ?? new ReportDefinition());

        CustomReportDefinition report;
        if (command.Id is { } id)
        {
            var existing = await _reports.GetByIdAsync(tenantId, id, cancellationToken);
            if (existing is null)
                return Result.Failure<CustomReportDto>(Error.NotFound("CustomReport", id));
            existing.Update(req.DisplayName.Trim(), req.DataSourceKind, req.DataSourceRef, definitionJson, isActive: true, userId);
            await _reports.UpdateAsync(existing, cancellationToken);
            report = existing;
        }
        else
        {
            var key = await GenerateKeyAsync(tenantId, req.Key, req.DisplayName, cancellationToken);
            report = CustomReportDefinition.Create(tenantId, key, req.DisplayName.Trim(),
                req.DataSourceKind, req.DataSourceRef, definitionJson, userId);
            await _reports.AddAsync(report, cancellationToken);
        }

        return Result.Success(ReportMapper.ToDto(report));
    }

    private async Task<string> GenerateKeyAsync(Guid tenantId, string? requested, string displayName, CancellationToken ct)
    {
        var baseKey = StudioKey.IsValidShape(requested) ? requested! : StudioKey.Slugify(displayName);
        if (string.IsNullOrEmpty(baseKey)) baseKey = "report";
        var key = baseKey;
        var i = 1;
        while (await _reports.KeyExistsAsync(tenantId, key, ct))
            key = $"{baseKey}_{++i}";
        return key;
    }
}

// ---- Delete (deactivate) ----

public sealed record DeleteCustomReportCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteCustomReportCommandHandler : IRequestHandler<DeleteCustomReportCommand, Result>
{
    private readonly ICustomReportRepository _reports;
    private readonly ICurrentUser _currentUser;

    public DeleteCustomReportCommandHandler(ICustomReportRepository reports, ICurrentUser currentUser)
    {
        _reports = reports;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DeleteCustomReportCommand command, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out var userId, out var err))
            return Result.Failure(err);

        var report = await _reports.GetByIdAsync(tenantId, command.Id, cancellationToken);
        if (report is null)
            return Result.Failure(Error.NotFound("CustomReport", command.Id));

        report.Update(report.DisplayName, report.DataSourceKind, report.DataSourceRef, report.DefinitionJson, isActive: false, userId);
        await _reports.UpdateAsync(report, cancellationToken);
        return Result.Success();
    }
}

// ---- Run a saved report ----

public sealed record RunSavedReportQuery(Guid Id) : IRequest<Result<ReportResultDto>>;

public sealed class RunSavedReportQueryHandler : IRequestHandler<RunSavedReportQuery, Result<ReportResultDto>>
{
    private readonly ICustomReportRepository _reports;
    private readonly ICustomEntityRepository _entities;
    private readonly ICustomFieldRepository _fields;
    private readonly ICustomRecordRepository _records;
    private readonly IExistingDataSourceProvider _existing;
    private readonly ISqlReportEngine _sqlReports;
    private readonly ICurrentUser _currentUser;

    public RunSavedReportQueryHandler(
        ICustomReportRepository reports, ICustomEntityRepository entities,
        ICustomFieldRepository fields, ICustomRecordRepository records,
        IExistingDataSourceProvider existing, ISqlReportEngine sqlReports, ICurrentUser currentUser)
    {
        _reports = reports;
        _entities = entities;
        _fields = fields;
        _records = records;
        _existing = existing;
        _sqlReports = sqlReports;
        _currentUser = currentUser;
    }

    public async Task<Result<ReportResultDto>> Handle(RunSavedReportQuery request, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out _, out var err))
            return Result.Failure<ReportResultDto>(err);

        var report = await _reports.GetByIdAsync(tenantId, request.Id, cancellationToken);
        if (report is null || !report.IsActive)
            return Result.Failure<ReportResultDto>(Error.NotFound("CustomReport", request.Id));

        var def = ReportDefinitionJson.Parse(report.DefinitionJson);
        return await ReportExecutor.ExecuteAsync(tenantId, report.DataSourceKind, report.DataSourceRef, def,
            _entities, _fields, _records, _existing, _sqlReports, cancellationToken);
    }
}

// ---- Run an ad-hoc definition (designer live preview) ----

public sealed record RunReportPreviewQuery(CustomReportDataSourceKind Kind, string DataSourceRef, ReportDefinition Definition) : IRequest<Result<ReportResultDto>>;

public sealed class RunReportPreviewQueryHandler : IRequestHandler<RunReportPreviewQuery, Result<ReportResultDto>>
{
    private readonly ICustomEntityRepository _entities;
    private readonly ICustomFieldRepository _fields;
    private readonly ICustomRecordRepository _records;
    private readonly IExistingDataSourceProvider _existing;
    private readonly ISqlReportEngine _sqlReports;
    private readonly ICurrentUser _currentUser;

    public RunReportPreviewQueryHandler(
        ICustomEntityRepository entities, ICustomFieldRepository fields, ICustomRecordRepository records,
        IExistingDataSourceProvider existing, ISqlReportEngine sqlReports, ICurrentUser currentUser)
    {
        _entities = entities;
        _fields = fields;
        _records = records;
        _existing = existing;
        _sqlReports = sqlReports;
        _currentUser = currentUser;
    }

    public async Task<Result<ReportResultDto>> Handle(RunReportPreviewQuery request, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out _, out var err))
            return Result.Failure<ReportResultDto>(err);

        return await ReportExecutor.ExecuteAsync(tenantId, request.Kind, request.DataSourceRef, request.Definition ?? new ReportDefinition(),
            _entities, _fields, _records, _existing, _sqlReports, cancellationToken);
    }
}

// ---- List available data sources (custom entities + whitelisted existing sources) ----

public sealed record GetReportSourcesQuery : IRequest<Result<IReadOnlyList<ReportSourceDto>>>;

public sealed class GetReportSourcesQueryHandler : IRequestHandler<GetReportSourcesQuery, Result<IReadOnlyList<ReportSourceDto>>>
{
    private readonly ICustomEntityRepository _entities;
    private readonly ICustomFieldRepository _fields;
    private readonly ICurrentUser _currentUser;
    private readonly OllamaSettings _settings;

    public GetReportSourcesQueryHandler(
        ICustomEntityRepository entities,
        ICustomFieldRepository fields,
        ICurrentUser currentUser,
        IOptions<OllamaSettings> settings)
    {
        _entities = entities;
        _fields = fields;
        _currentUser = currentUser;
        _settings = settings.Value;
    }

    public async Task<Result<IReadOnlyList<ReportSourceDto>>> Handle(GetReportSourcesQuery request, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out _, out var err))
            return Result.Failure<IReadOnlyList<ReportSourceDto>>(err);

        var sources = new List<ReportSourceDto>();

        var entities = await _entities.ListAsync(tenantId, includeInactive: false, cancellationToken);
        foreach (var e in entities)
        {
            var fields = await _fields.ListByEntityAsync(tenantId, e.Id, includeInactive: false, cancellationToken);
            var meta = fields.OrderBy(f => f.SortOrder)
                .Select(f => new ReportFieldMeta(f.Key, f.Label, f.FieldType is CustomFieldType.Number or CustomFieldType.Decimal))
                .ToList();
            sources.Add(new ReportSourceDto("custom", e.Key, e.DisplayName, meta));
        }

        foreach (var s in ExistingDataSourceCatalog.Sources)
            sources.Add(new ReportSourceDto("existing", s.Key, s.DisplayName, s.Fields));

        // Tables réelles du tenant : uniquement celles que CET utilisateur a le droit de lire.
        // Champs volontairement vides — chargés à la sélection (GetReportSourceFieldsQuery).
        if (_settings.EnableStudioSqlReportEngine)
        {
            foreach (var table in SqlReportAccessPolicy.AllowedFor(_currentUser.HasPermission))
            {
                sources.Add(new ReportSourceDto(
                    "sql",
                    table.Table,
                    table.DisplayName,
                    Array.Empty<ReportFieldMeta>(),
                    SqlReportAccessPolicy.DomainLabel(table.Domain)));
            }
        }

        return Result.Success<IReadOnlyList<ReportSourceDto>>(sources);
    }
}

// ---- Fields of one source (lazy: the SQL sources are introspected on demand) ----

public sealed record GetReportSourceFieldsQuery(string Kind, string DataSourceRef)
    : IRequest<Result<IReadOnlyList<ReportFieldMeta>>>;

public sealed class GetReportSourceFieldsQueryHandler
    : IRequestHandler<GetReportSourceFieldsQuery, Result<IReadOnlyList<ReportFieldMeta>>>
{
    private readonly ICustomEntityRepository _entities;
    private readonly ICustomFieldRepository _fields;
    private readonly ISqlReportEngine _sqlReports;
    private readonly ICurrentUser _currentUser;

    public GetReportSourceFieldsQueryHandler(
        ICustomEntityRepository entities, ICustomFieldRepository fields,
        ISqlReportEngine sqlReports, ICurrentUser currentUser)
    {
        _entities = entities;
        _fields = fields;
        _sqlReports = sqlReports;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<ReportFieldMeta>>> Handle(
        GetReportSourceFieldsQuery request, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out _, out var err))
            return Result.Failure<IReadOnlyList<ReportFieldMeta>>(err);

        switch ((request.Kind ?? string.Empty).ToLowerInvariant())
        {
            case "sql":
                return await _sqlReports.DescribeAsync(tenantId, request.DataSourceRef, cancellationToken);

            case "existing":
            {
                var source = ExistingDataSourceCatalog.Find(request.DataSourceRef);
                return source is null
                    ? Result.Failure<IReadOnlyList<ReportFieldMeta>>(
                        Error.Validation("dataSourceRef", "Source de données non autorisée."))
                    : Result.Success(source.Fields);
            }

            default:
            {
                var entity = await _entities.GetByKeyAsync(tenantId, request.DataSourceRef, cancellationToken);
                if (entity is null)
                    return Result.Failure<IReadOnlyList<ReportFieldMeta>>(
                        Error.Validation("dataSourceRef", $"Table « {request.DataSourceRef} » introuvable."));

                var fieldDefs = await _fields.ListByEntityAsync(tenantId, entity.Id, includeInactive: false, cancellationToken);
                return Result.Success<IReadOnlyList<ReportFieldMeta>>(fieldDefs
                    .OrderBy(f => f.SortOrder)
                    .Select(f => new ReportFieldMeta(f.Key, f.Label,
                        f.FieldType is CustomFieldType.Number or CustomFieldType.Decimal))
                    .ToList());
            }
        }
    }
}

// ---- Ready-made business reports ----

public sealed record GetReportPresetsQuery : IRequest<Result<IReadOnlyList<ReportPresetDto>>>;

public sealed class GetReportPresetsQueryHandler
    : IRequestHandler<GetReportPresetsQuery, Result<IReadOnlyList<ReportPresetDto>>>
{
    private readonly ICurrentUser _currentUser;
    private readonly OllamaSettings _settings;

    public GetReportPresetsQueryHandler(ICurrentUser currentUser, IOptions<OllamaSettings> settings)
    {
        _currentUser = currentUser;
        _settings = settings.Value;
    }

    public Task<Result<IReadOnlyList<ReportPresetDto>>> Handle(
        GetReportPresetsQuery request, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out _, out _, out var err))
            return Task.FromResult(Result.Failure<IReadOnlyList<ReportPresetDto>>(err));

        if (!_settings.EnableStudioSqlReportEngine)
            return Task.FromResult(Result.Success<IReadOnlyList<ReportPresetDto>>(Array.Empty<ReportPresetDto>()));

        // Un préréglage n'apparaît que si l'utilisateur a le droit de lire sa table de faits.
        var presets = SqlReportPresetCatalog.All
            .Where(p => SqlReportAccessPolicy.TryAuthorize(p.FactTable, _currentUser.HasPermission, out _, out _))
            .Select(p => new ReportPresetDto(
                p.Key, p.DisplayName, p.Description,
                SqlReportAccessPolicy.DomainLabel(p.Domain), p.FactTable, p.PeriodFieldKey is not null))
            .ToList();

        return Task.FromResult(Result.Success<IReadOnlyList<ReportPresetDto>>(presets));
    }
}
