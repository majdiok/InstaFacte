using System.Text.RegularExpressions;

namespace FactuTrust.Application.Features.Studio.Common;

/// <summary>
/// Pure safety rules for read-only SQL schema introspection: a hard denylist of sensitive/internal
/// tenant tables, strict SQL identifier validation, and bracket-quoting. No SQL is ever built from a
/// name that does not pass <see cref="IsValidIdentifier"/> AND exists in the live schema.
/// </summary>
public static partial class SqlSchemaGuard
{
    /// <summary>Tables never exposed to introspection (audit, Studio internals, outbox, fiscal export, EF history).</summary>
    public static readonly IReadOnlySet<string> DeniedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "AuditLogs",
        "CustomEntityDefinitions", "CustomFieldDefinitions", "CustomRecords",
        "CustomFormDefinitions", "CustomReportDefinitions", "CustomViewDefinitions",
        "TejXmlExportLogs", "StorefrontOutboxMessages", "UserDashboardLayouts",
        "__EFMigrationsHistory"
    };

    public static bool IsDenied(string table) =>
        string.IsNullOrWhiteSpace(table)
        || DeniedTables.Contains(table)
        || table.StartsWith("__", StringComparison.Ordinal)
        || table.StartsWith("AspNet", StringComparison.OrdinalIgnoreCase); // Identity lives in master, but deny defensively

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex IdentifierPattern();

    public static bool IsValidIdentifier(string? name) =>
        !string.IsNullOrEmpty(name) && name.Length <= 128 && IdentifierPattern().IsMatch(name);

    /// <summary>Bracket-quotes an identifier. Caller MUST have validated it with <see cref="IsValidIdentifier"/> first.</summary>
    public static string Quote(string identifier) => "[" + identifier.Replace("]", "]]") + "]";

    /// <summary>SQL Server numeric types eligible for aggregation/numeric formatting.</summary>
    public static bool IsNumericSqlType(string dataType) => dataType.ToLowerInvariant() switch
    {
        "int" or "bigint" or "smallint" or "tinyint" or "decimal" or "numeric"
            or "money" or "smallmoney" or "float" or "real" => true,
        _ => false
    };

    /// <summary>SQL Server string types eligible for text search.</summary>
    public static bool IsTextSqlType(string dataType) => dataType.ToLowerInvariant() switch
    {
        "char" or "varchar" or "text" or "nchar" or "nvarchar" or "ntext" => true,
        _ => false
    };
}
