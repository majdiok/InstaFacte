using System.Data;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories.Studio;

public sealed class CustomRecordRepository : ICustomRecordRepository
{
    private readonly ITenantDbContextFactory _contextFactory;
    private readonly IJsonIndexManager _jsonIndex;

    public CustomRecordRepository(ITenantDbContextFactory contextFactory, IJsonIndexManager jsonIndex)
    {
        _contextFactory = contextFactory;
        _jsonIndex = jsonIndex;
    }

    public async Task<CustomRecord?> GetAsync(Guid tenantId, Guid entityDefinitionId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CustomRecords
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.EntityDefinitionId == entityDefinitionId && r.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<CustomRecord> Items, int TotalCount)> ListAsync(
        Guid tenantId, Guid entityDefinitionId, string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = context.CustomRecords
            .Where(r => r.TenantId == tenantId && r.EntityDefinitionId == entityDefinitionId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            // MVP full-text-ish search over the JSON document; replaced by indexed columns in the hardening phase.
            query = query.Where(r => EF.Functions.Like(r.DataJson, $"%{term}%"));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<int> CountAsync(Guid tenantId, Guid entityDefinitionId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CustomRecords
            .CountAsync(r => r.TenantId == tenantId && r.EntityDefinitionId == entityDefinitionId, cancellationToken);
    }

    public async Task<IReadOnlyList<CustomRecord>> GetAllForReportAsync(Guid tenantId, Guid entityDefinitionId, int max, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CustomRecords
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.EntityDefinitionId == entityDefinitionId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(max)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsWithFieldValueAsync(
        Guid tenantId, Guid entityDefinitionId, string fieldKey, string value, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        // fieldKey is sanitized (^[a-z][a-z0-9_]{1,63}$); validate again before embedding it in the JSON path.
        if (!StudioKey.IsValidShape(fieldKey))
            return false;

        await using var context = _contextFactory.CreateContext();
        var conn = context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(cancellationToken);

        await using var cmd = conn.CreateCommand();
        var exclude = excludeId.HasValue ? " AND Id <> @x" : string.Empty;

        // When the computed-column index exists AND the value fits the indexed length, seek the index
        // ([jx_<key>] = @v) and verify exactly with JSON_VALUE (guards against the rare CAST truncation
        // collision). Otherwise fall back to the always-correct JSON_VALUE scan.
        var jsonPredicate = $"JSON_VALUE(DataJson, '$.{fieldKey}') = @v";
        var useIndex = value.Length <= JsonIndexSql.ValueMaxLength
            && await _jsonIndex.IndexedColumnExistsAsync(tenantId, fieldKey, cancellationToken);
        var predicate = useIndex
            ? $"{SqlSchemaGuard.Quote(JsonIndexSql.ColumnName(fieldKey))} = @v AND {jsonPredicate}"
            : jsonPredicate;

        cmd.CommandText =
            "SELECT TOP 1 1 FROM [dbo].[CustomRecords] " +
            "WHERE TenantId = @t AND EntityDefinitionId = @e AND IsDeleted = 0 " +
            $"AND {predicate}{exclude}";

        AddParam(cmd, "@t", tenantId);
        AddParam(cmd, "@e", entityDefinitionId);
        AddParam(cmd, "@v", value);
        if (excludeId.HasValue) AddParam(cmd, "@x", excludeId.Value);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is not null && result != DBNull.Value;
    }

    private static void AddParam(System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }

    public async Task AddAsync(CustomRecord record, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CustomRecords.Add(record);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CustomRecord record, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CustomRecords.Update(record);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateWithConcurrencyAsync(CustomRecord record, byte[]? expectedRowVersion, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CustomRecords.Update(record);
        if (expectedRowVersion is { Length: > 0 })
        {
            // Use the client's RowVersion as the concurrency token → EF throws DbUpdateConcurrencyException
            // if the row changed since the client loaded it (mapped to HTTP 409 by the middleware).
            context.Entry(record).Property(r => r.RowVersion).OriginalValue = expectedRowVersion;
        }
        await context.SaveChangesAsync(cancellationToken);
    }
}
