namespace FactuTrust.Application.Features.Studio.Common;

/// <summary>A whitelisted, read-only existing first-party source available to the report builder.</summary>
public sealed record ExistingDataSource(string Key, string DisplayName, IReadOnlyList<ReportFieldMeta> Fields);

/// <summary>
/// The fixed whitelist of existing sources reports may read. Deny-by-default: only these source
/// keys and only these fields are ever exposed/queried. Shared by the API (list sources) and the
/// infrastructure provider (validate + know the field set).
/// </summary>
public static class ExistingDataSourceCatalog
{
    public static readonly IReadOnlyList<ExistingDataSource> Sources = new[]
    {
        new ExistingDataSource("clients", "Clients", new[]
        {
            new ReportFieldMeta("name", "Nom", false),
            new ReportFieldMeta("type", "Type", false),
            new ReportFieldMeta("city", "Ville", false),
            new ReportFieldMeta("active", "Actif", false),
            new ReportFieldMeta("createdAt", "Créé le", false)
        }),
        new ExistingDataSource("products", "Produits", new[]
        {
            new ReportFieldMeta("code", "Code", false),
            new ReportFieldMeta("name", "Nom", false),
            new ReportFieldMeta("type", "Type", false),
            new ReportFieldMeta("unitPrice", "Prix unitaire", true),
            new ReportFieldMeta("active", "Actif", false),
            new ReportFieldMeta("createdAt", "Créé le", false)
        }),
        new ExistingDataSource("invoices", "Factures", new[]
        {
            new ReportFieldMeta("issueDate", "Date", false),
            new ReportFieldMeta("status", "Statut", false),
            new ReportFieldMeta("clientName", "Client", false),
            new ReportFieldMeta("totalAmount", "Total TTC", true),
            new ReportFieldMeta("totalVat", "TVA", true)
        })
    };

    public static ExistingDataSource? Find(string key) =>
        Sources.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal));

    public static bool IsWhitelisted(string key) => Find(key) is not null;
}

/// <summary>Existing sources that can be the target of a read-only relation field (id + display label).</summary>
public static class ExistingRelationSources
{
    public static readonly IReadOnlyList<(string Key, string DisplayName)> Targets = new[]
    {
        ("clients", "Clients"),
        ("products", "Produits")
    };

    public static bool IsRelationTarget(string key) => Targets.Any(t => t.Key == key);
}

/// <summary>Reads rows from a whitelisted existing source (implemented in Infrastructure with tenant DB access).</summary>
public interface IExistingDataSourceProvider
{
    /// <summary>Returns primitive rows for the source, or null if the key is not whitelisted.</summary>
    Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>?> GetRowsAsync(
        Guid tenantId, string sourceKey, int max, CancellationToken cancellationToken = default);

    /// <summary>Returns id + label options for a relation target source, or null if not a supported target.</summary>
    Task<IReadOnlyList<SelectOptionDto>?> GetRelationOptionsAsync(
        Guid tenantId, string sourceKey, int max, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the whitelisted fields of specific relation-target records (keyed by id string), for
    /// lookup fields. Returns null if the source is not a supported relation target. Deny-by-default:
    /// only the catalog's allowed fields are ever returned.
    /// </summary>
    Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>?> GetRelationRecordsByIdsAsync(
        Guid tenantId, string sourceKey, IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default);
}
