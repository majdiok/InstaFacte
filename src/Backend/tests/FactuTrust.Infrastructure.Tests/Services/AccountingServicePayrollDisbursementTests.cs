using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Services;
using FactuTrust.Infrastructure.Tests.Application.Payroll;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Écriture de décaissement des avances et prêts salariés (débit 421 / 421.1, crédit trésorerie).
/// Sans elle, la retenue opérée sur le bulletin suivant crédite 421 sans contrepartie et ce compte
/// d'actif reste durablement créditeur.
/// </summary>
public sealed class AccountingServicePayrollDisbursementTests
{
    private static ChartOfAccount Acc(string number) =>
        ChartOfAccount.Create(number, $"Compte {number}", int.Parse(number[..1].ToString()), null, AccountNatureType.Debit).Value;

    private static (AccountingService Service, List<JournalEntry> Captured, Mock<IJournalEntryRepository> Journals)
        BuildService(bool disbursementEnabled)
    {
        var chart = new Mock<IChartOfAccountRepository>();
        chart.Setup(x => x.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(50);
        chart.Setup(x => x.GetByAccountNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string n, CancellationToken _) => Acc(n));

        var periodService = new Mock<IAccountingPeriodService>();
        periodService.Setup(x => x.EnsureOpenPeriodAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime d, CancellationToken _) =>
                Result.Success(AccountingPeriod.Create(d.Year, d.Month, new DateTime(d.Year, d.Month, 1),
                    new DateTime(d.Year, d.Month, 1).AddMonths(1).AddDays(-1))));

        var captured = new List<JournalEntry>();
        var journals = new Mock<IJournalEntryRepository>();
        journals.Setup(x => x.GetBySourceAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string type, Guid id, CancellationToken _) =>
                captured.FirstOrDefault(e => e.SourceEntityType == type && e.SourceEntityId == id));
        journals.Setup(x => x.GetActiveBySourceAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string type, Guid id, CancellationToken _) =>
                captured.FirstOrDefault(e => e.SourceEntityType == type && e.SourceEntityId == id && !e.IsReversed));
        journals.Setup(x => x.ReserveNextEntryNumberAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        journals.Setup(x => x.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()))
            .Callback<JournalEntry, CancellationToken>((e, _) => captured.Add(e))
            .ReturnsAsync((JournalEntry e, CancellationToken _) => e);

        var settings = new AccountingSettings
        {
            BrouillardEnabled = false,
            PayrollDisbursementEntriesEnabled = disbursementEnabled
        };

        var service = new AccountingService(
            chart.Object,
            periodService.Object,
            journals.Object,
            new Mock<IWithholdingTaxRepository>().Object,
            new Mock<IDepreciationRateCategoryRepository>().Object,
            new Mock<ITenantDbContextFactory>().Object,
            NullLogger<AccountingService>.Instance,
            Options.Create(settings),
            audit: null,
            payrollProfileResolver: new PayrollProfileResolverStub(settings));

        return (service, captured, journals);
    }

    private static EmployeeAdvance BuildAdvance(decimal amount = 150m) =>
        EmployeeAdvance.Create(Guid.NewGuid(), new DateTime(2026, 8, 10), amount, "Avance août").Value;

    private static EmployeeLoan BuildLoan(decimal principal = 1200m) =>
        EmployeeLoan.Create(Guid.NewGuid(), "PRET-2026-001", principal, 12, 2026, 9, null).Value;

    // ── Drapeau éteint : aucun changement pour les dossiers existants ────────────────────────

    [Fact]
    public async Task Advance_ProducesNoEntry_WhenDisbursementIsDisabled()
    {
        var (service, captured, _) = BuildService(disbursementEnabled: false);

        var result = await service.GenerateEmployeeAdvanceDisbursementEntryAsync(
            BuildAdvance(), "Alice", PaymentMethod.BankTransfer, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(captured);
    }

    // ── Avance ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Advance_Debits421_AndCreditsBank()
    {
        var (service, captured, _) = BuildService(disbursementEnabled: true);
        var advance = BuildAdvance(150m);

        var result = await service.GenerateEmployeeAdvanceDisbursementEntryAsync(
            advance, "Alice Ben Salah", PaymentMethod.BankTransfer, null, CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        var entry = Assert.Single(captured);

        Assert.Equal("JB", entry.JournalCode);
        Assert.Equal(AccountingService.SourceEmployeeAdvance, entry.SourceEntityType);
        Assert.Equal(advance.Id, entry.SourceEntityId);
        Assert.Equal(new DateTime(2026, 8, 10), entry.EntryDate);

        Assert.Equal(150m, entry.Lines.Where(l => l.AccountNumber == "421").Sum(l => l.DebitAmount.Amount));
        Assert.Equal(150m, entry.Lines.Where(l => l.AccountNumber == "5321").Sum(l => l.CreditAmount.Amount));
        Assert.Equal(0m, entry.Lines.Sum(l => l.DebitAmount.Amount) - entry.Lines.Sum(l => l.CreditAmount.Amount));
        Assert.Contains(entry.Lines, l => l.Label.Contains("Alice Ben Salah", StringComparison.Ordinal));

        // 421 est un compte collectif : aucun code auxiliaire à exporter au FEC.
        Assert.All(entry.Lines, l => Assert.Equal(ThirdPartyKind.None, l.ThirdPartyKind));
    }

    [Fact]
    public async Task Advance_InCash_UsesCashJournalAndAccount()
    {
        var (service, captured, _) = BuildService(disbursementEnabled: true);

        await service.GenerateEmployeeAdvanceDisbursementEntryAsync(
            BuildAdvance(), "Alice", PaymentMethod.Cash, null, CancellationToken.None);

        var entry = Assert.Single(captured);
        Assert.Equal("JC", entry.JournalCode);
        Assert.Contains(entry.Lines, l => l.AccountNumber == "5411" && l.CreditAmount.Amount > 0);
    }

    [Fact]
    public async Task Advance_UsesBankAccountGlMapping_WhenProvided()
    {
        var (service, captured, _) = BuildService(disbursementEnabled: true);

        // Le compte comptable rattaché au compte bancaire prime sur le compte de banque par défaut.
        await service.GenerateEmployeeAdvanceDisbursementEntryAsync(
            BuildAdvance(), "Alice", PaymentMethod.BankTransfer, BankAccountWithGl("5321004"), CancellationToken.None);

        var entry = Assert.Single(captured);
        Assert.Contains(entry.Lines, l => l.AccountNumber == "5321004" && l.CreditAmount.Amount > 0);
    }

    [Fact]
    public async Task Advance_IsIdempotent()
    {
        var (service, captured, _) = BuildService(disbursementEnabled: true);
        var advance = BuildAdvance();

        await service.GenerateEmployeeAdvanceDisbursementEntryAsync(
            advance, "Alice", PaymentMethod.BankTransfer, null, CancellationToken.None);
        await service.GenerateEmployeeAdvanceDisbursementEntryAsync(
            advance, "Alice", PaymentMethod.BankTransfer, null, CancellationToken.None);

        Assert.Single(captured);
    }

    [Fact]
    public async Task Advance_Reversal_ContraPassesTheEntry()
    {
        var (service, captured, _) = BuildService(disbursementEnabled: true);
        var advance = BuildAdvance(150m);

        await service.GenerateEmployeeAdvanceDisbursementEntryAsync(
            advance, "Alice", PaymentMethod.BankTransfer, null, CancellationToken.None);
        var reversalResult = await service.ReverseEmployeeAdvanceDisbursementEntryAsync(
            advance.Id, "Suppression de l'avance", CancellationToken.None);

        Assert.True(reversalResult.IsSuccess, reversalResult.IsFailure ? reversalResult.Error.Description : null);
        Assert.Equal(2, captured.Count);

        var reversal = captured[1];
        Assert.Equal(AccountingService.SourceEmployeeAdvanceCancelled, reversal.SourceEntityType);
        // Sens inversé : 421 au crédit, trésorerie au débit.
        Assert.Equal(150m, reversal.Lines.Where(l => l.AccountNumber == "421").Sum(l => l.CreditAmount.Amount));
        Assert.Equal(150m, reversal.Lines.Where(l => l.AccountNumber == "5321").Sum(l => l.DebitAmount.Amount));
        Assert.True(captured[0].IsReversed);
    }

    [Fact]
    public async Task Advance_Reversal_IsIdempotent()
    {
        var (service, captured, _) = BuildService(disbursementEnabled: true);
        var advance = BuildAdvance();

        await service.GenerateEmployeeAdvanceDisbursementEntryAsync(
            advance, "Alice", PaymentMethod.BankTransfer, null, CancellationToken.None);
        await service.ReverseEmployeeAdvanceDisbursementEntryAsync(advance.Id, "raison", CancellationToken.None);
        await service.ReverseEmployeeAdvanceDisbursementEntryAsync(advance.Id, "raison", CancellationToken.None);

        Assert.Equal(2, captured.Count);
    }

    [Fact]
    public async Task Advance_Reversal_IsNoOp_WhenNoEntryExists()
    {
        var (service, captured, _) = BuildService(disbursementEnabled: true);

        var result = await service.ReverseEmployeeAdvanceDisbursementEntryAsync(
            Guid.NewGuid(), "raison", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(captured);
    }

    // ── Prêt salarié ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Loan_Debits4211_AndCreditsBank()
    {
        var (service, captured, _) = BuildService(disbursementEnabled: true);
        var loan = BuildLoan(1200m);

        var result = await service.GenerateEmployeeLoanDisbursementEntryAsync(
            loan, "Bob Trabelsi", new DateTime(2026, 8, 20), PaymentMethod.BankTransfer, null, CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        var entry = Assert.Single(captured);

        Assert.Equal(AccountingService.SourceEmployeeLoan, entry.SourceEntityType);
        Assert.Equal(new DateTime(2026, 8, 20), entry.EntryDate);
        Assert.Equal("PRET-PRET-2026-001", entry.PieceRef);
        Assert.Equal(1200m, entry.Lines.Where(l => l.AccountNumber == "421.1").Sum(l => l.DebitAmount.Amount));
        Assert.Equal(1200m, entry.Lines.Where(l => l.AccountNumber == "5321").Sum(l => l.CreditAmount.Amount));
    }

    [Fact]
    public async Task Loan_Reversal_ContraPassesTheEntry()
    {
        var (service, captured, _) = BuildService(disbursementEnabled: true);
        var loan = BuildLoan(1200m);

        await service.GenerateEmployeeLoanDisbursementEntryAsync(
            loan, "Bob", new DateTime(2026, 8, 20), PaymentMethod.BankTransfer, null, CancellationToken.None);
        await service.ReverseEmployeeLoanDisbursementEntryAsync(loan.Id, "Annulation", CancellationToken.None);

        Assert.Equal(2, captured.Count);
        Assert.Equal(1200m, captured[1].Lines.Where(l => l.AccountNumber == "421.1").Sum(l => l.CreditAmount.Amount));
    }

    private static BankAccount BankAccountWithGl(string glAccount)
    {
        const string rib = "12345678901234567890";
        var created = BankAccount.Create(
            Guid.NewGuid(), "BIAT", "Banque Internationale Arabe de Tunisie", rib, $"TN59{rib}");
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Description : null);

        var mapping = created.Value.SetChartOfAccountNumber(glAccount);
        Assert.True(mapping.IsSuccess, mapping.IsFailure ? mapping.Error.Description : null);
        return created.Value;
    }
}
