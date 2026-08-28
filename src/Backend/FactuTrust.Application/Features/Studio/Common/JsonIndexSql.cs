namespace FactuTrust.Application.Features.Studio.Common;

/// <summary>
/// Pure SQL builders for indexing a custom JSON field via a NON-persisted computed column + filtered
/// index on the shared <c>CustomRecords</c> table. All identifiers derive from a sanitized field key
/// (<see cref="StudioKey.IsValidShape"/>) and are bracket-quoted; the JSON path is a safe literal.
/// Statements are idempotent (<c>IF NOT EXISTS</c>) and never destructive.
/// </summary>
public static class JsonIndexSql
{
    /// <summary>Computed column CAST length: bounds the index key (≤900 bytes &lt; 1700) so inserts never fail.</summary>
    public const int ValueMaxLength = 450;

    public static string ColumnName(string key) => "jx_" + key;
    public static string IndexName(string key) => "IX_CustomRecords_jx_" + key;

    /// <summary>Idempotent ALTER ADD of a non-persisted computed column. Caller MUST have validated the key.</summary>
    public static string AddColumnSql(string key)
    {
        if (!StudioKey.IsValidShape(key))
            throw new ArgumentException($"Invalid Studio key shape: '{key}'.", nameof(key));

        var col = SqlSchemaGuard.Quote(ColumnName(key));
        return
            $"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[dbo].[CustomRecords]') AND [name] = N'{ColumnName(key)}') " +
            $"ALTER TABLE [dbo].[CustomRecords] ADD {col} AS CAST(JSON_VALUE([DataJson], '$.{key}') AS nvarchar({ValueMaxLength}));";
    }

    /// <summary>Idempotent filtered nonclustered index on the computed column. Caller MUST have validated the key.</summary>
    public static string CreateIndexSql(string key)
    {
        if (!StudioKey.IsValidShape(key))
            throw new ArgumentException($"Invalid Studio key shape: '{key}'.", nameof(key));

        var idx = SqlSchemaGuard.Quote(IndexName(key));
        var col = SqlSchemaGuard.Quote(ColumnName(key));
        return
            $"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'{IndexName(key)}' AND [object_id] = OBJECT_ID(N'[dbo].[CustomRecords]')) " +
            $"CREATE NONCLUSTERED INDEX {idx} ON [dbo].[CustomRecords] ([TenantId], [EntityDefinitionId], {col}) WHERE [IsDeleted] = 0;";
    }

    public static string ColumnExistsSql(string key)
    {
        if (!StudioKey.IsValidShape(key))
            throw new ArgumentException($"Invalid Studio key shape: '{key}'.", nameof(key));

        return $"SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[dbo].[CustomRecords]') AND [name] = N'{ColumnName(key)}'";
    }
}
