using System.Data;
using System.Text;

using FactuTrust.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FactuTrust.Infrastructure.Accounting;

/// <summary>
/// Primitives de réécriture d'un numéro de compte à travers toutes les colonnes qui le référencent.
/// </summary>
/// <remarks>
/// Extraites telles quelles de <see cref="Nct01ChartMigrationService"/>, qui les a éprouvées sur le
/// remap PCG → NCT 01, pour être partagées avec <see cref="ChartAccountDigitCompactionService"/>
/// sans en écrire une seconde version. Le seul changement de signature est l'allow-list passée en
/// paramètre : chaque service garde la sienne, afin qu'élargir l'un n'élargisse jamais la surface
/// SQL brute de l'autre.
/// </remarks>
internal static class ChartRewritePrimitives
{
    /// <summary>Préfixe temporaire de la réécriture en deux temps (hors alphabet des numéros).</summary>
    internal const string TempPrefix = "\u0001";

    /// <summary>
    /// Réécrit une colonne portant un index unique, en deux passes.
    /// </summary>
    /// <remarks>
    /// La passe intermédiaire préfixe la valeur de <see cref="TempPrefix"/> : elle <b>allonge</b>
    /// donc la donnée d'un caractère. C'est sans risque sur <c>ChartOfAccounts.AccountNumber</c>
    /// (<c>nvarchar(32)</c>), mais tronquerait <c>Payslips.EmployeeAuxiliaryAccount</c> et
    /// <c>PayrollPaymentLines.EmployeeAuxiliaryAccount</c>, tous deux en <c>nvarchar(10)</c>.
    /// Ces colonnes n'ont de toute façon pas d'index unique : la garde ci-dessous interdit l'erreur
    /// plutôt que de la documenter.
    /// </remarks>
    internal static async Task TwoPhaseUniqueRewriteAsync(
        TenantDbContext db,
        string table,
        string column,
        IReadOnlyList<string?> originals,
        Func<string, string> rewrite,
        CancellationToken ct)
    {
        if (!string.Equals(table, "ChartOfAccounts", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"TwoPhaseUniqueRewriteAsync n'est autorisée que sur ChartOfAccounts (demandé : {table}) : "
                + "la valeur intermédiaire est plus longue d'un caractère et déborderait des colonnes étroites.");
        }

        var changing = DistinctChanging(originals, rewrite);
        if (changing.Count == 0)
            return;

        await CaseRewriteAsync(db, table, column, changing, n => TempPrefix + n, ct);
        await CaseRewriteAsync(
            db,
            table,
            column,
            changing.Select(n => TempPrefix + n).ToList(),
            temp => rewrite(temp[TempPrefix.Length..]),
            ct);
    }

    /// <summary>Réécrit une colonne par lots, en <c>CASE … WHEN</c> entièrement paramétré.</summary>
    internal static async Task CaseRewriteAsync(
        TenantDbContext db,
        string table,
        string column,
        IReadOnlyList<string?> originals,
        Func<string, string> rewrite,
        CancellationToken ct)
    {
        var changing = DistinctChanging(originals, rewrite);
        const int chunkSize = 40;
        for (var offset = 0; offset < changing.Count; offset += chunkSize)
        {
            var chunk = changing.Skip(offset).Take(chunkSize).ToList();
            var sql = new StringBuilder();
            sql.Append("UPDATE [").Append(table).Append("] SET [").Append(column).Append("] = CASE [").Append(column).Append(']');
            var args = new List<object>(chunk.Count * 3);
            var index = 0;
            foreach (var from in chunk)
            {
                sql.Append(" WHEN {").Append(index).Append("} THEN {").Append(index + 1).Append('}');
                args.Add(from);
                args.Add(rewrite(from));
                index += 2;
            }

            sql.Append(" END WHERE [").Append(column).Append("] IN (");
            for (var i = 0; i < chunk.Count; i++)
            {
                if (i > 0) sql.Append(", ");
                sql.Append('{').Append(index).Append('}');
                args.Add(chunk[i]);
                index++;
            }

            sql.Append(')');
            await ExecAsync(db, sql.ToString(), args, ct);
        }
    }

    internal static List<string> DistinctChanging(IReadOnlyList<string?> originals, Func<string, string> rewrite) =>
        originals
            .Where(n => !string.IsNullOrWhiteSpace(n) && rewrite(n!) != n)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

    internal static Task<int> ExecAsync(
        TenantDbContext db, string sql, IReadOnlyList<object> args, CancellationToken ct) =>
        db.Database.ExecuteSqlRawAsync(sql, args, ct);

    /// <summary>
    /// Valeurs distinctes d'une colonne. <paramref name="table"/> et <paramref name="column"/> sont
    /// interpolés dans du SQL brut (identifiants entre crochets, non paramétrables) : la paire doit
    /// figurer dans <paramref name="allowList"/>, contrôle effectué avant tout accès à la base.
    /// </summary>
    internal static async Task<List<string?>> DistinctValuesAsync(
        TenantDbContext db,
        string table,
        string column,
        IReadOnlySet<(string Table, string Column)> allowList,
        string callerName,
        CancellationToken ct)
    {
        if (!allowList.Contains((table, column)))
            throw new InvalidOperationException(
                $"{callerName}: table/column pair not allow-listed for DistinctValuesAsync: [{table}].[{column}].");

        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await db.Database.OpenConnectionAsync(ct);

        var safeTable = table.Replace("]", "]]");
        var safeColumn = column.Replace("]", "]]");

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT DISTINCT [{safeColumn}] FROM [{safeTable}] WHERE [{safeColumn}] IS NOT NULL AND [{safeColumn}] <> N''";
        if (db.Database.CurrentTransaction is not null)
            cmd.Transaction = db.Database.CurrentTransaction.GetDbTransaction();

        var list = new List<string?>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(reader.IsDBNull(0) ? null : reader.GetString(0));
        return list;
    }

    /// <summary>
    /// Réaligne <c>Level</c> et <c>AccountClass</c> sur le numéro courant.
    /// </summary>
    /// <remarks>
    /// <c>Level</c> vaut la longueur du numéro par définition du domaine
    /// (<c>ChartOfAccount.Create</c>). Sans ce recalcul, un compte passé de 10 à 7 caractères
    /// resterait annoncé au niveau 10 : tout regroupement arborescent du plan afficherait un nœud
    /// fantôme à une profondeur qui n'existe plus.
    /// </remarks>
    internal static Task RecomputeLevelAndClassAsync(TenantDbContext db, CancellationToken ct) =>
        db.Database.ExecuteSqlRawAsync(
            """
            UPDATE [ChartOfAccounts]
            SET [Level] = LEN([AccountNumber]),
                [AccountClass] = TRY_CONVERT(int, LEFT([AccountNumber], 1))
            WHERE [AccountNumber] IS NOT NULL AND [AccountNumber] LIKE N'[1-7]%'
            """,
            ct);

    /// <summary>
    /// Réaligne les libellés auto-générés sur le numéro de compte courant.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Un sous-compte créé automatiquement porte un libellé de la forme
    /// <c>&lt;libellé parent&gt; — &lt;numéro&gt;</c>. Une renumérotation réécrit la colonne
    /// <c>AccountNumber</c> mais pas le numéro gravé dans le libellé : un compte devenu
    /// <c>4258744456</c> continuait de s'annoncer <c>… — 4218744456</c>, soit un libellé qui
    /// désigne un compte qui n'existe plus.
    /// </para>
    /// <para>
    /// La règle ne touche que les libellés dont le segment terminal est <b>entièrement numérique</b>
    /// et <b>différent</b> du numéro courant : un libellé saisi à la main est intact, et repasser la
    /// migration ne change plus rien.
    /// </para>
    /// </remarks>
    internal static async Task NormalizeAutoGeneratedLabelsAsync(TenantDbContext db, CancellationToken ct)
    {
        const string separator = " \u2014 ";

        var rows = await db.ChartOfAccounts
            .Where(a => a.Label.Contains(separator))
            .ToListAsync(ct);

        var changed = 0;
        foreach (var row in rows)
        {
            var index = row.Label.LastIndexOf(separator, StringComparison.Ordinal);
            if (index < 0)
                continue;

            var tail = row.Label[(index + separator.Length)..].Trim();
            if (tail.Length == 0 || !tail.All(char.IsAsciiDigit))
                continue;

            if (string.Equals(tail, row.AccountNumber, StringComparison.Ordinal))
                continue;

            var head = row.Label[..index].TrimEnd();
            // UpdateLabel refuse les comptes système : ils tiennent leur libellé du catalogue NCT,
            // que l'UPSERT réécrit juste après — on les laisse donc de côté sans les signaler.
            var relabel = row.UpdateLabel(head.Length == 0
                ? row.AccountNumber
                : head + separator + row.AccountNumber);

            if (relabel.IsSuccess)
                changed++;
        }

        if (changed > 0)
            await db.SaveChangesAsync(ct);

        db.ChangeTracker.Clear();
    }

    internal static async Task<bool> TableExistsAsync(TenantDbContext db, string table, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await db.Database.OpenConnectionAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT CASE WHEN OBJECT_ID(@t, N'U') IS NULL THEN 0 ELSE 1 END";
        var p = cmd.CreateParameter();
        p.ParameterName = "@t";
        p.Value = "dbo." + table;
        cmd.Parameters.Add(p);
        if (db.Database.CurrentTransaction is not null)
            cmd.Transaction = db.Database.CurrentTransaction.GetDbTransaction();
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result) == 1;
    }

    internal static async Task<bool> ColumnExistsAsync(TenantDbContext db, string table, string column, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await db.Database.OpenConnectionAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT CASE WHEN COL_LENGTH(@t, @c) IS NULL THEN 0 ELSE 1 END";
        var t = cmd.CreateParameter();
        t.ParameterName = "@t";
        t.Value = table;
        cmd.Parameters.Add(t);
        var c = cmd.CreateParameter();
        c.ParameterName = "@c";
        c.Value = column;
        cmd.Parameters.Add(c);
        if (db.Database.CurrentTransaction is not null)
            cmd.Transaction = db.Database.CurrentTransaction.GetDbTransaction();
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result) == 1;
    }
}
