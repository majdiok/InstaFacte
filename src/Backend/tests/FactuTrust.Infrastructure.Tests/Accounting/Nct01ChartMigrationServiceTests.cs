using System.Data;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Accounting;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Accounting;

/// <summary>
/// Migration PCG hybride → NCT 01 sur un mini-jeu de seed historique.
/// SQL Server / LocalDB requis : le service exécute du SQL brut (tampon d'unicité, table de log).
/// </summary>
public sealed class Nct01ChartMigrationServiceTests : IDisposable
{
    private readonly string? _connectionString;
    private readonly bool _canRun;

    public Nct01ChartMigrationServiceTests()
    {
        _connectionString = Environment.GetEnvironmentVariable("FACTUTRUST_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            var dbName = $"FactuTrust_Nct01Coa_{Guid.NewGuid():N}";
            _connectionString =
                $"Server=(localdb)\\mssqllocaldb;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";
        }

        _canRun = CanConnectAndCreate(_connectionString);
    }

    [Fact]
    public async Task EnsureMigrated_OldSeedMiniSet_FollowsBalances_AndSecondRunIsNoOp()
    {
        if (!_canRun) return;

        await using (var db = CreateContext())
        {
            SeedOldChartAndJournal(db);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
            await Nct01ChartMigrationService.EnsureMigratedAsync(db);

        await using (var db = CreateContext())
        {
            var lines = await db.JournalEntryLines.AsNoTracking().ToListAsync();
            Assert.Equal(5000m, SumDebit(lines, "221"));
            Assert.Equal(300m, SumDebit(lines, "413"));
            Assert.Equal(150m, SumDebit(lines, "421"));
            Assert.Equal(100m, SumCredit(lines, "117"));
            Assert.Equal(50m, SumCredit(lines, "43652"));
            Assert.Equal(2000m, SumCredit(lines, "425"));
            Assert.Equal(190m, SumCredit(lines, "436711"));
            Assert.DoesNotContain(lines, l => l.AccountNumber == "4477");
            Assert.DoesNotContain(lines, l => l.AccountNumber == "211");
            Assert.DoesNotContain(lines, l => l.AccountNumber == "412");

            var fodec = await db.ChartOfAccounts.AsNoTracking()
                .SingleAsync(a => a.AccountNumber == "43652");
            Assert.True(fodec.IsSystem);
            Assert.True(fodec.IsActive);
            Assert.Equal("4365", fodec.ParentAccountNumber);

            var numbers = await db.ChartOfAccounts.AsNoTracking()
                .Select(a => a.AccountNumber)
                .ToListAsync();
            Assert.DoesNotContain("4477", numbers);
            Assert.Contains("425", numbers);
            Assert.Contains("421", numbers);
            Assert.True(numbers.Count >= 600, $"Catalogue incomplet après UPSERT : {numbers.Count}");
            Assert.Equal(1, await LogCountAsync(db));
        }

        await using (var db = CreateContext())
            await Nct01ChartMigrationService.EnsureMigratedAsync(db);

        await using (var db = CreateContext())
        {
            Assert.Equal(1, await LogCountAsync(db));
            var lines = await db.JournalEntryLines.AsNoTracking().ToListAsync();
            Assert.Equal(2000m, SumCredit(lines, "425"));
            Assert.Equal(150m, SumDebit(lines, "421"));
            Assert.Equal(50m, SumCredit(lines, "43652"));
        }
    }

    private static void SeedOldChartAndJournal(TenantDbContext db)
    {
        db.ChartOfAccounts.AddRange(
            Acc("10", "Capital", null, AccountNatureType.Credit),
            Acc("101", "Capital social", "10", AccountNatureType.Credit),
            Acc("105", "Primes d'émission", "10", AccountNatureType.Credit),
            Acc("21", "Immobilisations corporelles", null, AccountNatureType.Debit),
            Acc("211", "Terrains", "21", AccountNatureType.Debit),
            Acc("41", "Clients", null, AccountNatureType.Debit),
            Acc("412", "Clients - effets à recevoir", "41", AccountNatureType.Debit),
            Acc("42", "Personnel", null, AccountNatureType.Credit),
            Acc("421", "Personnel - rémunérations dues", "42", AccountNatureType.Credit),
            Acc("425", "Personnel - avances", "42", AccountNatureType.Debit),
            Acc("44", "État", null, AccountNatureType.Credit),
            Acc("447", "État - autres", "44", AccountNatureType.Credit),
            Acc("4477", "FODEC à payer", "447", AccountNatureType.Credit),
            Acc("43671", "TVA collectée", null, AccountNatureType.Credit),
            Acc("436711", "TVA collectée sur débits", "43671", AccountNatureType.Credit));

        var period = AccountingPeriod.Create(2025, 12, new DateTime(2025, 12, 1), new DateTime(2025, 12, 31));
        db.AccountingPeriods.Add(period);

        var lines = new List<JournalLineInput>
        {
            new("211", "Terrains", 5000m, 0m, null, ThirdPartyKind.None),
            new("412", "Effets clients", 300m, 0m, null, ThirdPartyKind.None),
            new("425", "Avances personnel", 150m, 0m, null, ThirdPartyKind.None),
            new("105", "Primes", 0m, 100m, null, ThirdPartyKind.None),
            new("4477", "FODEC", 0m, 50m, null, ThirdPartyKind.None),
            new("421", "Net à payer", 0m, 2000m, null, ThirdPartyKind.None),
            new("436711", "TVA collectée", 0m, 190m, null, ThirdPartyKind.None),
            new("101", "Capital (équilibrage)", 0m, 3110m, null, ThirdPartyKind.None)
        };

        var entry = JournalEntry.Create(
            1, "JOD", new DateTime(2025, 12, 31), "Mini-jeu PCG historique",
            period.Id, true, "Test", Guid.NewGuid(), lines).Value;
        entry.SetAuditInfo("nct01-test");
        db.JournalEntries.Add(entry);
    }

    private static ChartOfAccount Acc(
        string number, string label, string? parent, AccountNatureType nature)
    {
        var created = ChartOfAccount.Create(
            number, label, number[0] - '0', parent, nature, isSystem: true);
        if (created.IsFailure)
            throw new InvalidOperationException($"{number}: {created.Error.Description}");
        created.Value.SetAuditInfo("nct01-test");
        return created.Value;
    }

    private static decimal SumDebit(IEnumerable<JournalEntryLine> lines, string account) =>
        lines.Where(l => l.AccountNumber == account).Sum(l => l.DebitAmount.Amount);

    private static decimal SumCredit(IEnumerable<JournalEntryLine> lines, string account) =>
        lines.Where(l => l.AccountNumber == account).Sum(l => l.CreditAmount.Amount);

    private static async Task<int> LogCountAsync(TenantDbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await db.Database.OpenConnectionAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM [dbo].[ChartOfAccountRemapLogs] WHERE [MapVersion] = N'nct01-v1'";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private TenantDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(_connectionString)
            .Options);

    private static bool CanConnectAndCreate(string connectionString)
    {
        try
        {
            using var context = new TenantDbContext(
                new DbContextOptionsBuilder<TenantDbContext>().UseSqlServer(connectionString).Options);
            context.Database.EnsureCreated();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (!_canRun || string.IsNullOrWhiteSpace(_connectionString))
            return;

        try
        {
            using var context = CreateContext();
            context.Database.EnsureDeleted();
        }
        catch
        {
            // Nettoyage best-effort.
        }
    }
}
