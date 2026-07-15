using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Studio.Maintenance;

/// <summary>
/// Ensures the JSON computed-column index exists for every active unique field of the tenant.
/// Backfills existing unique fields (created before indexing) and recovers from prior best-effort
/// failures. Best-effort per key; returns how many distinct keys were processed.
/// </summary>
public sealed record RebuildStudioIndexesCommand : IRequest<Result<int>>;

public sealed class RebuildStudioIndexesCommandHandler : IRequestHandler<RebuildStudioIndexesCommand, Result<int>>
{
    private readonly ICustomEntityRepository _entities;
    private readonly ICustomFieldRepository _fields;
    private readonly IJsonIndexManager _jsonIndex;
    private readonly ICurrentUser _currentUser;

    public RebuildStudioIndexesCommandHandler(
        ICustomEntityRepository entities, ICustomFieldRepository fields, IJsonIndexManager jsonIndex, ICurrentUser currentUser)
    {
        _entities = entities;
        _fields = fields;
        _jsonIndex = jsonIndex;
        _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(RebuildStudioIndexesCommand request, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out _, out var err))
            return Result.Failure<int>(err);

        var entities = await _entities.ListAsync(tenantId, includeInactive: false, cancellationToken);
        var keysDone = new HashSet<string>(StringComparer.Ordinal);

        foreach (var e in entities)
        {
            var fields = await _fields.ListByEntityAsync(tenantId, e.Id, includeInactive: false, cancellationToken);
            foreach (var f in fields.Where(x => x.IsUnique))
            {
                if (keysDone.Add(f.Key))
                    await _jsonIndex.EnsureUniqueFieldIndexAsync(tenantId, f.Key, cancellationToken);
            }
        }

        return Result.Success(keysDone.Count);
    }
}
