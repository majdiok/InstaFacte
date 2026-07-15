using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// État budgétaire (budget vs réalisé) : sens charges/produits, affectation par préfixe le plus
/// long, lignes « Hors postes », politique brouillard, cumuls partiels, écart/% et exports.
/// </summary>
public sealed class BudgetReportingTests
{
    private readonly TestTenantDbContextFactory _factory;

    public BudgetReportingTests()
    {
        _factory = new TestTenantDbContextFactory($"BudgetReportDb_{Guid.NewGuid()}");
    }

    private sealed class TestTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly string _databaseName;
        public TestTenantDbContextFactory(string databaseName) => _databaseName = databaseName;
        public TenantDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(databaseName: _databaseName)
                .Options;
            return new TenantDbContext(options);
        }
    }

    private AccountingReportingService BuildService(bool includeDrafts = true, bool enabled = true) =>
        new(_factory, Options.Create(new AccountingSettings
        {
            BudgetingEnabled = enabled,
            IncludeBrouillardInReports = includeDrafts
        }));

    private Guid SeedPost(string code, BudgetPostKind kind, string prefixes, int order = 10)
    {
        var post = BudgetPost.Create(code, $"Poste {code}", kind, prefixes, order).Value;
        post.SetAuditInfo("test", false);
        using var ctx = _factory.CreateContext();
        ctx.BudgetPosts.Add(post);
        ctx.SaveChanges();
        return post.Id;
    }

    private void SeedBudget(Guid postId, BudgetVersion version, params (int Month, decimal Amount)[] months)
    {
        using var ctx = _factory.CreateContext();
        foreach (var (month, amount) in months)
        {
            var line = BudgetLine.Create(postId, 2026, version, month, amount).Value;
            line.SetAuditInfo("test", false);
            ctx.BudgetLines.Add(line);
        }
        ctx.SaveChanges();
    }

    private void SeedValidatedYear()
    {
        using var ctx = _factory.CreateContext();
        var year = BudgetYear.Create(2026).Value;
        year.ValidateInitial("test");
        year.SetAuditInfo("test", false);
        ctx.BudgetYears.Add(year);
        ctx.SaveChanges();
    }

    private void SeedEntry(int number, DateTime date, string debitAccount, string creditAccount, decimal amount,
        JournalEntryStatus status = JournalEntryStatus.Validee)
    {
        var entry = JournalEntry.Create(number, "JOD", date, $"Écriture {number}", Guid.NewGuid(),
            false, "Manual", null, new[]
            {
                new JournalLineInput(debitAccount, "Débit", amount, 0m, null, ThirdPartyKind.None),
                new JournalLineInput(creditAccount, "Crédit", 0m, amount, null, ThirdPartyKind.None)
            }, initialStatus: status).Value;
        entry.SetAuditInfo("test", false);
        using var ctx = _factory.CreateContext();
        ctx.JournalEntries.Add(entry);
        ctx.SaveChanges();
    }

    [Fact]
    public async Task Report_ExpenseAndRevenue_UseCorrectSign()
    {
        SeedPost("61", BudgetPostKind.Expense, "61");
        SeedPost("70", BudgetPostKind.Revenue, "70", 100);
        SeedEntry(1, new DateTime(2026, 2, 10), "6132", "5320000", 1000m); // charge : D 613
        SeedEntry(2, new DateTime(2026, 2, 15), "5320000", "7011000", 5000m); // produit : C 701

        var report = await BuildService().GetBudgetReportAsync(2026, null);

        Assert.True(report.IsSuccess);
        var expense = report.Value.Rows.Single(r => r.Code == "61");
        var revenue = report.Value.Rows.Single(r => r.Code == "70");
        Assert.Equal(1000m, expense.MonthlyActual[1]);
        Assert.Equal(1000m, expense.PeriodActual);
        Assert.Equal(5000m, revenue.MonthlyActual[1]);
        Assert.Equal(5000m, revenue.PeriodActual);
    }

    [Fact]
    public async Task Report_LongestPrefixWins_AccountCountedOnce()
    {
        SeedPost("61", BudgetPostKind.Expense, "61", 10);
        SeedPost("613", BudgetPostKind.Expense, "613", 20);
        SeedEntry(1, new DateTime(2026, 3, 5), "6132000", "5320000", 800m);  // → poste 613
        SeedEntry(2, new DateTime(2026, 3, 6), "6110000", "5320000", 300m);  // → poste 61

        var report = await BuildService().GetBudgetReportAsync(2026, null);

        var post61 = report.Value.Rows.Single(r => r.Code == "61");
        var post613 = report.Value.Rows.Single(r => r.Code == "613");
        Assert.Equal(300m, post61.PeriodActual);
        Assert.Equal(800m, post613.PeriodActual);
        // Total charges = 1100 (chaque compte compté une seule fois).
        Assert.Equal(1100m, report.Value.Totals.ExpenseActual);
    }

    [Fact]
    public async Task Report_OffPostClass6And7_Appear_OtherClassesIgnored()
    {
        SeedPost("61", BudgetPostKind.Expense, "61");
        SeedEntry(1, new DateTime(2026, 4, 1), "6600000", "5320000", 450m);  // 66 non couvert → hors postes charges
        SeedEntry(2, new DateTime(2026, 4, 2), "5320000", "7500000", 900m);  // 75 non couvert → hors postes produits

        var report = await BuildService().GetBudgetReportAsync(2026, null);

        var offExpense = report.Value.Rows.Single(r => r.IsOffPost && r.Kind == (int)BudgetPostKind.Expense);
        var offRevenue = report.Value.Rows.Single(r => r.IsOffPost && r.Kind == (int)BudgetPostKind.Revenue);
        Assert.Equal(450m, offExpense.PeriodActual);
        Assert.Equal(900m, offRevenue.PeriodActual);
        // Le compte 532 (classe 5) n'apparaît nulle part.
        Assert.DoesNotContain(report.Value.Rows, r => r.Label.Contains("532"));
    }

    [Fact]
    public async Task Report_DraftPolicy_FollowsIncludeBrouillardInReports()
    {
        SeedPost("61", BudgetPostKind.Expense, "61");
        SeedEntry(1, new DateTime(2026, 5, 1), "6132000", "5320000", 100m, JournalEntryStatus.Validee);
        SeedEntry(2, new DateTime(2026, 5, 2), "6132000", "5320000", 40m, JournalEntryStatus.Brouillon);

        var withDrafts = await BuildService(includeDrafts: true).GetBudgetReportAsync(2026, null);
        var withoutDrafts = await BuildService(includeDrafts: false).GetBudgetReportAsync(2026, null);

        Assert.Equal(140m, withDrafts.Value.Rows.Single(r => r.Code == "61").PeriodActual);
        Assert.Equal(100m, withoutDrafts.Value.Rows.Single(r => r.Code == "61").PeriodActual);
    }

    [Fact]
    public async Task Report_ThroughMonth_LimitsPeriodTotals_MonthlyDetailComplete()
    {
        var postId = SeedPost("61", BudgetPostKind.Expense, "61");
        SeedBudget(postId, BudgetVersion.Initial, (1, 500m), (7, 700m));
        SeedEntry(1, new DateTime(2026, 1, 15), "6132000", "5320000", 400m);
        SeedEntry(2, new DateTime(2026, 7, 15), "6132000", "5320000", 600m);

        var report = await BuildService().GetBudgetReportAsync(2026, throughMonth: 6);

        var row = report.Value.Rows.Single(r => r.Code == "61");
        Assert.Equal(6, report.Value.ThroughMonth);
        Assert.Equal(400m, row.PeriodActual);   // juillet exclu du cumul
        Assert.Equal(500m, row.PeriodRevised);  // non validé ⇒ révisé = initial
        Assert.Equal(600m, row.MonthlyActual[6]); // mais le détail mensuel reste complet
    }

    [Fact]
    public async Task Report_VarianceAndPercent_DivisionByZeroGivesNull()
    {
        var postId = SeedPost("61", BudgetPostKind.Expense, "61");
        SeedPost("62", BudgetPostKind.Expense, "62", 20); // aucun budget
        SeedBudget(postId, BudgetVersion.Initial, (1, 1000m));
        SeedEntry(1, new DateTime(2026, 1, 10), "6132000", "5320000", 800m);
        SeedEntry(2, new DateTime(2026, 1, 11), "6210000", "5320000", 50m);

        var report = await BuildService().GetBudgetReportAsync(2026, null);

        var budgeted = report.Value.Rows.Single(r => r.Code == "61");
        Assert.Equal(-200m, budgeted.Variance);
        Assert.Equal(80.0m, budgeted.ConsumptionPercent);
        var unbudgeted = report.Value.Rows.Single(r => r.Code == "62");
        Assert.Null(unbudgeted.ConsumptionPercent);
        Assert.Equal(50m, unbudgeted.Variance); // réalisé − 0
    }

    [Fact]
    public async Task Report_WhenValidated_UsesRevisedDistinctFromInitial()
    {
        var postId = SeedPost("61", BudgetPostKind.Expense, "61");
        SeedValidatedYear();
        SeedBudget(postId, BudgetVersion.Initial, (1, 1000m));
        SeedBudget(postId, BudgetVersion.Revised, (1, 1500m));

        var report = await BuildService().GetBudgetReportAsync(2026, null);

        Assert.True(report.Value.IsValidated);
        var row = report.Value.Rows.Single(r => r.Code == "61");
        Assert.Equal(1000m, row.PeriodInitial);
        Assert.Equal(1500m, row.PeriodRevised);
    }

    [Fact]
    public async Task Report_FlagOff_Fails()
    {
        var result = await BuildService(enabled: false).GetBudgetReportAsync(2026, null);

        Assert.True(result.IsFailure);
        Assert.Contains("pas activée", result.Error.Description);
    }

    [Fact]
    public async Task Exports_CsvAndExcel_ProduceContent()
    {
        var postId = SeedPost("61", BudgetPostKind.Expense, "61");
        SeedBudget(postId, BudgetVersion.Initial, (1, 1000m));
        SeedEntry(1, new DateTime(2026, 1, 10), "6132000", "5320000", 800m);
        var report = (await BuildService().GetBudgetReportAsync(2026, null)).Value;

        var export = new AccountingExportService();
        var csv = export.ExportBudgetReportToCsv(report);
        var excel = export.ExportBudgetReportToExcel(report);

        var text = System.Text.Encoding.UTF8.GetString(csv);
        Assert.Contains("Code;Poste;Sens;Budget initial;Budget révisé;Réalisé;Écart;%", text);
        Assert.Contains("Poste 61", text);
        Assert.True(excel.Length > 500);
        Assert.Equal(0x50, excel[0]); // signature ZIP « PK » d'un .xlsx
        Assert.Equal(0x4B, excel[1]);
    }
}
