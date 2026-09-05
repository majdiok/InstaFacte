using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Accounting;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Accounting;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Accounting;

/// <summary>
/// Renumérotation des comptes de plus de 8 chiffres.
/// SQL Server / LocalDB requis : le service s'exécute en SQL brut, dans une transaction.
/// </summary>
/// <remarks>
/// Les comptes hors norme sont semés en deux temps — création d'un compte conforme par la fabrique
/// du domaine, puis réécriture du numéro en SQL brut. C'est volontaire :
/// <c>ChartOfAccount.Create</c> refuse désormais un numéro trop long, si bien qu'un tel compte ne
/// peut plus naître que d'une base antérieure au correctif, ce que le montage reproduit fidèlement.
/// </remarks>
public sealed class ChartAccountDigitCompactionServiceTests : IDisposable
{
    private readonly SqlTestDatabase _sqlDb = new(nameof(ChartAccountDigitCompactionServiceTests));
    private readonly string? _connectionString;
    private readonly bool _canRun;

    public ChartAccountDigitCompactionServiceTests()
    {
        _connectionString = _sqlDb.ConnectionString;
        _canRun = _sqlDb.CanRun;
    }

    [Fact]
    public async Task Compacts_OverlongAccounts_PreservesBalances_AndSecondRunIsNoOp()
    {
        if (!_canRun) return;

        await using (var db = CreateContext())
        {
            SeedCollectiveAndEntry(db);
            await db.SaveChangesAsync();
            await MakeOverlongAsync(db, "4250009", "4259655554");
            await MakeOverlongAsync(db, "4250008", "4258744456");
        }

        decimal debitBefore, creditBefore;
        await using (var db = CreateContext())
            (debitBefore, creditBefore) = await TrialBalanceAsync(db);

        ChartAccountCompactionResult result;
        await using (var db = CreateContext())
            result = await ChartAccountDigitCompactionService.EnsureCompactedAsync(db, appliedBy: "test");

        Assert.True(result.Applied);
        Assert.Equal(2, result.MappingCount);

        await using (var db = CreateContext())
        {
            var numbers = await db.ChartOfAccounts.AsNoTracking()
                .Select(a => a.AccountNumber).ToListAsync();

            // C1 — plus aucun compte hors norme.
            Assert.All(numbers, n =>
                Assert.True(AccountNumberRules.DigitCount(n) <= AccountNumberRules.MaxDigits, n));

            // Ordre ordinal : 4258744456 passe avant 4259655554, et 4250001 est déjà pris.
            Assert.Contains("4250002", numbers);
            Assert.Contains("4250003", numbers);
            Assert.DoesNotContain("4259655554", numbers);
            Assert.DoesNotContain("4258744456", numbers);

            // C2 — la balance générale n'a pas bougé d'un millime.
            var (debitAfter, creditAfter) = await TrialBalanceAsync(db);
            Assert.Equal(debitBefore, debitAfter);
            Assert.Equal(creditBefore, creditAfter);

            // C3 — les montants ont suivi le compte, ligne pour ligne.
            var lines = await db.JournalEntryLines.AsNoTracking().ToListAsync();
            Assert.Equal(1012.137m, SumCredit(lines, "4250003"));
            Assert.Equal(500m, SumCredit(lines, "4250002"));
            Assert.Empty(lines.Where(l => l.AccountNumber == "4259655554"));

            // Level recalculé : un compte passé de 10 à 7 caractères ne reste pas au niveau 10.
            var moved = await db.ChartOfAccounts.AsNoTracking().SingleAsync(a => a.AccountNumber == "4250003");
            Assert.Equal(7, moved.Level);
            Assert.Equal(4, moved.AccountClass);
            Assert.Equal("sami samou", moved.Label);
            Assert.Equal("coa-digit-compaction-v1", moved.UpdatedBy);

            Assert.Equal(2, await CompactionLogCountAsync(db));
            Assert.Equal("4250003", await CompactedTargetAsync(db, "4259655554"));
        }

        // Idempotence par la donnée : plus rien à compacter, donc aucune écriture.
        await using (var db = CreateContext())
        {
            var second = await ChartAccountDigitCompactionService.EnsureCompactedAsync(db, appliedBy: "test");
            Assert.False(second.Applied);
            Assert.Equal(0, second.MappingCount);
        }

        await using (var db = CreateContext())
            Assert.Equal(2, await CompactionLogCountAsync(db));
    }

    /// <summary>
    /// Le lettrage est mono-compte par construction : les deux côtés doivent bouger ensemble, sans
    /// quoi le groupe désignerait un compte que ses lignes ne portent plus.
    /// </summary>
    [Fact]
    public async Task Moves_Both_Sides_Of_A_Lettering_Group()
    {
        if (!_canRun) return;

        await using (var db = CreateContext())
        {
            SeedCollectiveAndEntry(db);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            // Le règlement : débit du même compte auxiliaire, apparié à la dette par lettrage.
            var period = await db.AccountingPeriods.AsNoTracking().SingleAsync();
            var payment = JournalEntry.Create(
                1, "JB", new DateTime(2026, 9, 30), "Règlement paie 09/2026",
                period.Id, true, "PayrollPayment", Guid.NewGuid(),
                new List<JournalLineInput>
                {
                    new("4250009", "Règlement — sami samou", 1012.137m, 0m, null, ThirdPartyKind.None),
                    new("5321", "Banque", 0m, 1012.137m, null, ThirdPartyKind.None)
                }).Value;
            payment.SetAuditInfo("compaction-test");
            db.ChartOfAccounts.Add(Acc("53", "Banques", null, AccountNatureType.Debit));
            db.ChartOfAccounts.Add(Acc("5321", "Banque en dinars", "53", AccountNatureType.Debit));
            db.JournalEntries.Add(payment);
            await db.SaveChangesAsync();

            var lineIds = await db.JournalEntryLines.AsNoTracking()
                .Where(l => l.AccountNumber == "4250009")
                .Select(l => l.Id)
                .ToListAsync();
            Assert.Equal(2, lineIds.Count);

            var group = LetteringGroup.Create(
                "L001", "4250009", Money.Create(1012.137m, "TND"), lineIds).Value;
            group.SetAuditInfo("compaction-test");
            db.LetteringGroups.Add(group);
            await db.SaveChangesAsync();

            await MakeOverlongAsync(db, "4250009", "4259655554");
            await ExecAsync(db,
                "UPDATE [LetteringGroups] SET [AccountNumber] = N'4259655554' WHERE [AccountNumber] = N'4250009'");
        }

        await using (var db = CreateContext())
            await ChartAccountDigitCompactionService.EnsureCompactedAsync(db, appliedBy: "test");

        await using (var db = CreateContext())
        {
            var group = await db.LetteringGroups.AsNoTracking().SingleAsync();
            Assert.Equal("4250002", group.AccountNumber);

            // Le groupe reste mono-compte : ses deux lignes portent le même numéro, le sien.
            var memberAccounts = await db.LetteringGroupMembers.AsNoTracking()
                .Join(db.JournalEntryLines, m => m.JournalEntryLineId, l => l.Id, (m, l) => l.AccountNumber)
                .Distinct()
                .ToListAsync();

            Assert.Single(memberAccounts);
            Assert.Equal(group.AccountNumber, memberAccounts[0]);
        }
    }

    /// <summary>
    /// Un compte hors norme qui a des sous-comptes ne peut pas bouger sans casser l'invariant
    /// « le numéro commence par le compte parent ». On refuse tout plutôt que de re-parenter.
    /// </summary>
    [Fact]
    public async Task Aborts_When_An_Overlong_Account_Has_Children()
    {
        if (!_canRun) return;

        await using (var db = CreateContext())
        {
            SeedCollectiveAndEntry(db);
            await db.SaveChangesAsync();
            await MakeOverlongAsync(db, "4250009", "4259655554");
            await MakeOverlongAsync(db, "4250008", "42596555541");
        }

        await using (var db = CreateContext())
        {
            var plan = await ChartAccountDigitCompactionService.BuildPlanAsync(db);
            Assert.NotEmpty(plan.BlockingIssues);
            Assert.Contains(plan.BlockingIssues, i => i.Contains("sous-compte"));
            Assert.False(plan.CanApply);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => ChartAccountDigitCompactionService.EnsureCompactedAsync(db, appliedBy: "test"));
        }

        // Rien n'a bougé : la transaction a été annulée en entier.
        await using (var db = CreateContext())
        {
            var numbers = await db.ChartOfAccounts.AsNoTracking().Select(a => a.AccountNumber).ToListAsync();
            Assert.Contains("4259655554", numbers);
            Assert.Contains("42596555541", numbers);
        }
    }

    /// <summary>
    /// Le pré-filtre SQL porte sur la longueur, la décision sur le nombre de chiffres : un compte
    /// pointé de 4 chiffres ne doit pas être touché, même s'il fait plus de 8 caractères.
    /// </summary>
    [Fact]
    public async Task Leaves_CompliantAccountsAlone_IncludingDottedOnes()
    {
        if (!_canRun) return;

        await using (var db = CreateContext())
        {
            SeedCollectiveAndEntry(db);
            db.ChartOfAccounts.Add(Acc("421", "Personnel - avances", "42", AccountNatureType.Debit));
            db.ChartOfAccounts.Add(Acc("421.1", "Prêts salariés", "421", AccountNatureType.Debit));
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            var plan = await ChartAccountDigitCompactionService.BuildPlanAsync(db);
            Assert.True(plan.IsNoOp);

            var result = await ChartAccountDigitCompactionService.EnsureCompactedAsync(db, appliedBy: "test");
            Assert.False(result.Applied);
        }

        await using (var db = CreateContext())
        {
            var numbers = await db.ChartOfAccounts.AsNoTracking().Select(a => a.AccountNumber).ToListAsync();
            Assert.Contains("421.1", numbers);
            Assert.Contains("4250001", numbers);
        }
    }

    /// <summary>
    /// L'empreinte du plan est ce que l'opérateur relit avant d'appliquer : elle doit être stable
    /// entre deux constructions, et une empreinte périmée doit annuler l'opération.
    /// </summary>
    [Fact]
    public async Task Plan_IsDeterministic_AndStalePlanHashIsRefused()
    {
        if (!_canRun) return;

        await using (var db = CreateContext())
        {
            SeedCollectiveAndEntry(db);
            await db.SaveChangesAsync();
            await MakeOverlongAsync(db, "4250009", "4259655554");
        }

        await using (var db = CreateContext())
        {
            var first = await ChartAccountDigitCompactionService.BuildPlanAsync(db);
            var second = await ChartAccountDigitCompactionService.BuildPlanAsync(db);

            Assert.Equal(first.PlanHash, second.PlanHash);
            Assert.Single(first.Mappings);
            Assert.Equal("4259655554", first.Mappings[0].From);
            Assert.Equal("425", first.Mappings[0].Root);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => ChartAccountDigitCompactionService.EnsureCompactedAsync(
                    db, expectedPlanHash: "perime", appliedBy: "test"));
        }

        await using (var db = CreateContext())
        {
            var plan = await ChartAccountDigitCompactionService.BuildPlanAsync(db);
            var applied = await ChartAccountDigitCompactionService.EnsureCompactedAsync(
                db, expectedPlanHash: plan.PlanHash, appliedBy: "test");
            Assert.True(applied.Applied);
        }
    }

    // ── Montage ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Collectif 425, deux auxiliaires conformes (dont 4250001 déjà pris, pour que la renumérotation
    /// doive l'éviter) et une OD de paie qui les mouvemente.
    /// </summary>
    private static void SeedCollectiveAndEntry(TenantDbContext db)
    {
        db.ChartOfAccounts.AddRange(
            Acc("42", "Personnel et comptes rattachés", null, AccountNatureType.Credit),
            Acc("425", "Personnel - rémunérations dues", "42", AccountNatureType.Credit),
            Acc("4250001", "Karim Soumi", "425", AccountNatureType.Credit),
            Acc("4250008", "nadia ben ali", "425", AccountNatureType.Credit),
            Acc("4250009", "sami samou", "425", AccountNatureType.Credit),
            Acc("64", "Charges de personnel", null, AccountNatureType.Debit),
            Acc("640", "Salaires et compléments", "64", AccountNatureType.Debit));

        var period = AccountingPeriod.Create(2026, 9, new DateTime(2026, 9, 1), new DateTime(2026, 9, 30));
        db.AccountingPeriods.Add(period);

        var lines = new List<JournalLineInput>
        {
            new("640", "Paie 09/2026", 1512.137m, 0m, null, ThirdPartyKind.None),
            new("4250009", "Paie 09/2026 — sami samou", 0m, 1012.137m, null, ThirdPartyKind.None),
            new("4250008", "Paie 09/2026 — nadia ben ali", 0m, 500m, null, ThirdPartyKind.None)
        };

        var entry = JournalEntry.Create(
            9, "JOD", new DateTime(2026, 9, 30), "Paie 09/2026",
            period.Id, true, "PayrollRun", Guid.NewGuid(), lines).Value;
        entry.SetAuditInfo("compaction-test");
        db.JournalEntries.Add(entry);
    }

    /// <summary>
    /// Réécrit un numéro conforme en numéro hors norme, comme une base antérieure au correctif en
    /// contient. Passe par du SQL brut : la fabrique du domaine refuse désormais un tel numéro.
    /// </summary>
    private static async Task MakeOverlongAsync(TenantDbContext db, string from, string to)
    {
        await ExecAsync(db,
            $"UPDATE [ChartOfAccounts] SET [AccountNumber] = N'{to}', [Level] = {to.Length} "
            + $"WHERE [AccountNumber] = N'{from}'");
        await ExecAsync(db,
            $"UPDATE [JournalEntryLines] SET [AccountNumber] = N'{to}' WHERE [AccountNumber] = N'{from}'");
    }

    private static ChartOfAccount Acc(string number, string label, string? parent, AccountNatureType nature)
    {
        var created = ChartOfAccount.Create(
            number, label, number[0] - '0', parent, nature,
            isSystem: false,
            accountType: parent == "425" ? AccountType.Other : AccountType.General,
            isAuxiliary: parent == "425",
            affectationAccountNumber: parent == "425" ? "425" : null);

        if (created.IsFailure)
            throw new InvalidOperationException($"{number}: {created.Error.Description}");

        created.Value.SetAuditInfo("compaction-test");
        return created.Value;
    }

    private static decimal SumCredit(IEnumerable<JournalEntryLine> lines, string account) =>
        lines.Where(l => l.AccountNumber == account).Sum(l => l.CreditAmount.Amount);

    private static async Task<(decimal Debit, decimal Credit)> TrialBalanceAsync(TenantDbContext db)
    {
        var lines = await db.JournalEntryLines.AsNoTracking().ToListAsync();
        return (lines.Sum(l => l.DebitAmount.Amount), lines.Sum(l => l.CreditAmount.Amount));
    }

    private static Task ExecAsync(TenantDbContext db, string sql) =>
        db.Database.ExecuteSqlRawAsync(sql);

    private static async Task<int> CompactionLogCountAsync(TenantDbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await db.Database.OpenConnectionAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM [dbo].[ChartOfAccountCompactionLogs]";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task<string?> CompactedTargetAsync(TenantDbContext db, string from)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await db.Database.OpenConnectionAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT [ToAccountNumber] FROM [dbo].[ChartOfAccountCompactionLogs] "
            + "WHERE [FromAccountNumber] = @from";
        var p = cmd.CreateParameter();
        p.ParameterName = "@from";
        p.Value = from;
        cmd.Parameters.Add(p);
        return (await cmd.ExecuteScalarAsync()) as string;
    }

    private TenantDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(_connectionString)
            .Options);

    public void Dispose() => _sqlDb.Dispose();
}
