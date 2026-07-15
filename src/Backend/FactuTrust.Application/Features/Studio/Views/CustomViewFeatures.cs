using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Studio;
using MediatR;

namespace FactuTrust.Application.Features.Studio.Views;

internal static class ViewMapper
{
    public static CustomViewDto ToDto(CustomViewDefinition v) =>
        new(v.Id, v.Key, v.DisplayName, v.SourceTable, ViewDefinitionJson.Parse(v.DefinitionJson), v.IsActive);
}

// ---- List ----

public sealed record ListCustomViewsQuery : IRequest<Result<IReadOnlyList<CustomViewDto>>>;

public sealed class ListCustomViewsQueryHandler : IRequestHandler<ListCustomViewsQuery, Result<IReadOnlyList<CustomViewDto>>>
{
    private readonly ICustomViewRepository _views;
    private readonly ICurrentUser _currentUser;

    public ListCustomViewsQueryHandler(ICustomViewRepository views, ICurrentUser currentUser)
    {
        _views = views;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<CustomViewDto>>> Handle(ListCustomViewsQuery request, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out _, out var err))
            return Result.Failure<IReadOnlyList<CustomViewDto>>(err);

        var list = await _views.ListAsync(tenantId, cancellationToken);
        return Result.Success<IReadOnlyList<CustomViewDto>>(list.Select(ViewMapper.ToDto).ToList());
    }
}

// ---- Get ----

public sealed record GetCustomViewQuery(Guid Id) : IRequest<Result<CustomViewDto>>;

public sealed class GetCustomViewQueryHandler : IRequestHandler<GetCustomViewQuery, Result<CustomViewDto>>
{
    private readonly ICustomViewRepository _views;
    private readonly ICurrentUser _currentUser;

    public GetCustomViewQueryHandler(ICustomViewRepository views, ICurrentUser currentUser)
    {
        _views = views;
        _currentUser = currentUser;
    }

    public async Task<Result<CustomViewDto>> Handle(GetCustomViewQuery request, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out _, out var err))
            return Result.Failure<CustomViewDto>(err);

        var view = await _views.GetByIdAsync(tenantId, request.Id, cancellationToken);
        if (view is null || !view.IsActive)
            return Result.Failure<CustomViewDto>(Error.NotFound("CustomView", request.Id));

        return Result.Success(ViewMapper.ToDto(view));
    }
}

// ---- Upsert ----

public sealed record UpsertCustomViewCommand(Guid? Id, SaveCustomViewRequest Request) : IRequest<Result<CustomViewDto>>;

public sealed class UpsertCustomViewCommandHandler : IRequestHandler<UpsertCustomViewCommand, Result<CustomViewDto>>
{
    private readonly ICustomViewRepository _views;
    private readonly ISqlSchemaProvider _schema;
    private readonly ICurrentUser _currentUser;

    public UpsertCustomViewCommandHandler(ICustomViewRepository views, ISqlSchemaProvider schema, ICurrentUser currentUser)
    {
        _views = views;
        _schema = schema;
        _currentUser = currentUser;
    }

    public async Task<Result<CustomViewDto>> Handle(UpsertCustomViewCommand command, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out var userId, out var err))
            return Result.Failure<CustomViewDto>(err);

        var req = command.Request;
        if (string.IsNullOrWhiteSpace(req.DisplayName))
            return Result.Failure<CustomViewDto>(Error.Validation("displayName", "Le nom de la vue est obligatoire."));

        // Validate the source table against the live schema (deny-by-default) and sanitize columns to real ones.
        var liveColumns = await _schema.ListColumnsAsync(tenantId, req.SourceTable, cancellationToken);
        if (liveColumns is null)
            return Result.Failure<CustomViewDto>(Error.Validation("sourceTable", "Table non autorisée ou introuvable."));

        var liveNames = liveColumns.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        var columns = (req.Definition?.Columns ?? Array.Empty<ViewColumn>())
            .Where(c => liveNames.Contains(c.Name))
            .Select(c => new ViewColumn
            {
                Name = c.Name,
                Label = string.IsNullOrWhiteSpace(c.Label) ? null : c.Label.Trim(),
                Width = c.Width == "half" ? "half" : "full",
                Format = string.IsNullOrWhiteSpace(c.Format) ? null : c.Format.Trim(),
                FormatOptions = c.FormatOptions
            })
            .ToList();
        if (columns.Count == 0)
            return Result.Failure<CustomViewDto>(Error.Validation("columns", "Sélectionnez au moins une colonne."));

        var definitionJson = ViewDefinitionJson.Serialize(new ViewDefinition { Columns = columns, Search = req.Definition?.Search ?? false });

        CustomViewDefinition view;
        if (command.Id is { } id)
        {
            var existing = await _views.GetByIdAsync(tenantId, id, cancellationToken);
            if (existing is null)
                return Result.Failure<CustomViewDto>(Error.NotFound("CustomView", id));
            existing.Update(req.DisplayName.Trim(), req.SourceTable, definitionJson, isActive: true, userId);
            await _views.UpdateAsync(existing, cancellationToken);
            view = existing;
        }
        else
        {
            var key = await GenerateKeyAsync(tenantId, req.Key, req.DisplayName, cancellationToken);
            view = CustomViewDefinition.Create(tenantId, key, req.DisplayName.Trim(), req.SourceTable, definitionJson, userId);
            await _views.AddAsync(view, cancellationToken);
        }

        return Result.Success(ViewMapper.ToDto(view));
    }

    private async Task<string> GenerateKeyAsync(Guid tenantId, string? requested, string displayName, CancellationToken ct)
    {
        var baseKey = StudioKey.IsValidShape(requested) ? requested! : StudioKey.Slugify(displayName);
        if (string.IsNullOrEmpty(baseKey)) baseKey = "view";
        var key = baseKey;
        var i = 1;
        while (await _views.KeyExistsAsync(tenantId, key, ct))
            key = $"{baseKey}_{++i}";
        return key;
    }
}

// ---- Delete (deactivate) ----

public sealed record DeleteCustomViewCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteCustomViewCommandHandler : IRequestHandler<DeleteCustomViewCommand, Result>
{
    private readonly ICustomViewRepository _views;
    private readonly ICurrentUser _currentUser;

    public DeleteCustomViewCommandHandler(ICustomViewRepository views, ICurrentUser currentUser)
    {
        _views = views;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DeleteCustomViewCommand command, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out var userId, out var err))
            return Result.Failure(err);

        var view = await _views.GetByIdAsync(tenantId, command.Id, cancellationToken);
        if (view is null)
            return Result.Failure(Error.NotFound("CustomView", command.Id));

        view.Update(view.DisplayName, view.SourceTable, view.DefinitionJson, isActive: false, userId);
        await _views.UpdateAsync(view, cancellationToken);
        return Result.Success();
    }
}

// ---- Run (read-only data) ----

public sealed record RunCustomViewQuery(Guid Id, string? Search, int Page = 1, int PageSize = 25) : IRequest<Result<SqlQueryResultDto>>;

public sealed class RunCustomViewQueryHandler : IRequestHandler<RunCustomViewQuery, Result<SqlQueryResultDto>>
{
    private readonly ICustomViewRepository _views;
    private readonly ISqlSchemaProvider _schema;
    private readonly ISqlViewResultEnricher _enricher;
    private readonly ICurrentUser _currentUser;

    public RunCustomViewQueryHandler(
        ICustomViewRepository views,
        ISqlSchemaProvider schema,
        ISqlViewResultEnricher enricher,
        ICurrentUser currentUser)
    {
        _views = views;
        _schema = schema;
        _enricher = enricher;
        _currentUser = currentUser;
    }

    public async Task<Result<SqlQueryResultDto>> Handle(RunCustomViewQuery request, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out _, out var err))
            return Result.Failure<SqlQueryResultDto>(err);

        var view = await _views.GetByIdAsync(tenantId, request.Id, cancellationToken);
        if (view is null || !view.IsActive)
            return Result.Failure<SqlQueryResultDto>(Error.NotFound("CustomView", request.Id));

        var def = ViewDefinitionJson.Parse(view.DefinitionJson);
        var columns = def.Columns.Select(c => c.Name).ToList();

        var result = await _schema.QueryAsync(tenantId, view.SourceTable, columns, request.Search, request.Page, request.PageSize, cancellationToken);
        if (result is null)
            return Result.Failure<SqlQueryResultDto>(Error.Validation("sourceTable", "Table non autorisée ou introuvable."));

        var liveColumns = await _schema.ListColumnsAsync(tenantId, view.SourceTable, cancellationToken);
        var enriched = await _enricher.EnrichAsync(tenantId, view.SourceTable, def.Columns, liveColumns ?? Array.Empty<SqlColumnInfo>(), result, cancellationToken);
        return Result.Success(enriched);
    }
}
