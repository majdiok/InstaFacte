using System.Text;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// C7 : service de rapprochement bancaire — import de relevé, rapprochement/dé-rapprochement
/// (avec gardes : brouillon interdit, ligne d'écriture déjà utilisée) et aperçu de fichier.
/// </summary>
public sealed class BankReconciliationServiceTests
{
    private readonly string _dbName = $"BankRecoDb_{Guid.NewGuid()}";
    private readonly TestTenantDbContextFactory _factory;

    public BankReconciliationServiceTests()
    {
        _factory = new TestTenantDbContextFactory(_dbName);
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

    private BankReconciliationService BuildService()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(u => u.Email).Returns("comptable@test.tn");
        var pdfImport = new Mock<IBankStatementPdfImportService>();
        return new BankReconciliationService(_factory, new Mock<IAuditService>().Object, currentUser.Object, pdfImport.Object,
            Options.Create(new AccountingSettings()));
    }

    private static ImportBankStatementRequest StatementRequest(params ImportBankStatementLineRequest[] lines) => new()
    {
        BankName = "BIAT",
        AccountNumber = "08 123 456789",
        StatementDate = new DateTime(2026, 5, 31),
        PeriodStart = new DateTime(2026, 5, 1),
        PeriodEnd = new DateTime(2026, 5, 31),
        OpeningBalance = 1000m,
        ClosingBalance = 2400m,
        Lines = lines
    };

    private static ImportBankStatementLineRequest Line(decimal amount, bool isDebit, string label = "Opération") => new()
    {
        TransactionDate = new DateTime(2026, 5, 10),
        Reference = "REF-1",
        Description = label,
        Amount = amount,
        IsDebit = isDebit
    };

    private (Guid PeriodId, Guid JournalLineId) SeedJournalLine(JournalEntryStatus status)
    {
        var period = AccountingPeriod.Create(2026, 5, new DateTime(2026, 5, 1), new DateTime(2026, 5, 31));
        period.SetAuditInfo("test", false);
        var entry = JournalEntry.Create(1, "JB", new DateTime(2026, 5, 10), "Encaissement", period.Id,
            false, "Manual", null, new[]
            {
                new JournalLineInput("532", "Banque", 1400m, 0m, null, ThirdPartyKind.None),
                new JournalLineInput("4111", "Client", 0m, 1400m, null, ThirdPartyKind.None)
            }, initialStatus: status).Value;
        entry.SetAuditInfo("test", false);
        using var ctx = _factory.CreateContext();
        ctx.AccountingPeriods.Add(period);
        ctx.JournalEntries.Add(entry);
        ctx.SaveChanges();
        return (period.Id, entry.Lines.First(l => l.AccountNumber == "532").Id);
    }

    [Fact]
    public async Task ImportStatement_HappyPath_PersistsStatementAndLines()
    {
        var service = BuildService();

        var result = await service.ImportStatementAsync(
            StatementRequest(Line(1500m, isDebit: false), Line(100m, isDebit: true)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Lines.Count);
        using var ctx = _factory.CreateContext();
        Assert.Equal(1, ctx.BankStatements.Count());
        Assert.Equal(2, ctx.BankStatementLines.Count());
    }

    [Fact]
    public async Task ImportStatement_WithoutLines_Fails()
    {
        var result = await BuildService().ImportStatementAsync(StatementRequest(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("aucune ligne", result.Error.Description);
    }

    [Fact]
    public async Task GetStatements_FiltersByAccount()
    {
        var service = BuildService();
        await service.ImportStatementAsync(StatementRequest(Line(10m, true)), CancellationToken.None);

        var match = await service.GetStatementsAsync("08 123 456789", null, null, CancellationToken.None);
        var noMatch = await service.GetStatementsAsync("autre-compte", null, null, CancellationToken.None);

        Assert.Single(match.Value);
        Assert.Empty(noMatch.Value);
    }

    [Fact]
    public async Task Reconcile_HappyPath_MarksLine()
    {
        var service = BuildService();
        var (_, journalLineId) = SeedJournalLine(JournalEntryStatus.Validee);
        var import = await service.ImportStatementAsync(StatementRequest(Line(1400m, false, "Virement client")), CancellationToken.None);
        var bankLineId = import.Value.Lines[0].Id;

        var result = await service.ReconcileLineAsync(
            new ReconcileLineRequest { BankStatementLineId = bankLineId, JournalEntryLineId = journalLineId },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var statement = await service.GetStatementAsync(import.Value.Id, CancellationToken.None);
        var line = statement.Value.Lines[0];
        Assert.True(line.IsReconciled);
        Assert.Equal(journalLineId, line.ReconciledJournalEntryLineId);
    }

    [Fact]
    public async Task Reconcile_DraftJournalEntry_Fails()
    {
        var service = BuildService();
        var (_, journalLineId) = SeedJournalLine(JournalEntryStatus.Brouillon);
        var import = await service.ImportStatementAsync(StatementRequest(Line(1400m, false)), CancellationToken.None);

        var result = await service.ReconcileLineAsync(
            new ReconcileLineRequest { BankStatementLineId = import.Value.Lines[0].Id, JournalEntryLineId = journalLineId },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("brouillon", result.Error.Description);
    }

    [Fact]
    public async Task Reconcile_JournalLineAlreadyUsed_Fails()
    {
        var service = BuildService();
        var (_, journalLineId) = SeedJournalLine(JournalEntryStatus.Validee);
        var import = await service.ImportStatementAsync(
            StatementRequest(Line(1400m, false, "Ligne A"), Line(1400m, false, "Ligne B")), CancellationToken.None);
        var lineA = import.Value.Lines[0].Id;
        var lineB = import.Value.Lines[1].Id;
        Assert.True((await service.ReconcileLineAsync(
            new ReconcileLineRequest { BankStatementLineId = lineA, JournalEntryLineId = journalLineId },
            CancellationToken.None)).IsSuccess);

        var result = await service.ReconcileLineAsync(
            new ReconcileLineRequest { BankStatementLineId = lineB, JournalEntryLineId = journalLineId },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("déjà rapprochée", result.Error.Description);
    }

    [Fact]
    public async Task Unreconcile_ThenLineIsReusable()
    {
        var service = BuildService();
        var (_, journalLineId) = SeedJournalLine(JournalEntryStatus.Validee);
        var import = await service.ImportStatementAsync(StatementRequest(Line(1400m, false)), CancellationToken.None);
        var bankLineId = import.Value.Lines[0].Id;
        await service.ReconcileLineAsync(
            new ReconcileLineRequest { BankStatementLineId = bankLineId, JournalEntryLineId = journalLineId },
            CancellationToken.None);

        var un = await service.UnreconcileLineAsync(bankLineId, CancellationToken.None);
        Assert.True(un.IsSuccess);

        // Idempotent + re-rapprochable.
        Assert.True((await service.UnreconcileLineAsync(bankLineId, CancellationToken.None)).IsSuccess);
        var again = await service.ReconcileLineAsync(
            new ReconcileLineRequest { BankStatementLineId = bankLineId, JournalEntryLineId = journalLineId },
            CancellationToken.None);
        Assert.True(again.IsSuccess);
    }

    [Fact]
    public async Task PreviewStatementFile_ComputesTotalsAndPeriod()
    {
        var csv = Encoding.UTF8.GetBytes(
            "date,libelle,montant\n" +
            "2026-05-03,Virement reçu,1500\n" +
            "2026-05-20,Frais,-25.500\n");

        var result = await BuildService().PreviewStatementFileAsync(
            csv, BankStatementFileFormat.Csv, "test.csv", "text/csv", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value;
        Assert.True(dto.CanImport);
        Assert.Equal(2, dto.ValidLines);
        Assert.Equal(25.500m, dto.TotalDebit);
        Assert.Equal(1500m, dto.TotalCredit);
        Assert.Equal(new DateTime(2026, 5, 3), dto.PeriodStart);
        Assert.Equal(new DateTime(2026, 5, 20), dto.PeriodEnd);
    }

    [Fact]
    public async Task PreviewStatementFile_BlockingIssues_CannotImport()
    {
        var csv = Encoding.UTF8.GetBytes("date,libelle\n2026-05-03,Sans montant\n");

        var result = await BuildService().PreviewStatementFileAsync(
            csv, BankStatementFileFormat.Csv, "test.csv", "text/csv", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.CanImport);
        Assert.NotEmpty(result.Value.Issues);
    }

    // ── Lot K : dédoublonnage à l'import (option « Ignorer les écritures déjà importées ») ──

    [Fact]
    public void ComputeFingerprint_SameInputs_IsStable_AndDiscriminates()
    {
        var a = BankStatementLine.ComputeFingerprint("08 123 456789", new DateTime(2026, 5, 10), 1500m, false, "REF-1", "Virement");
        var aBis = BankStatementLine.ComputeFingerprint("08 123 456789", new DateTime(2026, 5, 10), 1500m, false, "REF-1", "Virement");
        var differentAmount = BankStatementLine.ComputeFingerprint("08 123 456789", new DateTime(2026, 5, 10), 1501m, false, "REF-1", "Virement");
        var differentAccount = BankStatementLine.ComputeFingerprint("99 999 999999", new DateTime(2026, 5, 10), 1500m, false, "REF-1", "Virement");

        Assert.Equal(a, aBis);
        Assert.NotEqual(a, differentAmount);
        Assert.NotEqual(a, differentAccount);
    }

    [Fact]
    public async Task Reimport_WithSkip_IgnoresDuplicates_ImportsOnlyNew()
    {
        var service = BuildService();
        await service.ImportStatementAsync(
            StatementRequest(Line(1500m, false), Line(100m, true)), CancellationToken.None);

        var second = await service.ImportStatementAsync(
            StatementRequest(Line(1500m, false), Line(100m, true), Line(200m, true, "Nouveau mouvement"))
                with { SkipAlreadyImported = true },
            CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Single(second.Value.Lines);
        Assert.Equal(200m, second.Value.Lines[0].Amount);
        Assert.Equal(2, second.Value.SkippedDuplicateCount);
        using var ctx = _factory.CreateContext();
        Assert.Equal(3, ctx.BankStatementLines.Count()); // 2 premières + 1 nouvelle
    }

    [Fact]
    public async Task Reimport_WithoutSkip_ImportsAll_HistoricalBehavior()
    {
        var service = BuildService();
        await service.ImportStatementAsync(
            StatementRequest(Line(1500m, false), Line(100m, true)), CancellationToken.None);

        var second = await service.ImportStatementAsync(
            StatementRequest(Line(1500m, false), Line(100m, true)), CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Equal(2, second.Value.Lines.Count);
        Assert.Equal(0, second.Value.SkippedDuplicateCount);
        using var ctx = _factory.CreateContext();
        Assert.Equal(4, ctx.BankStatementLines.Count());
    }

    [Fact]
    public async Task Reimport_WithSkip_AllDuplicates_Fails()
    {
        var service = BuildService();
        await service.ImportStatementAsync(
            StatementRequest(Line(1500m, false), Line(100m, true)), CancellationToken.None);

        var second = await service.ImportStatementAsync(
            StatementRequest(Line(1500m, false), Line(100m, true)) with { SkipAlreadyImported = true },
            CancellationToken.None);

        Assert.True(second.IsFailure);
        Assert.Contains("déjà été importées", second.Error.Description);
    }
}
