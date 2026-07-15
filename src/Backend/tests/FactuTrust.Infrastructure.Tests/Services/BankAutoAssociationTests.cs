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
/// Lot J : association automatique (classement 1/multi/0), rapprochement en lot des paires
/// confirmées, comptabilisation d'une ligne (écriture 512 ↔ contrepartie + rapprochement).
/// </summary>
public sealed class BankAutoAssociationTests
{
    private const string BankAccount = "532";
    private readonly string _dbName = $"BankAutoDb_{Guid.NewGuid()}";
    private readonly TestTenantDbContextFactory _factory;

    public BankAutoAssociationTests()
    {
        _factory = new TestTenantDbContextFactory(_dbName);
        SeedAccounts();
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

    private void SeedAccounts()
    {
        using var ctx = _factory.CreateContext();
        ctx.ChartOfAccounts.Add(ChartOfAccount.Create(BankAccount, "Banque", 5, null, AccountNatureType.Debit).Value);
        ctx.ChartOfAccounts.Add(ChartOfAccount.Create("4111", "Clients", 4, "411", AccountNatureType.Debit).Value);
        ctx.ChartOfAccounts.Add(ChartOfAccount.Create("658", "Charges bancaires", 6, null, AccountNatureType.Debit).Value);
        ctx.SaveChanges();
    }

    private BankReconciliationService BuildService(bool brouillard = false)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(u => u.Email).Returns("compta@test.tn");
        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var pdf = new Mock<IBankStatementPdfImportService>();
        return new BankReconciliationService(_factory, audit.Object, currentUser.Object, pdf.Object,
            Options.Create(new AccountingSettings { BankMatchWindowDays = 10, BrouillardEnabled = brouillard }));
    }

    /// <summary>Relevé lié au compte banque 532, avec les lignes fournies.</summary>
    private async Task<Guid> ImportStatementAsync(BankReconciliationService service, params ImportBankStatementLineRequest[] lines)
    {
        var request = new ImportBankStatementRequest
        {
            BankName = "BIAT",
            AccountNumber = "08 123 456789",
            ChartOfAccountNumber = BankAccount,
            StatementDate = new DateTime(2026, 5, 31),
            PeriodStart = new DateTime(2026, 5, 1),
            PeriodEnd = new DateTime(2026, 5, 31),
            OpeningBalance = 0m,
            ClosingBalance = 0m,
            Lines = lines
        };
        var result = await service.ImportStatementAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value.Id;
    }

    private static ImportBankStatementLineRequest Line(decimal amount, bool isDebit, DateTime date, string label) => new()
    {
        TransactionDate = date,
        Reference = label,
        Description = label,
        Amount = amount,
        IsDebit = isDebit
    };

    /// <summary>Écriture sur le compte banque : débit (encaissement) ou crédit (décaissement).</summary>
    private void SeedBankEntry(int number, DateTime date, decimal amount, bool bankDebit, JournalEntryStatus status = JournalEntryStatus.Validee)
    {
        var lines = bankDebit
            ? new[]
            {
                new JournalLineInput(BankAccount, "Banque", amount, 0m, null, ThirdPartyKind.None),
                new JournalLineInput("4111", "Client", 0m, amount, null, ThirdPartyKind.None)
            }
            : new[]
            {
                new JournalLineInput("658", "Charges", amount, 0m, null, ThirdPartyKind.None),
                new JournalLineInput(BankAccount, "Banque", 0m, amount, null, ThirdPartyKind.None)
            };
        using var ctx = _factory.CreateContext();
        var period = ctx.AccountingPeriods.FirstOrDefault(p => p.FiscalYear == date.Year && p.Month == date.Month);
        if (period is null)
        {
            period = AccountingPeriod.Create(date.Year, date.Month, new DateTime(date.Year, date.Month, 1), new DateTime(date.Year, date.Month, 28));
            period.SetAuditInfo("test", false);
            ctx.AccountingPeriods.Add(period);
        }
        var entry = JournalEntry.Create(number, "JB", date, $"Écriture {number}", period.Id,
            false, "Manual", null, lines, initialStatus: status).Value;
        entry.SetAuditInfo("test", false);
        ctx.JournalEntries.Add(entry);
        ctx.SaveChanges();
    }

    [Fact]
    public async Task AutoAssociate_SingleMatch_IsAssociated()
    {
        var service = BuildService();
        // Encaissement 1400 le 10/05 (bank line credit) ↔ écriture débit banque 1400 le 11/05.
        SeedBankEntry(1, new DateTime(2026, 5, 11), 1400m, bankDebit: true);
        var statementId = await ImportStatementAsync(service, Line(1400m, isDebit: false, new DateTime(2026, 5, 10), "Virement client"));

        var result = await service.AutoAssociateAsync(statementId);

        Assert.True(result.IsSuccess);
        var assoc = Assert.Single(result.Value.Associations);
        Assert.Equal((int)BankLineAssociationStatus.Associated, assoc.Status);
        Assert.NotNull(assoc.ProposedJournalEntryLineId);
        Assert.Equal(1, result.Value.Summary.AutoMatchedCount);
    }

    [Fact]
    public async Task AutoAssociate_MultipleMatches_IsToReconcile()
    {
        var service = BuildService();
        SeedBankEntry(1, new DateTime(2026, 5, 11), 1400m, bankDebit: true);
        SeedBankEntry(2, new DateTime(2026, 5, 12), 1400m, bankDebit: true);
        var statementId = await ImportStatementAsync(service, Line(1400m, isDebit: false, new DateTime(2026, 5, 10), "Virement"));

        var result = await service.AutoAssociateAsync(statementId);

        var assoc = Assert.Single(result.Value.Associations);
        Assert.Equal((int)BankLineAssociationStatus.ToReconcile, assoc.Status);
        Assert.Equal(2, assoc.CandidateCount);
        Assert.Equal(1, result.Value.Summary.ToReconcileCount);
    }

    [Fact]
    public async Task AutoAssociate_NoMatch_IsNotAssociated()
    {
        var service = BuildService();
        var statementId = await ImportStatementAsync(service, Line(999m, isDebit: true, new DateTime(2026, 5, 10), "Frais"));

        var result = await service.AutoAssociateAsync(statementId);

        var assoc = Assert.Single(result.Value.Associations);
        Assert.Equal((int)BankLineAssociationStatus.NotAssociated, assoc.Status);
        Assert.Equal(1, result.Value.Summary.NotAssociatedCount);
    }

    [Fact]
    public async Task AutoAssociate_OutOfWindow_NotMatched()
    {
        var service = BuildService();
        SeedBankEntry(1, new DateTime(2026, 5, 25), 1400m, bankDebit: true); // 15 jours > fenêtre 10
        var statementId = await ImportStatementAsync(service, Line(1400m, isDebit: false, new DateTime(2026, 5, 10), "Virement"));

        var result = await service.AutoAssociateAsync(statementId);

        Assert.Equal((int)BankLineAssociationStatus.NotAssociated, result.Value.Associations[0].Status);
    }

    [Fact]
    public async Task ApplyAssociations_ReconcilesConfirmedPairs()
    {
        var service = BuildService();
        SeedBankEntry(1, new DateTime(2026, 5, 11), 1400m, bankDebit: true);
        var statementId = await ImportStatementAsync(service, Line(1400m, isDebit: false, new DateTime(2026, 5, 10), "Virement client"));
        var proposal = (await service.AutoAssociateAsync(statementId)).Value.Associations.Single();

        var result = await service.ApplyAssociationsAsync(statementId, new ApplyAssociationsRequest
        {
            Pairs = new[] { new ReconcilePairRequest { BankStatementLineId = proposal.BankStatementLineId, JournalEntryLineId = proposal.ProposedJournalEntryLineId!.Value } }
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.AppliedCount);
        using var ctx = _factory.CreateContext();
        Assert.True(ctx.BankStatementLines.Single(l => l.Id == proposal.BankStatementLineId).IsReconciled);
    }

    [Fact]
    public async Task CreateEntryForLine_Encaissement_GeneratesBalancedEntryAndReconciles()
    {
        var service = BuildService();
        var statementId = await ImportStatementAsync(service, Line(500m, isDebit: false, new DateTime(2026, 5, 10), "Encaissement divers"));
        Guid lineId;
        using (var ctx = _factory.CreateContext())
            lineId = ctx.BankStatementLines.Single(l => l.BankStatementId == statementId).Id;

        var result = await service.CreateEntryForLineAsync(lineId, new CreateEntryForLineRequest
        {
            JournalCode = "BQ", CounterpartyAccount = "4111", Label = "Encaissement"
        });

        Assert.True(result.IsSuccess);
        using var check = _factory.CreateContext();
        var entry = check.JournalEntries.Include(e => e.Lines).Single(e => e.SourceEntityType == "BankReconciliation");
        Assert.Equal(500m, entry.Lines.Single(l => l.AccountNumber == BankAccount).DebitAmount.Amount);   // encaissement ⇒ débit banque
        Assert.Equal(500m, entry.Lines.Single(l => l.AccountNumber == "4111").CreditAmount.Amount);
        Assert.True(check.BankStatementLines.Single(l => l.Id == lineId).IsReconciled);
    }

    [Fact]
    public async Task CreateEntryForLine_Decaissement_ReversesSides()
    {
        var service = BuildService();
        var statementId = await ImportStatementAsync(service, Line(200m, isDebit: true, new DateTime(2026, 5, 10), "Frais bancaires"));
        Guid lineId;
        using (var ctx = _factory.CreateContext())
            lineId = ctx.BankStatementLines.Single(l => l.BankStatementId == statementId).Id;

        var result = await service.CreateEntryForLineAsync(lineId, new CreateEntryForLineRequest
        {
            JournalCode = "BQ", CounterpartyAccount = "658"
        });

        Assert.True(result.IsSuccess);
        using var check = _factory.CreateContext();
        var entry = check.JournalEntries.Include(e => e.Lines).Single(e => e.SourceEntityType == "BankReconciliation");
        Assert.Equal(200m, entry.Lines.Single(l => l.AccountNumber == "658").DebitAmount.Amount);          // décaissement ⇒ débit contrepartie
        Assert.Equal(200m, entry.Lines.Single(l => l.AccountNumber == BankAccount).CreditAmount.Amount);   // crédit banque
    }

    [Fact]
    public async Task CreateEntryForLine_InactiveCounterparty_Fails()
    {
        using (var ctx = _factory.CreateContext())
        {
            var acc = ctx.ChartOfAccounts.Single(c => c.AccountNumber == "658");
            acc.ToggleActive();
            ctx.SaveChanges();
        }
        var service = BuildService();
        var statementId = await ImportStatementAsync(service, Line(200m, isDebit: true, new DateTime(2026, 5, 10), "Frais"));
        Guid lineId;
        using (var ctx = _factory.CreateContext())
            lineId = ctx.BankStatementLines.Single(l => l.BankStatementId == statementId).Id;

        var result = await service.CreateEntryForLineAsync(lineId, new CreateEntryForLineRequest { JournalCode = "BQ", CounterpartyAccount = "658" });

        Assert.True(result.IsFailure);
        Assert.Contains("désactivé", result.Error.Description);
    }
}
