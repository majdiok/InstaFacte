using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Common.SqlReport;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Filet STRUCTUREL du catalogue d'états : chaque préréglage est confronté au schéma réel, tel que
/// le modèle EF le décrit — colonnes ET clés étrangères.
///
/// Raison d'être : les préréglages sont écrits à la main et nomment des champs sous la forme
/// <c>Table_Colonne</c>. Rien n'obligeait ces noms à exister. Quatre d'entre eux regroupaient sur une
/// table qu'AUCUNE clé étrangère ne relie à leur table de faits (<c>Products_Name</c> depuis
/// <c>SupplierInvoiceLines</c>, entre autres) : la clé ne se résolvait pas, le regroupement partait
/// vide, et SQL Server rejetait la requête produite. Les tests unitaires ne pouvaient pas le voir,
/// puisqu'ils travaillent sur des schémas fabriqués pour le test.
///
/// Ici, le schéma vient du modèle EF : toute dérive entre le catalogue et la base fait échouer la
/// CI, sans qu'aucune base ne soit nécessaire (seul <c>DbContext.Model</c> est lu, jamais la
/// connexion).
/// </summary>
public sealed class SqlReportPresetSchemaConformanceTests
{
    /// <summary>Modèle EF du tenant. Aucune connexion n'est ouverte : seul le modèle est lu.</summary>
    private static readonly Lazy<IModel> TenantModel = new(() =>
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer("Server=(local);Database=none;Integrated Security=true")
            .Options;
        using var context = new TenantDbContext(options);
        return context.Model;
    });

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<SqlColumnInfo>>> Tables =
        new(() => ReadTables(TenantModel.Value));

    private static readonly Lazy<IReadOnlyList<SqlReportJoinEdge>> Edges =
        new(() => ReadEdges(TenantModel.Value));

    public static TheoryData<string> AllPresets()
    {
        var data = new TheoryData<string>();
        foreach (var preset in SqlReportPresetCatalog.All)
            data.Add(preset.Key);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllPresets))]
    public void Every_preset_resolves_entirely_against_the_real_schema(string key)
    {
        var preset = SqlReportPresetCatalog.Find(key)!;
        var snapshot = SnapshotFor(preset.FactTable);

        Assert.True(
            SqlReportPresetCatalog.ResolvesAgainst(preset, snapshot),
            $"Le préréglage « {key} » nomme un champ que le schéma réel ne porte pas. " +
            "Corrigez le catalogue, ou déclarez la relation manquante dans SqlReportLogicalRelations.");
    }

    [Theory]
    [MemberData(nameof(AllPresets))]
    public void Every_preset_builds_valid_sql_without_dropping_anything(string key)
    {
        var preset = SqlReportPresetCatalog.Find(key)!;
        var snapshot = SnapshotFor(preset.FactTable);
        var definition = SqlReportPresetCatalog.Materialize(
            preset, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        Assert.True(
            SqlReportSqlBuilder.TryBuild(snapshot, definition, 200, out var query, out var error),
            $"Le préréglage « {key} » ne produit pas de requête : {error}");

        // Un avertissement signale un élément ÉCARTÉ : sur un état écrit en dur, cela veut dire que
        // les chiffres ne sont plus ceux annoncés. Le catalogue exige le tout ou rien.
        Assert.True(query!.Warnings.Count == 0,
            $"Le préréglage « {key} » perd un élément : {string.Join(" | ", query.Warnings)}");

        // Le défaut qui a motivé ce filet : un GROUP BY suivi de rien.
        Assert.DoesNotContain("GROUP BY  ", query.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain("GROUP BY ORDER", query.Sql, StringComparison.Ordinal);
        AssertOrderByPrecedesPaging(query.Sql);
    }

    [Fact]
    public void Declared_logical_relations_point_at_columns_that_actually_exist()
    {
        foreach (var relation in SqlReportLogicalRelations.All)
        {
            Assert.True(Tables.Value.TryGetValue(relation.FromTable, out var from),
                $"Table « {relation.FromTable} » inconnue du modèle EF.");
            Assert.Contains(from!, c => string.Equals(c.Name, relation.FromColumn, StringComparison.OrdinalIgnoreCase));

            Assert.True(Tables.Value.TryGetValue(relation.ToTable, out var to),
                $"Table « {relation.ToTable} » inconnue du modèle EF.");
            Assert.Contains(to!, c => string.Equals(c.Name, relation.ToColumn, StringComparison.OrdinalIgnoreCase));

            // Une relation déclarée ne doit combler qu'un MANQUE : si la contrainte existe désormais,
            // la déclaration est à retirer plutôt qu'à laisser doubler le schéma.
            Assert.DoesNotContain(Edges.Value.Where(e => !IsDeclared(e)), e =>
                string.Equals(e.FromTable, relation.FromTable, StringComparison.OrdinalIgnoreCase)
                && string.Equals(e.FromColumn, relation.FromColumn, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static bool IsDeclared(SqlReportJoinEdge edge) =>
        SqlReportLogicalRelations.All.Any(r =>
            string.Equals(r.FromTable, edge.FromTable, StringComparison.OrdinalIgnoreCase)
            && string.Equals(r.FromColumn, edge.FromColumn, StringComparison.OrdinalIgnoreCase));

    internal static void AssertOrderByPrecedesPaging(string sql)
    {
        var orderBy = sql.LastIndexOf(" ORDER BY ", StringComparison.Ordinal);
        var offset = sql.LastIndexOf(" OFFSET ", StringComparison.Ordinal);
        Assert.True(orderBy >= 0, $"ORDER BY absent : OFFSET/FETCH l'exige. SQL = {sql}");
        Assert.True(orderBy < offset, $"ORDER BY doit précéder OFFSET. SQL = {sql}");
    }

    // ---- Lecture du modèle EF ---------------------------------------------------------------

    /// <summary>
    /// Photo du schéma autour d'une table de faits, calquée sur <c>SqlReportEngine.BuildSnapshotAsync</c> :
    /// deux sauts au plus, chaque table traversée devant être classée par la politique d'accès, et les
    /// colonnes sensibles retirées. Les permissions sont toutes accordées : ce test porte sur le
    /// schéma, pas sur les droits.
    /// </summary>
    private static SqlReportSchemaSnapshot SnapshotFor(string factTable)
    {
        var canonical = SqlReportAccessPolicy.Describe(factTable)?.Table ?? factTable;
        Assert.True(Tables.Value.ContainsKey(canonical), $"Table de faits « {canonical} » absente du modèle EF.");

        var included = new Dictionary<string, IReadOnlyList<SqlColumnInfo>>(StringComparer.OrdinalIgnoreCase)
        {
            [canonical] = SqlReportAccessPolicy.FilterColumns(Tables.Value[canonical])
        };
        var kept = new List<SqlReportJoinEdge>();
        var frontier = new List<string> { canonical };

        for (var hop = 0; hop < 2; hop++)
        {
            var next = new List<string>();
            foreach (var from in frontier)
            {
                foreach (var edge in Edges.Value.Where(e =>
                    string.Equals(e.FromTable, from, StringComparison.OrdinalIgnoreCase)))
                {
                    if (string.Equals(edge.ToTable, from, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!SqlReportAccessPolicy.TryAuthorize(edge.ToTable, _ => true, out var access, out _))
                        continue;
                    if (!Tables.Value.TryGetValue(access!.Table, out var columns))
                        continue;

                    if (!kept.Any(e => string.Equals(e.FromTable, from, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(e.FromColumn, edge.FromColumn, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(e.ToTable, access.Table, StringComparison.OrdinalIgnoreCase)))
                        kept.Add(new SqlReportJoinEdge(from, edge.FromColumn, access.Table, edge.ToColumn));

                    if (included.ContainsKey(access.Table))
                        continue;
                    included[access.Table] = SqlReportAccessPolicy.FilterColumns(columns);
                    next.Add(access.Table);
                }
            }
            frontier = next;
        }

        return new SqlReportSchemaSnapshot(canonical, included, kept);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<SqlColumnInfo>> ReadTables(IModel model)
    {
        var byTable = new Dictionary<string, List<SqlColumnInfo>>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entity in model.GetEntityTypes())
        {
            var store = StoreObjectIdentifier.Create(entity, StoreObjectType.Table);
            if (store is null)
                continue;

            var table = store.Value.Name;
            if (!byTable.TryGetValue(table, out var columns))
                byTable[table] = columns = new List<SqlColumnInfo>();

            // Les types possédés (Money vers « Total » + « TotalCurrency ») sont des entity types
            // distincts mappés sur la MÊME table : les regrouper par nom de table les réunit,
            // exactement comme INFORMATION_SCHEMA les rendrait.
            foreach (var property in entity.GetProperties())
            {
                var column = property.GetColumnName(store.Value);
                if (string.IsNullOrEmpty(column) || !seen.Add(table + "." + column))
                    continue;

                var dataType = BareSqlType(property.GetColumnType(store.Value));
                columns.Add(new SqlColumnInfo(
                    column!, dataType, property.IsNullable, SqlSchemaGuard.IsNumericSqlType(dataType)));
            }
        }

        return byTable.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<SqlColumnInfo>)kv.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static List<SqlReportJoinEdge> ReadEdges(IModel model)
    {
        var edges = new List<SqlReportJoinEdge>();

        foreach (var entity in model.GetEntityTypes())
        {
            var store = StoreObjectIdentifier.Create(entity, StoreObjectType.Table);
            if (store is null)
                continue;

            foreach (var fk in entity.GetForeignKeys())
            {
                // Une relation de possession n'est pas une jointure : les deux « tables » n'en font qu'une.
                if (fk.IsOwnership || fk.Properties.Count != 1 || fk.PrincipalKey.Properties.Count != 1)
                    continue;

                var principal = StoreObjectIdentifier.Create(fk.PrincipalEntityType, StoreObjectType.Table);
                if (principal is null
                    || string.Equals(principal.Value.Name, store.Value.Name, StringComparison.OrdinalIgnoreCase))
                    continue;

                var fromColumn = fk.Properties[0].GetColumnName(store.Value);
                var toColumn = fk.PrincipalKey.Properties[0].GetColumnName(principal.Value);
                if (string.IsNullOrEmpty(fromColumn) || string.IsNullOrEmpty(toColumn))
                    continue;

                edges.Add(new SqlReportJoinEdge(store.Value.Name, fromColumn!, principal.Value.Name, toColumn!));
            }
        }

        // Les mêmes relations déclarées qu'en production : le test doit voir le schéma que le moteur voit.
        foreach (var relation in SqlReportLogicalRelations.All)
            edges.Add(new SqlReportJoinEdge(relation.FromTable, relation.FromColumn, relation.ToTable, relation.ToColumn));

        return edges;
    }

    /// <summary>« decimal(18,3) » devient « decimal » : INFORMATION_SCHEMA.DATA_TYPE ne porte pas la précision.</summary>
    private static string BareSqlType(string? columnType)
    {
        if (string.IsNullOrWhiteSpace(columnType))
            return "nvarchar";
        var parenthesis = columnType!.IndexOf('(');
        return (parenthesis > 0 ? columnType[..parenthesis] : columnType).Trim();
    }
}
