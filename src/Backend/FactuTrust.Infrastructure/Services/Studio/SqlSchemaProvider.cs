using System.Data;
using System.Data.Common;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.Studio;

/// <summary>
/// READ-ONLY introspection + querying of the tenant database. Every table/column name is validated
/// against the live INFORMATION_SCHEMA (deny-by-default + denylist) and bracket-quoted before being
/// placed in SQL; values (offset/fetch/search) are always parameterized. Only SELECT is ever issued.
/// </summary>
public sealed class SqlSchemaProvider : ISqlSchemaProvider
{
    private const int MaxPageSize = 200;
    private const int MaxColumns = 50;
    private const int MaxLookupIds = 500;

    private readonly ITenantDbContextFactory _contextFactory;

    public SqlSchemaProvider(ITenantDbContextFactory contextFactory) => _contextFactory = contextFactory;

    public async Task<IReadOnlyList<SqlTableInfo>> ListTablesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        var conn = ctx.Database.GetDbConnection();
        await EnsureOpenAsync(conn, cancellationToken);

        var names = await ReadAllowedTableNamesAsync(conn, cancellationToken);
        return names.Select(n => new SqlTableInfo(n)).ToList();
    }

    public async Task<IReadOnlyList<SqlColumnInfo>?> ListColumnsAsync(Guid tenantId, string table, CancellationToken cancellationToken = default)
    {
        if (!SqlSchemaGuard.IsValidIdentifier(table) || SqlSchemaGuard.IsDenied(table))
            return null;

        await using var ctx = _contextFactory.CreateContext();
        var conn = ctx.Database.GetDbConnection();
        await EnsureOpenAsync(conn, cancellationToken);

        var allowed = await ReadAllowedTableNamesAsync(conn, cancellationToken);
        if (!allowed.Contains(table))
            return null;

        return await ReadColumnsAsync(conn, table, cancellationToken);
    }

    public async Task<SqlQueryResultDto?> QueryAsync(
        Guid tenantId, string table, IReadOnlyList<string> columns, string? search, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (!SqlSchemaGuard.IsValidIdentifier(table) || SqlSchemaGuard.IsDenied(table))
            return null;

        await using var ctx = _contextFactory.CreateContext();
        var conn = ctx.Database.GetDbConnection();
        await EnsureOpenAsync(conn, cancellationToken);

        var allowed = await ReadAllowedTableNamesAsync(conn, cancellationToken);
        if (!allowed.Contains(table))
            return null;

        var liveColumns = await ReadColumnsAsync(conn, table, cancellationToken);
        var liveByName = liveColumns.ToDictionary(c => c.Name, StringComparer.Ordinal);

        // Keep only requested columns that are valid identifiers AND exist in the live schema, preserving order.
        var selected = (columns ?? Array.Empty<string>())
            .Where(c => SqlSchemaGuard.IsValidIdentifier(c) && liveByName.ContainsKey(c))
            .Distinct(StringComparer.Ordinal)
            .Take(MaxColumns)
            .ToList();
        if (selected.Count == 0)
            selected = liveColumns.Take(12).Select(c => c.Name).ToList();
        if (selected.Count == 0)
            return new SqlQueryResultDto(Array.Empty<SqlQueryColumnDto>(), Array.Empty<IReadOnlyDictionary<string, object?>>(), 0);

        var safePage = page < 1 ? 1 : page;
        var safeSize = pageSize is < 1 or > MaxPageSize ? 25 : pageSize;

        var quotedTable = $"[dbo].{SqlSchemaGuard.Quote(table)}";
        var colList = string.Join(", ", selected.Select(SqlSchemaGuard.Quote));
        var orderBy = SqlSchemaGuard.Quote(selected[0]);

        // Search clause over text columns only, fully parameterized.
        var hasSearch = !string.IsNullOrWhiteSpace(search);
        var textCols = selected.Where(c => liveByName.TryGetValue(c, out var ci) && SqlSchemaGuard.IsTextSqlType(ci.DataType)).ToList();
        var whereClause = hasSearch && textCols.Count > 0
            ? " WHERE (" + string.Join(" OR ", textCols.Select(c => $"{SqlSchemaGuard.Quote(c)} LIKE @search")) + ")"
            : string.Empty;

        var total = await ScalarCountAsync(conn, $"SELECT COUNT(*) FROM {quotedTable}{whereClause}", search, whereClause.Length > 0, cancellationToken);

        var sql = $"SELECT {colList} FROM {quotedTable}{whereClause} ORDER BY {orderBy} " +
                  "OFFSET @offset ROWS FETCH NEXT @fetch ROWS ONLY";

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = sql;
            AddParam(cmd, "@offset", (safePage - 1) * safeSize);
            AddParam(cmd, "@fetch", safeSize);
            if (whereClause.Length > 0) AddParam(cmd, "@search", "%" + search!.Trim() + "%");

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>(StringComparer.Ordinal);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var value = await reader.IsDBNullAsync(i, cancellationToken) ? null : reader.GetValue(i);
                    row[reader.GetName(i)] = NormalizeValue(value);
                }
                rows.Add(row);
            }
        }

        var resultColumns = selected.Select(c =>
        {
            var ci = liveByName[c];
            var suggested = SqlColumnValueFormatter.SuggestFormat(ci);
            return new SqlQueryColumnDto(
                ci.Name,
                ci.Name,
                ci.Numeric ? "measure" : "dimension",
                suggested,
                null,
                ci.DataType,
                ci.Numeric);
        }).ToList();
        return new SqlQueryResultDto(resultColumns, rows, total);
    }

    public async Task<string?> LookupDisplayValueAsync(
        Guid tenantId, string table, string displayColumn, string idValue,
        CancellationToken cancellationToken = default)
    {
        if (!SqlSchemaGuard.IsValidIdentifier(table) || SqlSchemaGuard.IsDenied(table)
            || !SqlSchemaGuard.IsValidIdentifier(displayColumn)
            || string.IsNullOrWhiteSpace(idValue))
            return null;

        await using var ctx = _contextFactory.CreateContext();
        var conn = ctx.Database.GetDbConnection();
        await EnsureOpenAsync(conn, cancellationToken);

        var allowed = await ReadAllowedTableNamesAsync(conn, cancellationToken);
        if (!allowed.Contains(table)) return null;

        var liveColumns = await ReadColumnsAsync(conn, table, cancellationToken);
        var liveNames = liveColumns.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        if (!liveNames.Contains(displayColumn)) return null;

        // Find PK column (prefer Id)
        var pkCol = liveNames.Contains("Id") ? "Id"
            : liveColumns.FirstOrDefault(c => c.Name.EndsWith("Id", StringComparison.Ordinal))?.Name;
        if (pkCol is null || !SqlSchemaGuard.IsValidIdentifier(pkCol)) return null;

        var quotedTable = $"[dbo].{SqlSchemaGuard.Quote(table)}";
        var sql = $"SELECT TOP 1 {SqlSchemaGuard.Quote(displayColumn)} FROM {quotedTable} WHERE {SqlSchemaGuard.Quote(pkCol)} = @id";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        AddParam(cmd, "@id", idValue.Trim());
        var scalar = await cmd.ExecuteScalarAsync(cancellationToken);
        return scalar is null or DBNull ? null : scalar.ToString();
    }

    public async Task<IReadOnlyDictionary<string, string>> LookupDisplayValuesAsync(
        Guid tenantId, string table, string displayColumn, IReadOnlyCollection<string> idValues,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!SqlSchemaGuard.IsValidIdentifier(table) || SqlSchemaGuard.IsDenied(table)
            || !SqlSchemaGuard.IsValidIdentifier(displayColumn) || idValues is null)
            return result;

        // Distinct, non-empty ids, capped — one parameter per id in a single IN (...) query.
        var ids = idValues
            .Select(v => v?.Trim())
            .Where(v => !string.IsNullOrEmpty(v))
            .Select(v => v!)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxLookupIds)
            .ToList();
        if (ids.Count == 0) return result;

        await using var ctx = _contextFactory.CreateContext();
        var conn = ctx.Database.GetDbConnection();
        await EnsureOpenAsync(conn, cancellationToken);

        var allowed = await ReadAllowedTableNamesAsync(conn, cancellationToken);
        if (!allowed.Contains(table)) return result;

        var liveColumns = await ReadColumnsAsync(conn, table, cancellationToken);
        var liveNames = liveColumns.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        if (!liveNames.Contains(displayColumn)) return result;

        var pkCol = liveNames.Contains("Id") ? "Id"
            : liveColumns.FirstOrDefault(c => c.Name.EndsWith("Id", StringComparison.Ordinal))?.Name;
        if (pkCol is null || !SqlSchemaGuard.IsValidIdentifier(pkCol)) return result;

        var quotedTable = $"[dbo].{SqlSchemaGuard.Quote(table)}";
        var paramNames = ids.Select((_, i) => "@id" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();
        var sql = $"SELECT {SqlSchemaGuard.Quote(pkCol)} AS __id, {SqlSchemaGuard.Quote(displayColumn)} AS __label " +
                  $"FROM {quotedTable} WHERE {SqlSchemaGuard.Quote(pkCol)} IN ({string.Join(", ", paramNames)})";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        for (var i = 0; i < ids.Count; i++) AddParam(cmd, paramNames[i], ids[i]);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (await reader.IsDBNullAsync(0, cancellationToken)) continue;
            var id = reader.GetValue(0)?.ToString();
            if (string.IsNullOrEmpty(id)) continue;
            var label = await reader.IsDBNullAsync(1, cancellationToken) ? null : reader.GetValue(1)?.ToString();
            if (!string.IsNullOrEmpty(label)) result[id] = label!;
        }
        return result;
    }

    // ---- internals ----

    private static async Task<HashSet<string>> ReadAllowedTableNamesAsync(DbConnection conn, CancellationToken ct)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES " +
            "WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = 'dbo' ORDER BY TABLE_NAME";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var name = reader.GetString(0);
            if (SqlSchemaGuard.IsValidIdentifier(name) && !SqlSchemaGuard.IsDenied(name))
                set.Add(name);
        }
        return set;
    }

    private static async Task<List<SqlColumnInfo>> ReadColumnsAsync(DbConnection conn, string table, CancellationToken ct)
    {
        var fkMap = await ReadForeignKeysAsync(conn, table, ct);
        var cols = new List<SqlColumnInfo>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS " +
            "WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @t ORDER BY ORDINAL_POSITION";
        AddParam(cmd, "@t", table);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var name = reader.GetString(0);
            var dataType = reader.GetString(1);
            var nullable = string.Equals(reader.GetString(2), "YES", StringComparison.OrdinalIgnoreCase);
            if (SqlSchemaGuard.IsValidIdentifier(name))
            {
                fkMap.TryGetValue(name, out var fk);
                var info = new SqlColumnInfo(name, dataType, nullable, SqlSchemaGuard.IsNumericSqlType(dataType),
                    SqlColumnValueFormatter.SuggestFormat(new SqlColumnInfo(name, dataType, nullable,
                        SqlSchemaGuard.IsNumericSqlType(dataType), null, fk)),
                    fk);
                cols.Add(info);
            }
        }
        return cols;
    }

    private static async Task<Dictionary<string, ForeignKeyHint>> ReadForeignKeysAsync(
        DbConnection conn, string table, CancellationToken ct)
    {
        var map = new Dictionary<string, ForeignKeyHint>(StringComparer.Ordinal);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS ColumnName,
                   OBJECT_NAME(fkc.referenced_object_id) AS RefTable,
                   COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS RefColumn
            FROM sys.foreign_key_columns fkc
            INNER JOIN sys.tables t ON fkc.parent_object_id = t.object_id
            WHERE t.name = @t AND SCHEMA_NAME(t.schema_id) = 'dbo'
            """;
        AddParam(cmd, "@t", table);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var colName = reader.GetString(0);
            var refTable = reader.GetString(1);
            var refCol = reader.GetString(2);
            if (SqlSchemaGuard.IsValidIdentifier(colName) && SqlSchemaGuard.IsValidIdentifier(refTable)
                && !SqlSchemaGuard.IsDenied(refTable))
                map[colName] = new ForeignKeyHint(refTable, refCol);
        }
        return map;
    }

    private static async Task<int> ScalarCountAsync(DbConnection conn, string sql, string? search, bool hasSearch, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (hasSearch) AddParam(cmd, "@search", "%" + (search ?? string.Empty).Trim() + "%");
        var scalar = await cmd.ExecuteScalarAsync(ct);
        return scalar is null or DBNull ? 0 : Convert.ToInt32(scalar);
    }

    private static void AddParam(DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }

    private static async Task EnsureOpenAsync(DbConnection conn, CancellationToken ct)
    {
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);
    }

    /// <summary>Keep JSON output clean: dates as ISO strings, byte[] hidden, everything else as-is.</summary>
    private static object? NormalizeValue(object? value) => value switch
    {
        null => null,
        DateTime dt => dt.ToString("yyyy-MM-ddTHH:mm:ss"),
        DateTimeOffset dto => dto.ToString("yyyy-MM-ddTHH:mm:sszzz"),
        byte[] => "(binaire)",
        _ => value
    };
}
