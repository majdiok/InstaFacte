using System.Data;
using System.Data.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Common.SqlReport;
using FactuTrust.Domain.Common;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.Studio;

/// <summary>
/// Exécute un état sur les tables RÉELLES du tenant. Lecture seule, uniquement des <c>SELECT</c>.
///
/// Le modèle (ou le concepteur) fournit une <see cref="ReportDefinition"/> — jamais du SQL. Ce service
/// photographie le schéma réel autour de la table de faits, vérifie chaque table contre
/// <see cref="SqlReportAccessPolicy"/> ET les permissions de l'utilisateur, puis délègue l'assemblage
/// à <see cref="SqlReportSqlBuilder"/> (pur). L'agrégation étant faite par SQL, les totaux sont exacts
/// quel que soit le volume ; seul le DÉTAIL est plafonné, et il est alors signalé comme tronqué.
/// </summary>
public sealed class SqlReportEngine : ISqlReportEngine
{
    private const int MaxFirstHopTables = 8;
    private const int MaxSnapshotTables = 12;
    private const int HardMaxRows = 50_000;

    private readonly ITenantDbContextFactory _contextFactory;
    private readonly ISqlSchemaProvider _schema;
    private readonly ICurrentUser _currentUser;
    private readonly OllamaSettings _settings;
    private readonly ILogger<SqlReportEngine> _logger;

    public SqlReportEngine(
        ITenantDbContextFactory contextFactory,
        ISqlSchemaProvider schema,
        ICurrentUser currentUser,
        IOptions<OllamaSettings> settings,
        ILogger<SqlReportEngine> logger)
    {
        _contextFactory = contextFactory;
        _schema = schema;
        _currentUser = currentUser;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<ReportFieldMeta>>> DescribeAsync(
        Guid tenantId, string factTable, CancellationToken cancellationToken = default)
    {
        if (!_settings.EnableStudioSqlReportEngine)
            return Result.Failure<IReadOnlyList<ReportFieldMeta>>(
                Error.Validation("dataSourceRef", "Les états sur les tables de la solution ne sont pas activés."));

        var snapshot = await BuildSnapshotAsync(tenantId, factTable, cancellationToken);
        if (!snapshot.IsSuccess)
            return Result.Failure<IReadOnlyList<ReportFieldMeta>>(snapshot.Error);

        return Result.Success(SqlReportSqlBuilder.BuildFieldMeta(snapshot.Value));
    }

    public async Task<Result<ReportResultDto>> RunAsync(
        Guid tenantId, string factTable, ReportDefinition definition, int? maxRows = null,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.EnableStudioSqlReportEngine)
            return Result.Failure<ReportResultDto>(
                Error.Validation("dataSourceRef", "Les états sur les tables de la solution ne sont pas activés."));

        var snapshotResult = await BuildSnapshotAsync(tenantId, factTable, cancellationToken);
        if (!snapshotResult.IsSuccess)
            return Result.Failure<ReportResultDto>(snapshotResult.Error);

        var limit = Math.Clamp(maxRows ?? _settings.StudioReportMaxRows, 1, HardMaxRows);
        if (!SqlReportSqlBuilder.TryBuild(snapshotResult.Value, definition, limit, out var query, out var buildError))
            return Result.Failure<ReportResultDto>(Error.Validation("definition", buildError ?? "État impossible à construire."));

        await using var ctx = _contextFactory.CreateContext();
        var connection = ctx.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = query!.Sql;
            command.CommandTimeout = Math.Clamp(_settings.StudioReportCommandTimeoutSeconds, 5, 300);
            AddParameters(command, query.Parameters);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
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

        var total = await CountAsync(connection, query!, cancellationToken);
        var truncated = total > rows.Count;

        _logger.LogInformation(
            "Studio SQL report tenant={TenantId} table={Table} grouped={Grouped} columns={Columns} rows={Rows} total={Total} truncated={Truncated}",
            tenantId, factTable, definition?.Grouping.Count > 0, query!.Columns.Count, rows.Count, total, truncated);

        return Result.Success(new ReportResultDto(query.Columns, rows, total, truncated));
    }

    // ---- Photo du schéma -------------------------------------------------------------------

    /// <summary>
    /// Construit la photo du schéma autour de la table de faits : ses colonnes, puis celles des tables
    /// atteintes par clé étrangère (2 sauts au plus). CHAQUE table traversée est soumise à la même
    /// autorisation que la table de faits — une jointure ne contourne jamais une permission.
    /// </summary>
    private async Task<Result<SqlReportSchemaSnapshot>> BuildSnapshotAsync(
        Guid tenantId, string factTable, CancellationToken ct)
    {
        if (!SqlReportAccessPolicy.TryAuthorize(factTable, _currentUser.HasPermission, out var access, out var error))
            return Result.Failure<SqlReportSchemaSnapshot>(Error.Validation("dataSourceRef", error!));

        var canonical = access!.Table;
        var factColumns = await LoadColumnsAsync(tenantId, canonical, ct);
        if (factColumns is null)
            return Result.Failure<SqlReportSchemaSnapshot>(
                Error.Validation("dataSourceRef", $"Table « {canonical} » introuvable dans la base."));

        var columnsByTable = new Dictionary<string, IReadOnlyList<SqlColumnInfo>>(StringComparer.OrdinalIgnoreCase)
        {
            [canonical] = factColumns
        };
        var edges = new List<SqlReportJoinEdge>();

        var firstHop = await ExpandAsync(tenantId, canonical, factColumns, columnsByTable, edges, MaxFirstHopTables, ct);

        // Deuxième saut : indispensable pour « ventes par client » depuis les lignes de facture.
        foreach (var table in firstHop)
        {
            if (columnsByTable.Count >= MaxSnapshotTables)
                break;
            if (!columnsByTable.TryGetValue(table, out var columns))
                continue;
            await ExpandAsync(tenantId, table, columns, columnsByTable, edges,
                MaxSnapshotTables - columnsByTable.Count, ct);
        }

        return Result.Success(new SqlReportSchemaSnapshot(canonical, columnsByTable, edges));
    }

    /// <summary>Ajoute les tables atteintes depuis <paramref name="from"/> par clé étrangère, si autorisées.</summary>
    private async Task<List<string>> ExpandAsync(
        Guid tenantId,
        string from,
        IReadOnlyList<SqlColumnInfo> fromColumns,
        Dictionary<string, IReadOnlyList<SqlColumnInfo>> columnsByTable,
        List<SqlReportJoinEdge> edges,
        int budget,
        CancellationToken ct)
    {
        var added = new List<string>();

        foreach (var column in fromColumns)
        {
            if (budget <= 0)
                break;
            if (column.ForeignKey is not { } fk)
                continue;

            // Une table liée doit elle aussi être classée ET autorisée pour cet utilisateur.
            if (!SqlReportAccessPolicy.TryAuthorize(fk.ReferencedTable, _currentUser.HasPermission, out var access, out _))
                continue;

            var target = access!.Table;
            if (string.Equals(target, from, StringComparison.OrdinalIgnoreCase))
                continue; // auto-référence : inutile pour un état

            var edge = new SqlReportJoinEdge(from, column.Name, target, fk.ReferencedColumn);
            if (!edges.Any(e => e.FromTable == edge.FromTable && e.FromColumn == edge.FromColumn && e.ToTable == edge.ToTable))
                edges.Add(edge);

            if (columnsByTable.ContainsKey(target))
                continue;

            var targetColumns = await LoadColumnsAsync(tenantId, target, ct);
            if (targetColumns is null)
                continue;

            columnsByTable[target] = targetColumns;
            added.Add(target);
            budget--;
        }

        return added;
    }

    private async Task<IReadOnlyList<SqlColumnInfo>?> LoadColumnsAsync(Guid tenantId, string table, CancellationToken ct)
    {
        var columns = await _schema.ListColumnsAsync(tenantId, table, ct);
        if (columns is null || columns.Count == 0)
            return null;
        return SqlReportAccessPolicy.FilterColumns(columns);
    }

    // ---- Exécution -------------------------------------------------------------------------

    private async Task<int> CountAsync(DbConnection connection, SqlReportQuery query, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = query.CountSql;
        command.CommandTimeout = Math.Clamp(_settings.StudioReportCommandTimeoutSeconds, 5, 300);
        AddParameters(command, query.Parameters);
        var scalar = await command.ExecuteScalarAsync(ct);
        return scalar is null or DBNull ? 0 : Convert.ToInt32(scalar);
    }

    private static void AddParameters(DbCommand command, IReadOnlyDictionary<string, object?> parameters)
    {
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }

    private static async Task EnsureOpenAsync(DbConnection connection, CancellationToken ct)
    {
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);
    }

    /// <summary>Mêmes conventions que le fournisseur de fenêtres : dates ISO, binaire masqué.</summary>
    private static object? NormalizeValue(object? value) => value switch
    {
        null => null,
        DateTime dt => dt.ToString("yyyy-MM-dd"),
        DateTimeOffset dto => dto.ToString("yyyy-MM-dd"),
        byte[] => "(binaire)",
        _ => value
    };
}
