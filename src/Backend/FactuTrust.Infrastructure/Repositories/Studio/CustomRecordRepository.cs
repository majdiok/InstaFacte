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
        Guid tenantId, Guid entityDefinitionId, string? search, int page, int pageSize,
        string? filterField = null, string? filterValue = null, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        IQueryable<CustomRecord> query = context.CustomRecords;

        if (!string.IsNullOrWhiteSpace(filterField))
        {
            // Defensive re-check (the handler validates against the entity's active fields): a key that is
            // not a sanitized Studio key is NEVER embedded in SQL — the filter yields an empty page rather
            // than silently degrading to an unfiltered scan.
            if (!StudioKey.IsValidShape(filterField))
                return (Array.Empty<CustomRecord>(), 0);

            var value = filterValue ?? string.Empty;
            var useIndex = value.Length <= JsonIndexSql.ValueMaxLength
                && await _jsonIndex.IndexedColumnExistsAsync(tenantId, filterField, cancellationToken);

            // Raw base query: the JSON path is a validated literal, the value is ALWAYS a DbParameter ({0}).
            // Composed LINQ (tenant/entity/search/order/page) is appended by EF as an outer query, and the
            // soft-delete global filter still applies. The string is built by concatenation on purpose
            // (no interpolation → no EF1002 false positive).
            query = context.CustomRecords.FromSqlRaw(
                "SELECT * FROM [dbo].[CustomRecords] WHERE " + BuildFieldPredicate(filterField, useIndex, "{0}"),
                value);
        }

        query = query.Where(r => r.TenantId == tenantId && r.EntityDefinitionId == entityDefinitionId);

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
        var useIndex = value.Length <= JsonIndexSql.ValueMaxLength
            && await _jsonIndex.IndexedColumnExistsAsync(tenantId, fieldKey, cancellationToken);
        var predicate = BuildFieldPredicate(fieldKey, useIndex, "@v");

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

    public async Task<bool> ExistsWithFieldPairAsync(
        Guid tenantId, Guid entityDefinitionId, string fieldKeyA, string valueA, string fieldKeyB, string valueB,
        Guid? excludeId, CancellationToken cancellationToken = default)
    {
        // Same guard as ExistsWithFieldValueAsync: both keys are re-validated before touching SQL.
        if (!StudioKey.IsValidShape(fieldKeyA) || !StudioKey.IsValidShape(fieldKeyB)
            || string.Equals(fieldKeyA, fieldKeyB, StringComparison.Ordinal))
            return false;

        await using var context = _contextFactory.CreateContext();
        var conn = context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(cancellationToken);

        await using var cmd = conn.CreateCommand();
        var exclude = excludeId.HasValue ? " AND Id <> @x" : string.Empty;

        var useIndexA = valueA.Length <= JsonIndexSql.ValueMaxLength
            && await _jsonIndex.IndexedColumnExistsAsync(tenantId, fieldKeyA, cancellationToken);
        var useIndexB = valueB.Length <= JsonIndexSql.ValueMaxLength
            && await _jsonIndex.IndexedColumnExistsAsync(tenantId, fieldKeyB, cancellationToken);

        cmd.CommandText =
            "SELECT TOP 1 1 FROM [dbo].[CustomRecords] " +
            "WHERE TenantId = @t AND EntityDefinitionId = @e AND IsDeleted = 0 " +
            $"AND {BuildFieldPredicate(fieldKeyA, useIndexA, "@a")} " +
            $"AND {BuildFieldPredicate(fieldKeyB, useIndexB, "@b")}{exclude}";

        AddParam(cmd, "@t", tenantId);
        AddParam(cmd, "@e", entityDefinitionId);
        AddParam(cmd, "@a", valueA);
        AddParam(cmd, "@b", valueB);
        if (excludeId.HasValue) AddParam(cmd, "@x", excludeId.Value);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is not null && result != DBNull.Value;
    }

    /// <summary>
    /// <c>JSON_VALUE(DataJson, '$.&lt;key&gt;') = &lt;param&gt;</c>, prefixed by an index seek on the
    /// computed column <c>[jx_&lt;key&gt;] = &lt;param&gt;</c> when it exists. The key MUST already be
    /// validated by <see cref="StudioKey.IsValidShape"/>; the parameter placeholder is never a value.
    /// </summary>
    private static string BuildFieldPredicate(string fieldKey, bool useIndex, string parameter)
    {
        var jsonPredicate = "JSON_VALUE(DataJson, '$." + fieldKey + "') = " + parameter;
        return useIndex
            ? SqlSchemaGuard.Quote(JsonIndexSql.ColumnName(fieldKey)) + " = " + parameter + " AND " + jsonPredicate
            : jsonPredicate;
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
