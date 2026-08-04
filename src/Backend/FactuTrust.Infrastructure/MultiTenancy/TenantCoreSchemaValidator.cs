using FactuTrust.Domain.Common;
using Microsoft.Data.SqlClient;

namespace FactuTrust.Infrastructure.MultiTenancy;

/// <summary>
/// Validates that core tenant tables match the EF model for POS / wizard flows.
/// </summary>
public static class TenantCoreSchemaValidator
{
    private static readonly (string Table, string[] Columns)[] CoreDocumentAuditColumns =
    [
        ("Invoices", ["CreatedBy", "UpdatedBy", "Version"]),
        ("Clients", ["CreatedBy", "UpdatedBy", "Version"]),
        ("InvoiceLines", ["CreatedBy", "UpdatedBy"]),
        ("InvoiceDrafts", ["CreatedBy", "UpdatedBy"]),
        ("Quotes", ["CreatedBy", "UpdatedBy", "Version"]),
        ("SalesOrders", ["CreatedBy", "UpdatedBy", "Version"]),
        ("DeliveryNotes", ["CreatedBy", "UpdatedBy", "Version"]),
        ("Products", ["CreatedBy", "UpdatedBy", "Version"]),
        // Pricing tables read during POS / wizard submit (PriceResolver / PromotionResolver)
        ("ClientProductPrices", ["CreatedBy", "UpdatedBy", "Version"]),
        ("PriceLists", ["CreatedBy", "UpdatedBy", "Version"]),
        ("Promotions", ["CreatedBy", "UpdatedBy", "Version"]),
    ];

    /// <summary>
    /// Verifies that core sales document tables have the expected audit / concurrency columns.
    /// </summary>
    public static async Task<Result> EnsureInvoiceAuditColumnsAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
        => await EnsureCoreDocumentAuditColumnsAsync(connectionString, cancellationToken);

    /// <summary>
    /// Verifies that core sales document tables have the expected audit / concurrency columns.
    /// </summary>
    public static async Task<Result> EnsureCoreDocumentAuditColumnsAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        var missing = await GetMissingAuditColumnsAsync(connectionString, cancellationToken);
        if (missing.Count == 0)
            return Result.Success();

        var tables = string.Join(", ", missing.Select(m => m.Table).Distinct());
        return Result.Failure(Error.Validation(
            "Migration",
            $"Schéma tenant incomplet (colonnes d'audit manquantes sur : {tables}). Veuillez contacter l'administrateur."));
    }

    internal static async Task<IReadOnlyList<(string Table, string Column)>> GetMissingAuditColumnsAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = BuildMissingColumnsQuery();

        var missing = new List<(string Table, string Column)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            missing.Add((reader.GetString(0), reader.GetString(1)));
        }

        return missing;
    }

    internal static string BuildMissingColumnsQuery()
    {
        var expectedRows = CoreDocumentAuditColumns
            .SelectMany(pair => pair.Columns.Select(column => $"SELECT N'{pair.Table}' AS TableName, N'{column}' AS ColumnName"))
            .ToArray();

        return $"""
            WITH Expected AS (
                {string.Join("\n                UNION ALL\n                ", expectedRows)}
            )
            SELECT e.TableName, e.ColumnName
            FROM Expected e
            INNER JOIN sys.tables st ON st.name = e.TableName
            WHERE NOT EXISTS (
                SELECT 1
                FROM sys.columns sc
                WHERE sc.object_id = st.object_id
                  AND sc.name = e.ColumnName
            )
            ORDER BY e.TableName, e.ColumnName
            """;
    }
}
