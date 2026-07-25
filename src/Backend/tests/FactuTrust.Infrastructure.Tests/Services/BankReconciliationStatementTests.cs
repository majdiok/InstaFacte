using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// État de rapprochement bancaire : confrontation solde comptable ancré ↔ solde relevé, suspens des
/// deux côtés, écart. L'identité clé : à cohérence complète l'écart est nul ; un suspens oublié le
/// fait ressortir. Le solde comptable réutilise la balance ancrée (pas de double compte à-nouveaux).
/// </summary>
public sealed class BankReconciliationStatementTests
{
    private const string Bank = "532";
    private readonly string _dbName = $"BankRecoStmtDb_{Guid.NewGuid()}";
    private readonly TestTenantDbContextFactory _factory;

    public BankReconciliationStatementTests() => _factory = new TestTenantDbContextFactory(_dbName);

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
        currentUser.SetupGet(u => u.Email).Returns("compta@test.tn");
        var reporting = new AccountingReportingService(_factory, Options.Create(new AccountingSettings()));
        return new BankReconciliationService(
            _factory, new Mock<IAuditService>().Object, currentUser.Object,
            new Mock<IBankStatementPdfImportService>().Object, reporting, Options.Create(new AccountingSettings()));
    }

    /// <summary>Écriture sur le compte banque : débit (encaissement) ou crédit (décaissement). Renvoie l'Id de la ligne 532.</summary>
    private Guid SeedBankEntry(int number, DateTime date, decimal amount, bool debitBank,
        string source = "Manual", string journal = "JB", JournalEntryStatus status = JournalEntryStatus.Validee)
    {
        var counterparty = debitBank ? "4111" : "607";
        var lines = debitBank
            ? new[]
            {
                new JournalLineInput(Bank, "Banque", amount, 0m, null, ThirdPartyKind.None),
                new JournalLineInput(counterparty, "Contrepartie", 0m, amount, null, ThirdPartyKind.None)
            }
            : new[]
            {
                new JournalLineInput(counterparty, "Contrepartie", amount, 0m, null, ThirdPartyKind.None),
                new JournalLineInput(Bank, "Banque", 0m, amount, null, ThirdPartyKind.None)
            };
        var period = AccountingPeriod.Create(date.Year, date.Month, new DateTime(date.Year, date.Month, 1), date);
        period.SetAuditInfo("test", false);
        var entry = JournalEntry.Create(number, journal, date, "Op banque", period.Id,
            false, source, source == AccountingService.SourceOpeningBalance ? Guid.NewGuid() : null,
            lines, initialStatus: status).Value;
        entry.SetAuditInfo("test", false);
        using var ctx = _factory.CreateContext();
        ctx.AccountingPeriods.Add(period);
        ctx.JournalEntries.Add(entry);
        ctx.SaveChanges();
        return entry.Lines.First(l => l.AccountNumber == Bank).Id;
    }

    private Guid SeedStatement(decimal closing, params (decimal Amount, bool IsDebit, Guid? ReconciledTo)[] lines)
    {
        var st = BankStatement.Create("BIAT", "08 123 456789",
            new DateTime(2026, 5, 31), new DateTime(2026, 5, 1), new DateTime(2026, 5, 31),
            Money.Create(1000m), Money.Create(closing));
        st.SetProvenance(null, Bank, null, null, BankStatementImportMethod.Manual);
        st.SetAuditInfo("test", false);

        var entities = new List<BankStatementLine>();
        var n = 1;
        foreach (var (amount, isDebit, reconciledTo) in lines)
        {
            var line = BankStatementLine.Create(st.Id, new DateTime(2026, 5, 10), $"REF-{n++}", "Opération",
                Money.Create(amount), isDebit);
            if (reconciledTo is { } id)
                line.Reconcile(id);
            line.SetAuditInfo("test", false);
            entities.Add(line);
        }

        using var ctx = _factory.CreateContext();
        ctx.BankStatements.Add(st);
        ctx.BankStatementLines.AddRange(entities);
        ctx.SaveChanges();
        return st.Id;
    }

    [Fact]
    public async Task SansCompteBanque_Echoue()
    {
        var st = BankStatement.Create("BIAT", "08 1", new DateTime(2026, 5, 31),
            new DateTime(2026, 5, 1), new DateTime(2026, 5, 31), Money.Create(0m), Money.Create(0m));
        st.SetAuditInfo("test", false);   // pas de SetProvenance → ChartOfAccountNumber null
        using (var ctx = _factory.CreateContext()) { ctx.BankStatements.Add(st); ctx.SaveChanges(); }

        var result = await BuildService().GetReconciliationStatementAsync(st.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("compte banque", result.Error.Description);
    }

    [Fact]
    public async Task EntierementRapproche_EcartNul()
    {
        var l1 = SeedBankEntry(1, new DateTime(2026, 5, 10), 1500m, debitBank: true);
        var l2 = SeedBankEntry(2, new DateTime(2026, 5, 12), 100m, debitBank: false);
        // B_acc = 1500 − 100 = 1400 ; relevé 1400, deux lignes pointées.
        var stId = SeedStatement(1400m, (1500m, false, l1), (100m, true, l2));

        var result = await BuildService().GetReconciliationStatementAsync(stId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1400m, result.Value.AccountingBalance);
        Assert.Empty(result.Value.UnreconciledBookItems);
        Assert.Empty(result.Value.UnreconciledStatementItems);
        Assert.Equal(0m, result.Value.Difference);
        Assert.True(result.Value.IsReconciled);
    }

    [Fact]
    public async Task ChequeEmisNonDebite_ApparaitEnSuspensComptable_EcartNul()
    {
        var l1 = SeedBankEntry(1, new DateTime(2026, 5, 10), 1500m, debitBank: true);
        var l2 = SeedBankEntry(2, new DateTime(2026, 5, 12), 100m, debitBank: false);
        SeedBankEntry(3, new DateTime(2026, 5, 20), 300m, debitBank: false);   // chèque émis, non pointé
        // B_acc = 1500 − 100 − 300 = 1100 ; la banque n'a pas encore débité le chèque → relevé 1400.
        var stId = SeedStatement(1400m, (1500m, false, l1), (100m, true, l2));

        var result = await BuildService().GetReconciliationStatementAsync(stId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1100m, result.Value.AccountingBalance);
        var suspens = Assert.Single(result.Value.UnreconciledBookItems);
        Assert.Equal(300m, suspens.Credit);           // décaissement → crédit banque
        Assert.Equal(1100m, result.Value.AdjustedStatementBalance);   // 1400 − 300
        Assert.Equal(0m, result.Value.Difference);
        Assert.True(result.Value.IsReconciled);
    }

    [Fact]
    public async Task FraisBancairesNonComptabilises_ApparaissentEnSuspensReleve_EcartNul()
    {
        var l1 = SeedBankEntry(1, new DateTime(2026, 5, 10), 1500m, debitBank: true);
        var l2 = SeedBankEntry(2, new DateTime(2026, 5, 12), 100m, debitBank: false);
        // B_acc = 1400 ; la banque a prélevé 50 de frais → relevé 1350 ; ligne frais non comptabilisée.
        var stId = SeedStatement(1350m, (1500m, false, l1), (100m, true, l2), (50m, true, null));

        var result = await BuildService().GetReconciliationStatementAsync(stId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1400m, result.Value.AccountingBalance);
        var suspens = Assert.Single(result.Value.UnreconciledStatementItems);
        Assert.Equal(50m, suspens.Credit);            // frais = décaissement → crédit banque
        Assert.Equal(1350m, result.Value.AdjustedAccountingBalance);   // 1400 − 50
        Assert.Equal(0m, result.Value.Difference);
        Assert.True(result.Value.IsReconciled);
    }

    [Fact]
    public async Task EcartInexplique_RessortNonNul()
    {
        var l1 = SeedBankEntry(1, new DateTime(2026, 5, 10), 1500m, debitBank: true);
        // B_acc = 1500 ; relevé 1450 sans aucun suspens pour l'expliquer → écart 50.
        var stId = SeedStatement(1450m, (1500m, false, l1));

        var result = await BuildService().GetReconciliationStatementAsync(stId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(50m, result.Value.Difference);
        Assert.False(result.Value.IsReconciled);
    }

    [Fact]
    public async Task LignePointeeParUnAutreReleve_AbsenteDesSuspens()
    {
        var l1 = SeedBankEntry(1, new DateTime(2026, 5, 10), 1500m, debitBank: true);
        // L'écriture est pointée par un SECOND relevé (juin) → ne doit pas ressortir en suspens du relevé sous revue.
        SeedStatement(1500m, (1500m, false, l1));                 // relevé « autre » qui pointe l1
        var stId = SeedStatement(1500m);                          // relevé sous revue, aucune ligne

        var result = await BuildService().GetReconciliationStatementAsync(stId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.UnreconciledBookItems);         // l1 exclue car pointée ailleurs
    }

    [Fact]
    public async Task SoldeComptableAncre_SurExerciceAvecANouveau_PasDeDoubleCompte()
    {
        // Détail antérieur (2025) + à-nouveau 2026 qui le résume : la balance ancrée ne compte le report qu'une fois.
        SeedBankEntry(1, new DateTime(2025, 6, 15), 500m, debitBank: true);
        SeedBankEntry(2, new DateTime(2026, 1, 1), 500m, debitBank: true, source: AccountingService.SourceOpeningBalance, journal: "JAN");
        SeedBankEntry(3, new DateTime(2026, 5, 10), 100m, debitBank: true);
        var stId = SeedStatement(600m);   // à-nouveau 500 + mouvement 2026 100 = 600 (et non 1100)

        var result = await BuildService().GetReconciliationStatementAsync(stId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(600m, result.Value.AccountingBalance);
    }
}
