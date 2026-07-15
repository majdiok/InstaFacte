namespace FactuTrust.Application.Features.Studio.Common;

public sealed record SqlTableInfo(string Name);

public sealed record ForeignKeyHint(string ReferencedTable, string ReferencedColumn);

public sealed record SqlColumnInfo(
    string Name,
    string DataType,
    bool IsNullable,
    bool Numeric,
    string? SuggestedFormat = null,
    ForeignKeyHint? ForeignKey = null);

public sealed record SqlQueryColumnDto(
    string Key,
    string Label,
    string Kind,
    string? Format = null,
    ViewColumnFormatOptions? FormatOptions = null,
    string? DataType = null,
    bool Numeric = false);

public sealed record SqlQueryResultDto(
    IReadOnlyList<SqlQueryColumnDto> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    int TotalRows,
    IReadOnlyDictionary<string, string>? DisplayValues = null);

public sealed record SqlPreviewRequest(string Table, IReadOnlyList<string> Columns, string? Search, int Page = 1, int PageSize = 25);

/// <summary>
/// Read-only introspection + querying of the TENANT database (DB-per-tenant). Implemented in
/// Infrastructure with the tenant connection. Returns null when the requested table is not allowed
/// (denylisted, invalid identifier, or absent from the live schema) — deny-by-default.
/// </summary>
public interface ISqlSchemaProvider
{
    Task<IReadOnlyList<SqlTableInfo>> ListTablesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SqlColumnInfo>?> ListColumnsAsync(Guid tenantId, string table, CancellationToken cancellationToken = default);
    Task<SqlQueryResultDto?> QueryAsync(
        Guid tenantId, string table, IReadOnlyList<string> columns, string? search, int page, int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves a single FK display label (read-only, parameterized).</summary>
    Task<string?> LookupDisplayValueAsync(
        Guid tenantId, string table, string displayColumn, string idValue,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch-resolves FK display labels for many ids in ONE query (read-only, parameterized
    /// <c>WHERE pk IN (…)</c>). Returns a map keyed by the database's id string. Used to avoid the
    /// per-row N+1 storm when rendering view results.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> LookupDisplayValuesAsync(
        Guid tenantId, string table, string displayColumn, IReadOnlyCollection<string> idValues,
        CancellationToken cancellationToken = default);
}

public interface ISqlViewResultEnricher
{
    Task<SqlQueryResultDto> EnrichAsync(
        Guid tenantId,
        string sourceTable,
        IReadOnlyList<ViewColumn> viewColumns,
        IReadOnlyList<SqlColumnInfo> liveColumns,
        SqlQueryResultDto raw,
        CancellationToken cancellationToken = default);
}

public interface ISqlColumnValueFormatter
{
    Task<IReadOnlyDictionary<string, string>> ResolveDisplayValuesAsync(
        Guid tenantId,
        IReadOnlyList<SqlQueryColumnDto> columns,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        CancellationToken cancellationToken = default);

    string FormatDisplay(object? value, SqlQueryColumnDto column, IReadOnlyDictionary<string, string>? resolved);
}
