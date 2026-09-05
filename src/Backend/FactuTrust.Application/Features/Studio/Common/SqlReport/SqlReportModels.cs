using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Features.Studio.Common.SqlReport;

/// <summary>Une arête de jointure, TOUJOURS issue d'une clé étrangère réelle (jamais du modèle).</summary>
public sealed record SqlReportJoinEdge(string FromTable, string FromColumn, string ToTable, string ToColumn);

/// <summary>
/// Photo du schéma réel autour d'une table de faits : ses colonnes, celles des tables joignables
/// (1 à 2 sauts), et les arêtes de jointure. C'est la SEULE source de vérité du constructeur SQL —
/// tout identifiant absent d'ici est écarté.
/// </summary>
public sealed record SqlReportSchemaSnapshot(
    string FactTable,
    IReadOnlyDictionary<string, IReadOnlyList<SqlColumnInfo>> ColumnsByTable,
    IReadOnlyList<SqlReportJoinEdge> Edges);

/// <summary>Requête prête à exécuter : SQL paramétré, requête de comptage et colonnes de sortie.</summary>
public sealed record SqlReportQuery(
    string Sql,
    string CountSql,
    IReadOnlyDictionary<string, object?> Parameters,
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Exécute une <see cref="ReportDefinition"/> sur une table de faits réelle du tenant.
/// Implémenté dans Infrastructure (accès à la base du tenant).
/// </summary>
public interface ISqlReportEngine
{
    /// <summary>
    /// Champs disponibles pour une table de faits : ses colonnes + celles des tables joignables
    /// auxquelles l'utilisateur a droit. Clés au format <c>Table_Colonne</c>.
    /// </summary>
    Task<Result<IReadOnlyList<ReportFieldMeta>>> DescribeAsync(
        Guid tenantId, string factTable, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exécute l'état. Agrégation faite par SQL : les totaux sont exacts, jamais tronqués.
    /// <paramref name="presetKey"/> — quand la définition vient d'un état prêt à l'emploi, sa clé :
    /// le moteur vérifie alors que le préréglage se résout ENTIÈREMENT sur le schéma et les droits
    /// de l'utilisateur, et refuse plutôt que de l'exécuter à moitié.
    /// </summary>
    Task<Result<ReportResultDto>> RunAsync(
        Guid tenantId, string factTable, ReportDefinition definition, int? maxRows = null,
        string? presetKey = null, CancellationToken cancellationToken = default);
}
