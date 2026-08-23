namespace FactuTrust.Domain.Enums;

/// <summary>
/// Where a custom report reads its data from. Append-only for migration safety.
/// </summary>
public enum CustomReportDataSourceKind
{
    /// <summary>A custom entity (records stored as JSON in CustomRecord).</summary>
    CustomEntity = 0,

    /// <summary>A whitelisted, read-only existing first-party source (e.g. Invoices, Clients).</summary>
    ExistingSource = 1,

    /// <summary>
    /// Une table RÉELLE de la base du tenant, lue par <c>SqlReportEngine</c> : filtres, jointures
    /// (clés étrangères) et agrégation exécutés par SQL. <c>DataSourceRef</c> porte le nom de la table
    /// de faits ; la <c>ReportDefinition</c> utilise des clés <c>Table_Colonne</c>.
    /// </summary>
    SqlQuery = 2
}
