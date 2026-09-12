using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Studio;
using MediatR;

namespace FactuTrust.Application.Features.Studio.Entities;

// ---- List ----

public sealed record ListCustomEntitiesQuery(bool IncludeInactive = false)
    : IRequest<Result<IReadOnlyList<CustomEntityDto>>>;

public sealed class ListCustomEntitiesQueryHandler
    : IRequestHandler<ListCustomEntitiesQuery, Result<IReadOnlyList<CustomEntityDto>>>
{
    private readonly ICustomEntityRepository _entities;
    private readonly ICustomFieldRepository _fields;
    private readonly ICurrentUser _currentUser;

    public ListCustomEntitiesQueryHandler(ICustomEntityRepository entities, ICustomFieldRepository fields, ICurrentUser currentUser)
    {
        _entities = entities;
        _fields = fields;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<CustomEntityDto>>> Handle(ListCustomEntitiesQuery request, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out _, out var err))
            return Result.Failure<IReadOnlyList<CustomEntityDto>>(err);

        var list = await _entities.ListAsync(tenantId, request.IncludeInactive, cancellationToken);
        var dtos = new List<CustomEntityDto>(list.Count);
        foreach (var e in list)
        {
            var count = await _fields.CountByEntityAsync(tenantId, e.Id, cancellationToken);
            dtos.Add(StudioMappers.ToDto(e, count));
        }
        return Result.Success<IReadOnlyList<CustomEntityDto>>(dtos);
    }
}

// ---- Get by id ----

public sealed record GetCustomEntityByIdQuery(Guid Id) : IRequest<Result<CustomEntityDto>>;

public sealed class GetCustomEntityByIdQueryHandler
    : IRequestHandler<GetCustomEntityByIdQuery, Result<CustomEntityDto>>
{
    private readonly ICustomEntityRepository _entities;
    private readonly ICustomFieldRepository _fields;
    private readonly ICurrentUser _currentUser;

    public GetCustomEntityByIdQueryHandler(ICustomEntityRepository entities, ICustomFieldRepository fields, ICurrentUser currentUser)
    {
        _entities = entities;
        _fields = fields;
        _currentUser = currentUser;
    }

    public async Task<Result<CustomEntityDto>> Handle(GetCustomEntityByIdQuery request, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out _, out var err))
            return Result.Failure<CustomEntityDto>(err);

        var entity = await _entities.GetByIdAsync(tenantId, request.Id, cancellationToken);
        if (entity is null)
            return Result.Failure<CustomEntityDto>(Error.NotFound("CustomEntity", request.Id));

        var count = await _fields.CountByEntityAsync(tenantId, entity.Id, cancellationToken);
        return Result.Success(StudioMappers.ToDto(entity, count));
    }
}

// ---- Create ----

public sealed record CreateCustomEntityCommand(CreateCustomEntityRequest Request) : IRequest<Result<CustomEntityDto>>;

public sealed class CreateCustomEntityCommandHandler
    : IRequestHandler<CreateCustomEntityCommand, Result<CustomEntityDto>>
{
    private readonly ICustomEntityRepository _entities;
    private readonly IStudioQuotaService _quota;
    private readonly IAuditService _audit;
    private readonly ICurrentUser _currentUser;

    public CreateCustomEntityCommandHandler(ICustomEntityRepository entities, IStudioQuotaService quota, IAuditService audit, ICurrentUser currentUser)
    {
        _entities = entities;
        _quota = quota;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<Result<CustomEntityDto>> Handle(CreateCustomEntityCommand command, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out var userId, out var err))
            return Result.Failure<CustomEntityDto>(err);

        var req = command.Request;
        var key = req.Key?.Trim().ToLowerInvariant() ?? string.Empty;

        if (!StudioKey.IsValidShape(key))
            return Result.Failure<CustomEntityDto>(Error.Validation("key",
                "La clé doit commencer par une lettre et ne contenir que minuscules, chiffres et « _ » (2 à 64 caractères)."));
        if (string.IsNullOrWhiteSpace(req.DisplayName))
            return Result.Failure<CustomEntityDto>(Error.Validation("displayName", "Le nom est obligatoire."));
        if (await _entities.KeyExistsAsync(tenantId, key, cancellationToken))
            return Result.Failure<CustomEntityDto>(Error.Conflict($"Une table avec la clé « {key} » existe déjà."));

        var count = await _entities.CountAsync(tenantId, cancellationToken);
        var quota = await _quota.EnsureUnderLimitAsync(tenantId, StudioQuotas.MaxEntitiesKey, count, StudioQuotas.MaxEntitiesFallback, "tables personnalisées", cancellationToken);
        if (quota.IsFailure)
            return Result.Failure<CustomEntityDto>(quota.Error);

        var plural = string.IsNullOrWhiteSpace(req.DisplayNamePlural) ? req.DisplayName.Trim() : req.DisplayNamePlural.Trim();
        var entity = CustomEntityDefinition.Create(tenantId, key, req.DisplayName.Trim(), plural,
            req.Icon?.Trim(), req.Description?.Trim(), userId, req.SystemId, req.Kind);

        await _entities.AddAsync(entity, cancellationToken);
        await StudioAudit.SafeLogAsync(_audit, "Studio.Entity.Created", "CustomEntity", entity.Id,
            null, new { entity.Key, entity.DisplayName, Kind = entity.Kind.ToString() }, cancellationToken);
        return Result.Success(StudioMappers.ToDto(entity, 0));
    }
}

// ---- Update ----

public sealed record UpdateCustomEntityCommand(Guid Id, UpdateCustomEntityRequest Request) : IRequest<Result<CustomEntityDto>>;

public sealed class UpdateCustomEntityCommandHandler
    : IRequestHandler<UpdateCustomEntityCommand, Result<CustomEntityDto>>
{
    private readonly ICustomEntityRepository _entities;
    private readonly ICustomFieldRepository _fields;
    private readonly IAuditService _audit;
    private readonly ICurrentUser _currentUser;

    public UpdateCustomEntityCommandHandler(ICustomEntityRepository entities, ICustomFieldRepository fields, IAuditService audit, ICurrentUser currentUser)
    {
        _entities = entities;
        _fields = fields;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<Result<CustomEntityDto>> Handle(UpdateCustomEntityCommand command, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out var userId, out var err))
            return Result.Failure<CustomEntityDto>(err);

        var entity = await _entities.GetByIdAsync(tenantId, command.Id, cancellationToken);
        if (entity is null)
            return Result.Failure<CustomEntityDto>(Error.NotFound("CustomEntity", command.Id));

        var req = command.Request;
        if (string.IsNullOrWhiteSpace(req.DisplayName))
            return Result.Failure<CustomEntityDto>(Error.Validation("displayName", "Le nom est obligatoire."));

        var oldValues = new { entity.DisplayName, entity.IsActive };
        var plural = string.IsNullOrWhiteSpace(req.DisplayNamePlural) ? req.DisplayName.Trim() : req.DisplayNamePlural.Trim();
        entity.Update(req.DisplayName.Trim(), plural, req.Icon?.Trim(), req.Description?.Trim(), req.IsActive, userId);
        await _entities.UpdateAsync(entity, cancellationToken);
        await StudioAudit.SafeLogAsync(_audit, "Studio.Entity.Updated", "CustomEntity", entity.Id,
            oldValues, new { entity.DisplayName, entity.IsActive }, cancellationToken);

        var count = await _fields.CountByEntityAsync(tenantId, entity.Id, cancellationToken);
        return Result.Success(StudioMappers.ToDto(entity, count));
    }
}

// ---- Delete (soft) ----

public sealed record DeleteCustomEntityCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteCustomEntityCommandHandler : IRequestHandler<DeleteCustomEntityCommand, Result>
{
    private readonly ICustomEntityRepository _entities;
    private readonly IAuditService _audit;
    private readonly ICurrentUser _currentUser;

    public DeleteCustomEntityCommandHandler(ICustomEntityRepository entities, IAuditService audit, ICurrentUser currentUser)
    {
        _entities = entities;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DeleteCustomEntityCommand command, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out var userId, out var err))
            return Result.Failure(err);

        var entity = await _entities.GetByIdAsync(tenantId, command.Id, cancellationToken);
        if (entity is null)
            return Result.Failure(Error.NotFound("CustomEntity", command.Id));

        entity.SoftDelete(userId);
        await _entities.UpdateAsync(entity, cancellationToken);
        await StudioAudit.SafeLogAsync(_audit, "Studio.Entity.Deleted", "CustomEntity", entity.Id,
            new { entity.Key, entity.DisplayName }, null, cancellationToken);
        return Result.Success();
    }
}
