using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// T15 — verrouillage de la comptabilisation de cession d'immobilisation (générateur déjà conforme,
/// §1.2 DÉFAUT #1 OBSOLÈTE). Plus-value → crédit 736 ; moins-value → débit 636 ; reprise d'amortissements
/// au débit du compte d'amortissement ; sortie du brut au crédit du compte d'immobilisation ; idempotence
/// par <c>SourceFixedAssetDisposal</c> ; journal des immobilisations (<c>JIM</c>) ; statut <c>Validee</c>
/// même avec <c>BrouillardEnabled</c> désactivé. Aucun changement de code de production.
/// </summary>
public sealed class FixedAssetDisposalEntryTests
{
    private static ChartOfAccount Acc(string number) =>
        ChartOfAccount.Create(number, $"Compte {number}", int.Parse(number[..1].ToString()), null, AccountNatureType.Debit).Value;

    private static (AccountingService service, Mock<IJournalEntryRepository> journals, List<JournalEntry> captured) BuildService(
        JournalEntry? existingBySource = null, bool brouillardEnabled = false)
    {
        var chart = new Mock<IChartOfAccountRepository>();
        chart.Setup(x => x.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(50);
        chart.Setup(x => x.GetByAccountNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string n, CancellationToken _) => Acc(n));

        var periodService = new Mock<IAccountingPeriodService>();
        var period = AccountingPeriod.Create(2026, 6, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30));
        periodService.Setup(x => x.EnsureOpenPeriodAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(period));

        var journals = new Mock<IJournalEntryRepository>();
        journals.Setup(x => x.GetBySourceAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingBySource);
        journals.Setup(x => x.ReserveNextEntryNumberAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);
        var captured = new List<JournalEntry>();
        journals.Setup(x => x.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()))
            .Callback<JournalEntry, CancellationToken>((e, _) => captured.Add(e))
            .ReturnsAsync((JournalEntry e, CancellationToken _) => e);

        var withholding = new Mock<IWithholdingTaxRepository>();
        var categories = new Mock<IDepreciationRateCategoryRepository>();
        var ctxFactory = new Mock<ITenantDbContextFactory>();
        var settings = Options.Create(new AccountingSettings { BrouillardEnabled = brouillardEnabled });

        var service = new AccountingService(
            chart.Object, periodService.Object, journals.Object, withholding.Object, categories.Object,
            ctxFactory.Object, NullLogger<AccountingService>.Instance, settings);

        return (service, journals, captured);
    }

    /// <summary>
    /// Immobilisation amortie : brut 100, amortissements cumulés 60, VNC 40, prix de cession 50 → plus-value 10.
    /// Comptes du plan SCE : 221 terrains, 2821 amort. constructions, 532 banque (trésorerie de cession).
    /// </summary>
    private static FixedAsset DisposedAsset(decimal gross, decimal accumulated, decimal proceeds)
    {
        var asset = FixedAsset.Create(
            "IMMO-CESSION-0001",
            "Construction test",
            Guid.NewGuid(),
            5m,
            20m,
            "221",
            "2821",
            "6811",
            gross,
            0m,
            0m,
            new DateTime(2024, 1, 10)).Value;

        asset.PutInService(new DateTime(2024, 1, 15), "404");
        // Simule l'amortissement constaté jusqu'à la cession.
        asset.ApplyDepreciation(accumulated, accumulated, gross - accumulated);

        var dispose = asset.Dispose(new DateTime(2026, 6, 20), proceeds, "532");
        Assert.True(dispose.IsSuccess, dispose.IsFailure ? dispose.Error.Description : null);
        return asset;
    }

    private static decimal Debit(JournalEntry e, string acc) =>
        e.Lines.Where(l => l.AccountNumber == acc).Sum(l => l.DebitAmount.Amount);

    private static decimal Credit(JournalEntry e, string acc) =>
        e.Lines.Where(l => l.AccountNumber == acc).Sum(l => l.CreditAmount.Amount);

    // ── Plus-value de cession : crédit 736 ──────────────────────────────────────────────────

    [Fact]
    public async Task Disposal_Gain_Credits736_AndReversesDepreciation_AndWritesOffAsset()
    {
        // Brut 100, amort. 60, VNC 40, prix 50 → plus-value 10.
        var asset = DisposedAsset(gross: 100m, accumulated: 60m, proceeds: 50m);
        var (service, _, captured) = BuildService();

        var result = await service.GenerateFixedAssetDisposalEntryAsync(asset, CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        var entry = Assert.Single(captured);

        // Journal des immobilisations.
        Assert.Equal(AccountingService.FixedAssetJournalCode, entry.JournalCode);
        Assert.Equal(asset.DisposalDate, entry.EntryDate);
        // Écriture d'impôt/source : source de cession + idempotence.
        Assert.Equal(AccountingService.SourceFixedAssetDisposal, entry.SourceEntityType);
        Assert.Equal(asset.Id, entry.SourceEntityId);

        // Reprise d'amortissements au DÉBIT du compte d'amortissement (2821 = 60).
        Assert.Equal(60m, Debit(entry, "2821"));
        // Encaissement au DÉBIT de la trésorerie (532 = 50).
        Assert.Equal(50m, Debit(entry, "532"));
        // Sortie du brut au CRÉDIT du compte d'immobilisation (221 = 100).
        Assert.Equal(100m, Credit(entry, "221"));
        // Plus-value au CRÉDIT de 736 (10) — jamais 636.
        Assert.Equal(10m, Credit(entry, "736"));
        Assert.Equal(0m, Debit(entry, "636"));

        // Équilibre : débits (60 + 50) = crédits (100 + 10) = 110.
        var totalDebit = entry.Lines.Sum(l => l.DebitAmount.Amount);
        var totalCredit = entry.Lines.Sum(l => l.CreditAmount.Amount);
        Assert.Equal(totalDebit, totalCredit);
    }

    // ── Moins-value de cession : débit 636 ──────────────────────────────────────────────────

    [Fact]
    public async Task Disposal_Loss_Debits636_AndReversesDepreciation_AndWritesOffAsset()
    {
        // Brut 100, amort. 60, VNC 40, prix 25 → moins-value 15.
        var asset = DisposedAsset(gross: 100m, accumulated: 60m, proceeds: 25m);
        var (service, _, captured) = BuildService();

        var result = await service.GenerateFixedAssetDisposalEntryAsync(asset, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(captured);
        Assert.Equal(AccountingService.FixedAssetJournalCode, entry.JournalCode);

        Assert.Equal(60m, Debit(entry, "2821"));
        Assert.Equal(25m, Debit(entry, "532"));
        Assert.Equal(100m, Credit(entry, "221"));
        // Moins-value au DÉBIT de 636 (15) — jamais 736.
        Assert.Equal(15m, Debit(entry, "636"));
        Assert.Equal(0m, Credit(entry, "736"));

        var totalDebit = entry.Lines.Sum(l => l.DebitAmount.Amount);
        var totalCredit = entry.Lines.Sum(l => l.CreditAmount.Amount);
        // débits (60 + 25 + 15) = crédits (100) = 100.
        Assert.Equal(totalDebit, totalCredit);
    }

    // ── Cession à prix nul : sortie du brut sans trésorerie ni résultat de cession (VNC = brute) ──

    [Fact]
    public async Task Disposal_ZeroProceeds_NoTreasuryLine_NoGainOrLossLine()
    {
        // Brut 100, amort. 100, VNC 0, prix 0 → ni plus-value ni moins-value.
        var asset = DisposedAsset(gross: 100m, accumulated: 100m, proceeds: 0m);
        var (service, _, captured) = BuildService();

        var result = await service.GenerateFixedAssetDisposalEntryAsync(asset, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(captured);

        Assert.Equal(100m, Debit(entry, "2821"));
        Assert.Equal(100m, Credit(entry, "221"));
        // Aucune ligne de trésorerie (prix = 0) ni de résultat de cession.
        Assert.Equal(0m, Debit(entry, "532"));
        Assert.Equal(0m, Credit(entry, "736"));
        Assert.Equal(0m, Debit(entry, "636"));
    }

    // ── Idempotence : un rejeu ne crée pas de doublon ───────────────────────────────────────

    [Fact]
    public async Task Disposal_AlreadyPosted_IsIdempotent_NoDuplicateEntry()
    {
        var asset = DisposedAsset(gross: 100m, accumulated: 60m, proceeds: 50m);
        // Une écriture source existe déjà (premier passage) : le générateur court-circuite.
        // L'écriture doit être ÉQUILIBRÉE pour que JournalEntry.Create réussisse.
        var existing = JournalEntry.Create(
            1, AccountingService.FixedAssetJournalCode, asset.DisposalDate!.Value, "Cession précédente",
            Guid.NewGuid(), true, AccountingService.SourceFixedAssetDisposal, asset.Id,
            new[]
            {
                new JournalLineInput("2821", "Reprise amort.", 60m, 0m, null, ThirdPartyKind.None),
                new JournalLineInput("532", "Produit cession", 50m, 0m, null, ThirdPartyKind.None),
                new JournalLineInput("221", "Sortie immobilisation", 0m, 100m, null, ThirdPartyKind.None),
                new JournalLineInput("736", "Plus-value cession", 0m, 10m, null, ThirdPartyKind.None)
            }).Value;

        var (service, journals, captured) = BuildService(existingBySource: existing);

        var result = await service.GenerateFixedAssetDisposalEntryAsync(asset, CancellationToken.None);

        // Succès sans nouvelle écriture (rejeu sûr).
        Assert.True(result.IsSuccess);
        Assert.Empty(captured);
        journals.Verify(
            x => x.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Cession incomplète (sans date ni compte de trésorerie) : refus ───────────────────────

    [Fact]
    public async Task Disposal_Incomplete_ReturnsFailure()
    {
        // Immobilisation non cédée (DisposalDate null, DisposalTreasuryAccount null).
        var asset = FixedAsset.Create(
            "IMMO-CESSION-0002", "Machine", Guid.NewGuid(), 10m, 10m,
            "228", "2828", "68112", 10_000m, 0m, 0m, new DateTime(2026, 1, 10)).Value;
        asset.PutInService(new DateTime(2026, 1, 15), "404");

        var (service, _, captured) = BuildService();

        var result = await service.GenerateFixedAssetDisposalEntryAsync(asset, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(captured);
    }

    // ── Plan comptable vide : la comptabilisation est ignorée (aucune écriture) ──────────────

    [Fact]
    public async Task Disposal_EmptyChartOfAccounts_IsNoOpSuccess()
    {
        var asset = DisposedAsset(gross: 100m, accumulated: 60m, proceeds: 50m);

        var chart = new Mock<IChartOfAccountRepository>();
        chart.Setup(x => x.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var journals = new Mock<IJournalEntryRepository>();
        var captured = new List<JournalEntry>();
        journals.Setup(x => x.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()))
            .Callback<JournalEntry, CancellationToken>((e, _) => captured.Add(e))
            .ReturnsAsync((JournalEntry e, CancellationToken _) => e);

        var periodService = new Mock<IAccountingPeriodService>();
        var withholding = new Mock<IWithholdingTaxRepository>();
        var categories = new Mock<IDepreciationRateCategoryRepository>();
        var ctxFactory = new Mock<ITenantDbContextFactory>();
        var settings = Options.Create(new AccountingSettings());

        var service = new AccountingService(
            chart.Object, periodService.Object, journals.Object, withholding.Object, categories.Object,
            ctxFactory.Object, NullLogger<AccountingService>.Instance, settings);

        var result = await service.GenerateFixedAssetDisposalEntryAsync(asset, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(captured);
    }
}
