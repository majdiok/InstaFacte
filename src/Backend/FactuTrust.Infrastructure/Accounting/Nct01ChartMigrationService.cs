using System.Data;
using System.Text;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FactuTrust.Infrastructure.Accounting;

/// <summary>
/// Applique le remap PCG hybride → NCT 01 puis UPSERT le catalogue. Idempotent via
/// <c>ChartOfAccountRemapLogs.MapVersion = nct01-v1</c>.
/// </summary>
public static class Nct01ChartMigrationService
{
    public const string MapVersion = SceChartCatalog.Version;
    private const string TempPrefix = "\u0001";

    public static async Task<bool> IsAppliedAsync(TenantDbContext db, CancellationToken cancellationToken = default)
    {
        if (!await TableExistsAsync(db, "ChartOfAccounts", cancellationToken))
            return false;
        if (!await TableExistsAsync(db, "ChartOfAccountRemapLogs", cancellationToken))
            return false;

        try
        {
            return await AlreadyAppliedAsync(db, cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    public static async Task EnsureMigratedAsync(TenantDbContext db, CancellationToken cancellationToken = default)
    {
        if (!await TableExistsAsync(db, "ChartOfAccounts", cancellationToken))
            return;

        await EnsureLogTableAsync(db, cancellationToken);
        if (await AlreadyAppliedAsync(db, cancellationToken))
            return;

        var remap = SceChartCatalog.LoadRemap();
        var catalog = SceChartCatalog.LoadAccounts();

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await RemapReferencingColumnsAsync(db, remap, cancellationToken);
            await RemapChartRowsAsync(db, remap, cancellationToken);
            await UpsertCatalogAsync(db, catalog, cancellationToken);
            await DeactivateOrphansAsync(db, remap, cancellationToken);
            await InsertLogAsync(db, cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task RemapReferencingColumnsAsync(
        TenantDbContext db, CoaRemapTable remap, CancellationToken ct)
    {
        var targets = new (string Table, string Column)[]
        {
            ("JournalEntryLines", "AccountNumber"),
            ("LetteringGroups", "AccountNumber"),
            ("JournalEntryTemplateLines", "AccountNumber"),
            ("ThirdPartyAccountingProfiles", "CollectiveAccountNumber"),
            ("BankAccounts", "ChartOfAccountNumber"),
            ("BankStatements", "ChartOfAccountNumber"),
            ("FixedAssets", "AssetAccountNumber"),
            ("FixedAssets", "DepreciationAccountNumber"),
            ("FixedAssets", "ExpenseAccountNumber"),
            ("FixedAssets", "CreditAccountNumber"),
            ("FixedAssets", "DisposalTreasuryAccount"),
            ("DepreciationRateCategories", "DefaultAssetAccount"),
            ("DepreciationRateCategories", "DefaultDepreciationAccount"),
            ("DepreciationRateCategories", "DefaultExpenseAccount"),
            ("SupplierInvoiceLines", "AssetAccountNumber"),
            ("Loans", "LoanAccountNumber"),
            ("Loans", "InterestAccountNumber"),
            ("Loans", "BankAccountNumber"),
            ("Payslips", "EmployeeAuxiliaryAccount"),
            ("PayrollPaymentLines", "EmployeeAuxiliaryAccount"),
            ("AccountingAnomalies", "AccountRef"),
            ("AccountingAnomalyLines", "AccountNumber"),
        };

        foreach (var (table, column) in targets)
        {
            if (!await TableExistsAsync(db, table, ct) || !await ColumnExistsAsync(db, table, column, ct))
                continue;

            var originals = await DistinctValuesAsync(db, table, column, ct);
            await CaseRewriteAsync(db, table, column, originals, remap.Rewrite, ct);
        }

        if (await TableExistsAsync(db, "BudgetPosts", ct)
            && await ColumnExistsAsync(db, "BudgetPosts", "AccountPrefixes", ct))
        {
            var prefixes = await DistinctValuesAsync(db, "BudgetPosts", "AccountPrefixes", ct);
            await CaseRewriteAsync(db, "BudgetPosts", "AccountPrefixes", prefixes, RewritePrefixList(remap), ct);
        }
    }

    private static Func<string, string> RewritePrefixList(CoaRemapTable remap) => value =>
    {
        var parts = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(';', parts.Select(remap.Rewrite));
    };

    private static async Task RemapChartRowsAsync(
        TenantDbContext db, CoaRemapTable remap, CancellationToken ct)
    {
        var numbers = await db.ChartOfAccounts.AsNoTracking().Select(a => a.AccountNumber).ToListAsync(ct);
        var parents = await db.ChartOfAccounts.AsNoTracking().Select(a => a.ParentAccountNumber).ToListAsync(ct);
        var affectations = await db.ChartOfAccounts.AsNoTracking().Select(a => a.AffectationAccountNumber).ToListAsync(ct);

        await DeleteMergeSourceRowsAsync(db, numbers, remap.Rewrite, ct);

        var remaining = await db.ChartOfAccounts.AsNoTracking().Select(a => a.AccountNumber).ToListAsync(ct);
        await TwoPhaseUniqueRewriteAsync(db, "ChartOfAccounts", "AccountNumber", remaining, remap.Rewrite, ct);
        await CaseRewriteAsync(db, "ChartOfAccounts", "ParentAccountNumber", parents, remap.Rewrite, ct);
        await CaseRewriteAsync(db, "ChartOfAccounts", "AffectationAccountNumber", affectations, remap.Rewrite, ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE [ChartOfAccounts]
            SET [Level] = LEN([AccountNumber]),
                [AccountClass] = TRY_CONVERT(int, LEFT([AccountNumber], 1))
            WHERE [AccountNumber] IS NOT NULL AND [AccountNumber] LIKE N'[1-7]%'
            """,
            ct);

        db.ChangeTracker.Clear();

        // Le remap réécrit les numéros ; les libellés auto-générés qui citent l'ancien numéro
        // resteraient sinon figés dessus (« … — 421 — 4218744456 » sur un compte 4258744456).
        await NormalizeAutoGeneratedLabelsAsync(db, ct);

        var duplicates = await db.ChartOfAccounts
            .GroupBy(a => a.AccountNumber)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToListAsync(ct);

        foreach (var number in duplicates)
        {
            var rows = await db.ChartOfAccounts.Where(a => a.AccountNumber == number).OrderBy(a => a.CreatedAt).ToListAsync(ct);
            foreach (var extra in rows.Skip(1))
                db.ChartOfAccounts.Remove(extra);
        }

        if (duplicates.Count > 0)
            await db.SaveChangesAsync(ct);

        db.ChangeTracker.Clear();
    }

    /// <summary>
    /// Réaligne les libellés auto-générés sur le numéro de compte courant.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Un sous-compte créé automatiquement porte un libellé de la forme
    /// <c>« &lt;libellé parent&gt; — &lt;numéro&gt; »</c>. Le remap réécrit le numéro de la colonne
    /// <c>AccountNumber</c> mais pas celui gravé dans le libellé : un compte devenu
    /// <c>4258744456</c> continuait de s'annoncer <c>« … — 4218744456 »</c>, soit un libellé qui
    /// désigne un compte qui n'existe plus.
    /// </para>
    /// <para>
    /// La règle ne touche que les libellés dont le segment terminal est <b>entièrement numérique</b>
    /// et <b>différent</b> du numéro courant : un libellé saisi à la main est intact, et repasser la
    /// migration ne change plus rien.
    /// </para>
    /// </remarks>
    private static async Task NormalizeAutoGeneratedLabelsAsync(TenantDbContext db, CancellationToken ct)
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

    private static async Task DeleteMergeSourceRowsAsync(
        TenantDbContext db,
        IReadOnlyList<string?> originals,
        Func<string, string> rewrite,
        CancellationToken ct)
    {
        var numbers = originals
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var group in numbers.GroupBy(rewrite, StringComparer.Ordinal))
        {
            var members = group.ToList();
            if (members.Count <= 1)
                continue;

            var keep = members.Contains(group.Key, StringComparer.Ordinal)
                ? group.Key
                : members.OrderBy(m => m.Length).ThenBy(m => m, StringComparer.Ordinal).First();

            foreach (var extra in members.Where(m => !string.Equals(m, keep, StringComparison.Ordinal)))
            {
                await ExecAsync(
                    db,
                    "DELETE FROM [ChartOfAccounts] WHERE [AccountNumber] = {0}",
                    [extra],
                    ct);
            }
        }
    }

    private static async Task UpsertCatalogAsync(
        TenantDbContext db, IReadOnlyList<SceChartAccount> catalog, CancellationToken ct)
    {
        var existingNumbers = (await db.ChartOfAccounts.AsNoTracking()
                .Select(a => a.AccountNumber)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var now = DateTime.UtcNow;
        foreach (var account in catalog.OrderBy(a => a.Number.Length).ThenBy(a => a.Number, StringComparer.Ordinal))
        {
            if (existingNumbers.Contains(account.Number))
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE [ChartOfAccounts]
                    SET [Label] = {account.Label},
                        [AccountClass] = {account.AccountClass},
                        [ParentAccountNumber] = {account.Parent},
                        [NatureType] = {account.NatureType},
                        [Level] = {account.Level},
                        [IsSystem] = 1,
                        [IsActive] = 1,
                        [UpdatedAt] = {now},
                        [UpdatedBy] = {MapVersion}
                    WHERE [AccountNumber] = {account.Number}
                    """,
                    ct);
                continue;
            }

            var created = ChartOfAccount.Create(
                account.Number,
                account.Label,
                account.AccountClass,
                account.Parent,
                (AccountNatureType)account.NatureType,
                isSystem: true);

            if (created.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Catalogue NCT : impossible de créer {account.Number} ({account.Label}) : {created.Error.Description}");
            }

            created.Value.SetAuditInfo(MapVersion);
            db.ChartOfAccounts.Add(created.Value);
            existingNumbers.Add(account.Number);
        }

        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    private static async Task DeactivateOrphansAsync(
        TenantDbContext db, CoaRemapTable remap, CancellationToken ct)
    {
        var hasJournal = await TableExistsAsync(db, "JournalEntryLines", ct);
        foreach (var entry in remap.DeactivateEntries)
        {
            if (hasJournal)
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE [ChartOfAccounts]
                    SET [IsActive] = 0, [IsSystem] = 0, [UpdatedAt] = {DateTime.UtcNow}, [UpdatedBy] = {MapVersion}
                    WHERE [AccountNumber] = {entry.From}
                      AND NOT EXISTS (
                          SELECT 1 FROM [JournalEntryLines] l WHERE l.[AccountNumber] = {entry.From})
                    """,
                    ct);
            }
            else
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE [ChartOfAccounts]
                    SET [IsActive] = 0, [IsSystem] = 0, [UpdatedAt] = {DateTime.UtcNow}, [UpdatedBy] = {MapVersion}
                    WHERE [AccountNumber] = {entry.From}
                    """,
                    ct);
            }
        }
    }

    private static async Task TwoPhaseUniqueRewriteAsync(
        TenantDbContext db,
        string table,
        string column,
        IReadOnlyList<string?> originals,
        Func<string, string> rewrite,
        CancellationToken ct)
    {
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

    private static async Task CaseRewriteAsync(
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

    private static List<string> DistinctChanging(IReadOnlyList<string?> originals, Func<string, string> rewrite) =>
        originals
            .Where(n => !string.IsNullOrWhiteSpace(n) && rewrite(n!) != n)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static Task<int> ExecAsync(
        TenantDbContext db, string sql, IReadOnlyList<object> args, CancellationToken ct) =>
        db.Database.ExecuteSqlRawAsync(sql, args, ct);

    /// <summary>
    /// Allow-list of (table, column) pairs that <see cref="DistinctValuesAsync"/> is permitted to read.
    /// Both values are interpolated into raw SQL below (bracket-quoted identifiers, no query params for
    /// identifiers); restricting them to constants known at call sites in this file prevents SQL injection
    /// even if a future caller accidentally threads untrusted input through.
    /// </summary>
    internal static readonly IReadOnlySet<(string Table, string Column)> DistinctValuesAllowList =
        new HashSet<(string Table, string Column)>
        {
            ("JournalEntryLines", "AccountNumber"),
            ("LetteringGroups", "AccountNumber"),
            ("JournalEntryTemplateLines", "AccountNumber"),
            ("ThirdPartyAccountingProfiles", "CollectiveAccountNumber"),
            ("BankAccounts", "ChartOfAccountNumber"),
            ("BankStatements", "ChartOfAccountNumber"),
            ("FixedAssets", "AssetAccountNumber"),
            ("FixedAssets", "DepreciationAccountNumber"),
            ("FixedAssets", "ExpenseAccountNumber"),
            ("FixedAssets", "CreditAccountNumber"),
            ("FixedAssets", "DisposalTreasuryAccount"),
            ("DepreciationRateCategories", "DefaultAssetAccount"),
            ("DepreciationRateCategories", "DefaultDepreciationAccount"),
            ("DepreciationRateCategories", "DefaultExpenseAccount"),
            ("SupplierInvoiceLines", "AssetAccountNumber"),
            ("Loans", "LoanAccountNumber"),
            ("Loans", "InterestAccountNumber"),
            ("Loans", "BankAccountNumber"),
            ("Payslips", "EmployeeAuxiliaryAccount"),
            ("PayrollPaymentLines", "EmployeeAuxiliaryAccount"),
            ("AccountingAnomalies", "AccountRef"),
            ("AccountingAnomalyLines", "AccountNumber"),
            ("BudgetPosts", "AccountPrefixes"),
        };

    internal static async Task<List<string?>> DistinctValuesAsync(
        TenantDbContext db, string table, string column, CancellationToken ct)
    {
        if (!DistinctValuesAllowList.Contains((table, column)))
            throw new InvalidOperationException(
                $"Nct01ChartMigrationService: table/column pair not allow-listed for DistinctValuesAsync: [{table}].[{column}].");

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

    private static async Task EnsureLogTableAsync(TenantDbContext db, CancellationToken ct) =>
        await db.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'dbo.ChartOfAccountRemapLogs', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ChartOfAccountRemapLogs] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [MapVersion] nvarchar(32) NOT NULL,
                    [AppliedAt] datetime2 NOT NULL,
                    [AccountCount] int NOT NULL
                );
                CREATE UNIQUE INDEX [IX_ChartOfAccountRemapLogs_MapVersion]
                    ON [dbo].[ChartOfAccountRemapLogs] ([MapVersion]);
            END
            """,
            ct);

    private static async Task<bool> AlreadyAppliedAsync(TenantDbContext db, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await db.Database.OpenConnectionAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM [dbo].[ChartOfAccountRemapLogs] WHERE [MapVersion] = @v";
        var p = cmd.CreateParameter();
        p.ParameterName = "@v";
        p.Value = MapVersion;
        cmd.Parameters.Add(p);
        if (db.Database.CurrentTransaction is not null)
            cmd.Transaction = db.Database.CurrentTransaction.GetDbTransaction();
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result) > 0;
    }

    private static async Task InsertLogAsync(TenantDbContext db, CancellationToken ct)
    {
        var count = await db.ChartOfAccounts.CountAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO [dbo].[ChartOfAccountRemapLogs] ([Id], [MapVersion], [AppliedAt], [AccountCount])
            VALUES ({Guid.NewGuid()}, {MapVersion}, {DateTime.UtcNow}, {count})
            """,
            ct);
    }

    private static async Task<bool> TableExistsAsync(TenantDbContext db, string table, CancellationToken ct)
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

    private static async Task<bool> ColumnExistsAsync(TenantDbContext db, string table, string column, CancellationToken ct)
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
