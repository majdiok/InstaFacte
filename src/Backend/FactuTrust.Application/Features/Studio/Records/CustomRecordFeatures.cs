using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Studio.Automations;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.Studio.Records;

/// <summary>Resolves the active custom entity by key for a record operation.</summary>
internal static class RecordEntityResolver
{
    public static async Task<(CustomEntityDefinition? Entity, Error Error)> ResolveAsync(
        ICustomEntityRepository entities, Guid tenantId, string entityKey, CancellationToken ct)
    {
        var entity = await entities.GetByKeyAsync(tenantId, entityKey, ct);
        if (entity is null || !entity.IsActive)
            return (null, Error.Validation("entityKey", $"Table « {entityKey} » introuvable ou inactive."));
        return (entity, Error.None);
    }
}

// ---- List (paged) ----

public sealed record ListCustomRecordsQuery(string EntityKey, string? Search, int Page = 1, int PageSize = 25)
    : IRequest<Result<PagedResult<CustomRecordDto>>>;

public sealed class ListCustomRecordsQueryHandler
    : IRequestHandler<ListCustomRecordsQuery, Result<PagedResult<CustomRecordDto>>>
{
    private readonly ICustomEntityRepository _entities;
    private readonly ICustomFieldRepository _fields;
    private readonly ICustomRecordRepository _records;
    private readonly IStudioComputedFieldReader _computedReader;
    private readonly ICurrentUser _currentUser;

    public ListCustomRecordsQueryHandler(
        ICustomEntityRepository entities, ICustomFieldRepository fields, ICustomRecordRepository records,
        IStudioComputedFieldReader computedReader, ICurrentUser currentUser)
    {
        _entities = entities;
        _fields = fields;
        _records = records;
        _computedReader = computedReader;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<CustomRecordDto>>> Handle(ListCustomRecordsQuery request, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out _, out var err))
            return Result.Failure<PagedResult<CustomRecordDto>>(err);

        var (entity, resolveError) = await RecordEntityResolver.ResolveAsync(_entities, tenantId, request.EntityKey, cancellationToken);
        if (entity is null)
            return Result.Failure<PagedResult<CustomRecordDto>>(resolveError);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200 ? 25 : request.PageSize;

        var (items, total) = await _records.ListAsync(tenantId, entity.Id, request.Search, page, pageSize, cancellationToken);
        var dtos = items.Select(StudioMappers.ToDto).ToList();

        // Compute-on-read: inject Lookup/Rollup values (fresh, never stored).
        var fields = await _fields.ListByEntityAsync(tenantId, entity.Id, includeInactive: false, cancellationToken);
        await _computedReader.EnrichAsync(tenantId, fields, dtos, cancellationToken);

        return Result.Success(PagedResult<CustomRecordDto>.Create(dtos, page, pageSize, total));
    }
}

// ---- Get ----

public sealed record GetCustomRecordQuery(string EntityKey, Guid Id) : IRequest<Result<CustomRecordDto>>;

public sealed class GetCustomRecordQueryHandler : IRequestHandler<GetCustomRecordQuery, Result<CustomRecordDto>>
{
    private readonly ICustomEntityRepository _entities;
    private readonly ICustomFieldRepository _fields;
    private readonly ICustomRecordRepository _records;
    private readonly IStudioComputedFieldReader _computedReader;
    private readonly ICurrentUser _currentUser;

    public GetCustomRecordQueryHandler(
        ICustomEntityRepository entities, ICustomFieldRepository fields, ICustomRecordRepository records,
        IStudioComputedFieldReader computedReader, ICurrentUser currentUser)
    {
        _entities = entities;
        _fields = fields;
        _records = records;
        _computedReader = computedReader;
        _currentUser = currentUser;
    }

    public async Task<Result<CustomRecordDto>> Handle(GetCustomRecordQuery request, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out _, out var err))
            return Result.Failure<CustomRecordDto>(err);

        var (entity, resolveError) = await RecordEntityResolver.ResolveAsync(_entities, tenantId, request.EntityKey, cancellationToken);
        if (entity is null)
            return Result.Failure<CustomRecordDto>(resolveError);

        var record = await _records.GetAsync(tenantId, entity.Id, request.Id, cancellationToken);
        if (record is null)
            return Result.Failure<CustomRecordDto>(Error.NotFound("CustomRecord", request.Id));

        var dto = StudioMappers.ToDto(record);

        // Compute-on-read: inject Lookup/Rollup values (fresh, never stored).
        var fields = await _fields.ListByEntityAsync(tenantId, entity.Id, includeInactive: false, cancellationToken);
        await _computedReader.EnrichAsync(tenantId, fields, new[] { dto }, cancellationToken);

        return Result.Success(dto);
    }
}

// ---- Create ----

public sealed record CreateCustomRecordCommand(string EntityKey, SaveCustomRecordRequest Request) : IRequest<Result<CustomRecordDto>>;

public sealed class CreateCustomRecordCommandHandler : IRequestHandler<CreateCustomRecordCommand, Result<CustomRecordDto>>
{
    private readonly ICustomEntityRepository _entities;
    private readonly ICustomFieldRepository _fields;
    private readonly ICustomRecordRepository _records;
    private readonly IStudioQuotaService _quota;
    private readonly IStudioComputedFieldWriter _computedWriter;
    private readonly IPublisher _publisher;
    private readonly ICurrentUser _currentUser;

    public CreateCustomRecordCommandHandler(
        ICustomEntityRepository entities, ICustomFieldRepository fields, ICustomRecordRepository records,
        IStudioQuotaService quota, IStudioComputedFieldWriter computedWriter, IPublisher publisher, ICurrentUser currentUser)
    {
        _entities = entities;
        _fields = fields;
        _records = records;
        _quota = quota;
        _computedWriter = computedWriter;
        _publisher = publisher;
        _currentUser = currentUser;
    }

    public async Task<Result<CustomRecordDto>> Handle(CreateCustomRecordCommand command, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out var userId, out var err))
            return Result.Failure<CustomRecordDto>(err);

        var (entity, resolveError) = await RecordEntityResolver.ResolveAsync(_entities, tenantId, command.EntityKey, cancellationToken);
        if (entity is null)
            return Result.Failure<CustomRecordDto>(resolveError);

        var recordCount = await _records.CountAsync(tenantId, entity.Id, cancellationToken);
        var quota = await _quota.EnsureUnderLimitAsync(tenantId, StudioQuotas.MaxRecordsKey, recordCount, StudioQuotas.MaxRecordsFallback, "enregistrements par table", cancellationToken);
        if (quota.IsFailure)
            return Result.Failure<CustomRecordDto>(quota.Error);

        var fields = await _fields.ListByEntityAsync(tenantId, entity.Id, includeInactive: false, cancellationToken);
        var validation = CustomRecordValidator.ValidateAndCanonicalize(fields, command.Request.Data);
        if (validation.IsFailure)
            return Result.Failure<CustomRecordDto>(validation.Error);

        // Compute-on-write: allocate AutoNumber references into the canonical JSON before persistence.
        var canonicalJson = await _computedWriter.ApplyOnCreateAsync(tenantId, entity.Id, fields, validation.Value, cancellationToken);

        var uniqueError = await UniqueFieldChecker.CheckAsync(_records, tenantId, entity.Id, fields, canonicalJson, excludeId: null, cancellationToken);
        if (uniqueError is not null)
            return Result.Failure<CustomRecordDto>(uniqueError);

        var record = CustomRecord.Create(tenantId, entity.Id, canonicalJson, userId);
        await _records.AddAsync(record, cancellationToken);

        // ERP bridge: fire OnCreate automations (best-effort — the record is already persisted).
        await StudioRecordLifecycle.PublishAsync(
            _publisher, tenantId, entity.Id, record.Id, canonicalJson, StudioAutomationTrigger.OnCreate, userId, cancellationToken);

        return Result.Success(StudioMappers.ToDto(record));
    }
}

// ---- Update ----

public sealed record UpdateCustomRecordCommand(string EntityKey, Guid Id, SaveCustomRecordRequest Request) : IRequest<Result<CustomRecordDto>>;

public sealed class UpdateCustomRecordCommandHandler : IRequestHandler<UpdateCustomRecordCommand, Result<CustomRecordDto>>
{
    private readonly ICustomEntityRepository _entities;
    private readonly ICustomFieldRepository _fields;
    private readonly ICustomRecordRepository _records;
    private readonly IStudioComputedFieldWriter _computedWriter;
    private readonly IPublisher _publisher;
    private readonly ICurrentUser _currentUser;

    public UpdateCustomRecordCommandHandler(
        ICustomEntityRepository entities, ICustomFieldRepository fields, ICustomRecordRepository records,
        IStudioComputedFieldWriter computedWriter, IPublisher publisher, ICurrentUser currentUser)
    {
        _entities = entities;
        _fields = fields;
        _records = records;
        _computedWriter = computedWriter;
        _publisher = publisher;
        _currentUser = currentUser;
    }

    public async Task<Result<CustomRecordDto>> Handle(UpdateCustomRecordCommand command, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out var userId, out var err))
            return Result.Failure<CustomRecordDto>(err);

        var (entity, resolveError) = await RecordEntityResolver.ResolveAsync(_entities, tenantId, command.EntityKey, cancellationToken);
        if (entity is null)
            return Result.Failure<CustomRecordDto>(resolveError);

        var record = await _records.GetAsync(tenantId, entity.Id, command.Id, cancellationToken);
        if (record is null)
            return Result.Failure<CustomRecordDto>(Error.NotFound("CustomRecord", command.Id));

        var fields = await _fields.ListByEntityAsync(tenantId, entity.Id, includeInactive: false, cancellationToken);
        var validation = CustomRecordValidator.ValidateAndCanonicalize(fields, command.Request.Data);
        if (validation.IsFailure)
            return Result.Failure<CustomRecordDto>(validation.Error);

        // Compute-on-write: AutoNumber values are immutable — preserve the record's existing reference.
        var canonicalJson = await _computedWriter.ApplyOnUpdateAsync(tenantId, entity.Id, fields, validation.Value, record.DataJson, cancellationToken);

        var uniqueError = await UniqueFieldChecker.CheckAsync(_records, tenantId, entity.Id, fields, canonicalJson, excludeId: command.Id, cancellationToken);
        if (uniqueError is not null)
            return Result.Failure<CustomRecordDto>(uniqueError);

        record.SetData(canonicalJson, userId);

        // Optimistic concurrency: if the client sent the RowVersion it loaded, enforce it (mismatch → 409).
        byte[]? expectedRowVersion = null;
        if (!string.IsNullOrWhiteSpace(command.Request.RowVersion))
        {
            try { expectedRowVersion = Convert.FromBase64String(command.Request.RowVersion); }
            catch (FormatException) { expectedRowVersion = null; }
        }
        await _records.UpdateWithConcurrencyAsync(record, expectedRowVersion, cancellationToken);

        // ERP bridge: fire OnUpdate automations (best-effort — the record is already persisted).
        await StudioRecordLifecycle.PublishAsync(
            _publisher, tenantId, entity.Id, record.Id, canonicalJson, StudioAutomationTrigger.OnUpdate, userId, cancellationToken);

        return Result.Success(StudioMappers.ToDto(record));
    }
}

// ---- Delete (soft) ----

public sealed record DeleteCustomRecordCommand(string EntityKey, Guid Id) : IRequest<Result>;

public sealed class DeleteCustomRecordCommandHandler : IRequestHandler<DeleteCustomRecordCommand, Result>
{
    private readonly ICustomEntityRepository _entities;
    private readonly ICustomRecordRepository _records;
    private readonly ICurrentUser _currentUser;

    public DeleteCustomRecordCommandHandler(ICustomEntityRepository entities, ICustomRecordRepository records, ICurrentUser currentUser)
    {
        _entities = entities;
        _records = records;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DeleteCustomRecordCommand command, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out var userId, out var err))
            return Result.Failure(err);

        var (entity, resolveError) = await RecordEntityResolver.ResolveAsync(_entities, tenantId, command.EntityKey, cancellationToken);
        if (entity is null)
            return Result.Failure(resolveError);

        var record = await _records.GetAsync(tenantId, entity.Id, command.Id, cancellationToken);
        if (record is null)
            return Result.Failure(Error.NotFound("CustomRecord", command.Id));

        record.SoftDelete(userId);
        await _records.UpdateAsync(record, cancellationToken);
        return Result.Success();
    }
}
