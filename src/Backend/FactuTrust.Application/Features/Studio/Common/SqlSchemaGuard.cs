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
        // Reste des internes Studio : la liste historique en oubliait 5 sur 11. Aucun usage métier
        // légitime — on ne construit pas une fenêtre sur la plomberie du Studio.
        "CustomFieldSequences", "CustomSystemDefinitions",
        "CustomEntityAutomations", "CustomAutomationRuns", "StudioAiBuildPlans",
        "TejXmlExportLogs", "StorefrontOutboxMessages", "UserDashboardLayouts",
        "DataProtectionKeys",
        "__EFMigrationsHistory"
    };

    public static bool IsDenied(string table) =>
        string.IsNullOrWhiteSpace(table)
        || DeniedTables.Contains(table)
        || table.StartsWith("__", StringComparison.Ordinal)
        || table.StartsWith("AspNet", StringComparison.OrdinalIgnoreCase); // Identity lives in master, but deny defensively

    /// <summary>
    /// Colonnes qui ne doivent jamais être projetées : secrets, empreintes et jetons de concurrence.
    /// Utilisé par le moteur d'états (deny-by-default au niveau colonne) — volontairement NON appliqué
    /// à l'introspection historique des fenêtres, dont le comportement reste inchangé.
    /// </summary>
    public static bool IsDeniedColumn(string? column)
    {
        if (string.IsNullOrWhiteSpace(column))
            return true;

        if (string.Equals(column, "RowVersion", StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var fragment in DeniedColumnFragments)
        {
            if (column.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static readonly string[] DeniedColumnFragments =
    {
        "Password", "Secret", "ApiKey", "AccessToken", "RefreshToken", "TokenHash",
        "SecurityStamp", "ConcurrencyStamp", "PasswordHash", "PrivateKey"
    };

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
