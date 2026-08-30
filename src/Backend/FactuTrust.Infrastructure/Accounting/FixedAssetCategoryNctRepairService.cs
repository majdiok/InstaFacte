using System.Data;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FactuTrust.Infrastructure.Accounting;

/// <summary>
/// Idempotent corrective startup step, run right after <see cref="Nct01ChartMigrationService.EnsureMigratedAsync"/>.
/// Converges <c>DepreciationRateCategories</c> default accounts — seeded in French-style accounts by the two
/// immutable migrations (<c>AddFixedAssetsModule_Tenant</c>, <c>SeedDecree2008_492RateCategories_Tenant</c>) and
/// never touched by the NCT01 remap because they were seeded after the <c>nct01-v1</c> log row was written — onto
/// the canonical NCT targets. This covers the vehicle sub-accounts (224/2241/2244) and the incorporeal
/// sub-accounts (212/218/281x) that a generic account remap cannot produce, and fixes the VEH_PASS legal rate
/// (20 % / 5 years, not 3.33 %).
/// Idempotent via <c>ChartOfAccountRemapLogs.MapVersion = "fixedassets-nct-v2"</c> (same log table created by
/// <see cref="Nct01ChartMigrationService"/>), and safe under concurrent multi-instance startup via
/// <c>sp_getapplock</c> + a fallback on the unique-index violation of a racing insert.
/// Never touches existing <c>FixedAssets</c> rows: only category defaults (future creations) are corrected.
/// </summary>
public static class FixedAssetCategoryNctRepairService
{
    public const string MapVersion = "fixedassets-nct-v2";

    private sealed record CategoryTarget(string AssetAccount, string DepreciationAccount, string ExpenseAccount);

    /// <summary>
    /// Canonical category → account triplet, per plan T1.3 (v3-immobilisations.md). Covers all 43 category
    /// codes seeded by the two immutable tenant migrations.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, CategoryTarget> CanonicalTargets = BuildCanonicalTargets();

    /// <summary>
    /// Chart of accounts rows that must exist for the canonical targets above but are not part of the
    /// nct01-coa-catalog.json layer (verified missing: 2824, 2812, 2818 — the sibling accounts 2811/2813/2818's
    /// parent 281, and 2821/2822/2823/2828's parent 282, already exist).
    /// </summary>
    private static readonly (string Number, string Label, string Parent)[] MissingAccounts =
    {
        ("2824", "Amortissements du matériel de transport", "282"),
        ("2812", "Amortissements des concessions de marques, brevets, licences", "281"),
        ("2818", "Amortissements des autres immobilisations incorporelles", "281"),
    };

    private static IReadOnlyDictionary<string, CategoryTarget> BuildCanonicalTargets()
    {
        var map = new Dictionary<string, CategoryTarget>(StringComparer.Ordinal);

        void Add(CategoryTarget target, params string[] codes)
        {
            foreach (var code in codes)
                map[code] = target;
        }

        Add(new CategoryTarget("221", "2821", "68112"), "LAND");
        Add(new CategoryTarget("218", "2818", "68111"), "PRELIM");
        Add(new CategoryTarget("212", "2812", "68111"), "PATENT");
        Add(new CategoryTarget("222", "2822", "68112"), "BLDG_PERM", "BLDG_LIGHT", "BLDG_TEMP", "BLDG_BRIDGE", "BLDG_DUR");
        Add(
            new CategoryTarget("223", "2823", "68112"),
            "TECH_INST", "MACH_GEN", "MACH_AGRI", "MAJOR_REP_3", "MAJOR_REP_4", "MAJOR_REP_6", "MAJOR_REP_8",
            "UTIL_GAS", "AGRI_IRR", "AGRI_TRACT", "AGRI_WELL", "AGRI_IRRIG");
        Add(new CategoryTarget("221", "2821", "68112"), "AGRI_TREE", "AGRI_OLIVE_30", "AGRI_OLIVE_20", "AGRI_VINE", "AGRI_CITRUS");
        Add(
            new CategoryTarget("228", "2828", "68112"),
            "IT_EQUIP", "OFF_FURN", "SHELVING", "CONTAINER", "TANK", "HOTEL_KITCH", "HOTEL_DISH", "HOTEL_LINEN",
            "PUB_WORKS", "OTHER");
        Add(new CategoryTarget("2241", "2824", "68112"), "VEH_UTIL");
        Add(new CategoryTarget("2244", "2824", "68112"), "VEH_PASS");
        Add(
            new CategoryTarget("224", "2824", "68112"),
            "ROAD_TRANS", "RAIL_15", "RAIL_20", "RAIL_30", "AIR_TRANS", "SEA_TRANS");

        return map;
    }

    public static async Task EnsureAppliedAsync(TenantDbContext db, CancellationToken cancellationToken = default)
    {
        if (!await TableExistsAsync(db, "DepreciationRateCategories", cancellationToken))
            return;

        // Fast path only when the log table already exists. Table creation itself must happen under
        // the same transaction-owned applock as the repair; otherwise two application instances can
        // both observe a missing table and race on CREATE TABLE before either reaches the lock.
        if (await TableExistsAsync(db, "ChartOfAccountRemapLogs", cancellationToken)
            && await AlreadyAppliedAsync(db, cancellationToken))
            return;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await AcquireApplockAsync(db, cancellationToken);
            await EnsureLogTableAsync(db, cancellationToken);

            // Re-check after acquiring the lock: a concurrent instance may have already applied and
            // committed while we were waiting on sp_getapplock.
            if (await AlreadyAppliedAsync(db, cancellationToken))
            {
                await tx.CommitAsync(cancellationToken);
                return;
            }

            await UpsertMissingAccountsAsync(db, cancellationToken);
            await ConvergeCategoriesAsync(db, cancellationToken);
            await FixVehPassRateAsync(db, cancellationToken);

            try
            {
                await InsertLogAsync(db, cancellationToken);
            }
            catch (Exception ex) when (IsUniqueViolation(ex))
            {
                // Safety net (finding 10): another instance's insert won the race on
                // IX_ChartOfAccountRemapLogs_MapVersion despite the applock — treat as success.
            }

            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static bool IsUniqueViolation(Exception ex) =>
        ex.GetBaseException() is SqlException { Number: 2601 or 2627 };

    private static async Task AcquireApplockAsync(TenantDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            DECLARE @lockResult INT;
            EXEC @lockResult = sp_getapplock
                @Resource = 'fixedassets-nct-v2',
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction';
            IF @lockResult < 0
                THROW 51000, 'FixedAssetCategoryNctRepairService: unable to acquire fixedassets-nct-v2 applock.', 1;
            """,
            ct);
    }

    private static async Task UpsertMissingAccountsAsync(TenantDbContext db, CancellationToken ct)
    {
        var numbers = MissingAccounts.Select(a => a.Number).ToArray();
        var existing = await db.ChartOfAccounts
            .Where(a => numbers.Contains(a.AccountNumber))
            .Select(a => a.AccountNumber)
            .ToListAsync(ct);
        var existingSet = existing.ToHashSet(StringComparer.Ordinal);

        foreach (var (number, label, parent) in MissingAccounts)
        {
            if (existingSet.Contains(number))
                continue;

            var created = ChartOfAccount.Create(
                number,
                label,
                accountClass: 2,
                parentAccountNumber: parent,
                natureType: AccountNatureType.Credit,
                isSystem: true);

            if (created.IsFailure)
            {
                throw new InvalidOperationException(
                    $"FixedAssetCategoryNctRepairService : impossible de créer le compte {number} ({label}) : {created.Error.Description}");
            }

            created.Value.SetAuditInfo(MapVersion);
            db.ChartOfAccounts.Add(created.Value);
        }

        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    private static async Task ConvergeCategoriesAsync(TenantDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        foreach (var (code, target) in CanonicalTargets)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE [DepreciationRateCategories]
                SET [DefaultAssetAccount] = {target.AssetAccount},
                    [DefaultDepreciationAccount] = {target.DepreciationAccount},
                    [DefaultExpenseAccount] = {target.ExpenseAccount},
                    [UpdatedAt] = {now},
                    [UpdatedBy] = {MapVersion}
                WHERE [Code] = {code}
                  AND ([DefaultAssetAccount] <> {target.AssetAccount}
                       OR [DefaultDepreciationAccount] <> {target.DepreciationAccount}
                       OR [DefaultExpenseAccount] <> {target.ExpenseAccount})
                """,
                ct);
        }
    }

    private static async Task FixVehPassRateAsync(TenantDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE [DepreciationRateCategories]
            SET [LegalRatePercent] = 20,
                [UpdatedAt] = {now},
                [UpdatedBy] = {MapVersion}
            WHERE [Code] = N'VEH_PASS' AND [LegalRatePercent] <> 20
            """,
            ct);
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
        var count = await db.DepreciationRateCategories.CountAsync(ct);
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
}
