namespace FactuTrust.Application.Features.Studio.Common.SqlReport;

/// <summary>Un lien métier déclaré à la main entre deux tables réelles.</summary>
public sealed record SqlReportLogicalRelation(string FromTable, string FromColumn, string ToTable, string ToColumn);

/// <summary>
/// Relations métier que la base ne matérialise PAS par une contrainte de clé étrangère.
///
/// Le moteur d'états ne joint que sur des arêtes de clés étrangères réelles — c'est ce qui garantit
/// qu'aucune jointure n'est devinée. Certaines tables portent pourtant une référence bien réelle
/// sans contrainte associée (<c>StockItems.ProductId</c>), ce qui rend des états légitimes
/// inexécutables : la clé de regroupement ne se résout pas et l'état est refusé.
///
/// Ce tableau est la seule échappatoire, et elle reste sûre :
/// <list type="bullet">
/// <item>il est écrit ICI, à la main, par un développeur — jamais produit par le modèle ni par un
/// appelant ; c'est une valeur d'un ensemble fermé, pas un fragment de requête ;</item>
/// <item>chaque arête est reconfrontée au schéma vivant avant usage (colonne source et colonne
/// cible doivent exister) ;</item>
/// <item>la table cible reste soumise à <see cref="SqlReportAccessPolicy"/> et aux permissions de
/// l'utilisateur, exactement comme une clé étrangère réelle : une jointure ne contourne jamais
/// une permission.</item>
/// </list>
/// Une relation déclarée ne remplace jamais une clé étrangère réelle : elle ne s'applique qu'à une
/// colonne qui n'en porte aucune.
/// </summary>
public static class SqlReportLogicalRelations
{
    private static readonly SqlReportLogicalRelation[] Declared =
    {
        // StockItems porte ProductId et WarehouseId sans contrainte de clé étrangère : sans ces deux
        // arêtes, « stock par entrepôt » et « mouvements de stock par produit » sont inexécutables.
        new("StockItems", "ProductId", "Products", "Id"),
        new("StockItems", "WarehouseId", "Warehouses", "Id")
    };

    public static IReadOnlyList<SqlReportLogicalRelation> All => Declared;

    /// <summary>Relations déclarées au départ de <paramref name="table"/>.</summary>
    public static IEnumerable<SqlReportLogicalRelation> From(string table) =>
        Declared.Where(r => string.Equals(r.FromTable, table, StringComparison.OrdinalIgnoreCase));
}
