using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.FixedAssets;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// T13/C6 — <see cref="GenerateDepreciationScheduleCommandHandler"/> : garde
/// <c>Any(l =&gt; l.IsPosted &amp;&amp; !IsReversed)</c>, dé-postage atomique des lignes extournées +
/// recalcul du cumul + merge du tableau régénéré dans <see cref="ITenantUnitOfWork"/>.
/// Inclut le test bout-en-bout exigé par le finding (comptabilisation → extourne → régénération →
/// recomptabilisation). Modèle : <see cref="DisposeFixedAssetCommandHandlerTests"/>.
/// </summary>
public sealed class GenerateDepreciationScheduleCommandHandlerT13Tests
{
    /// <summary>Modèle : PostDepreciationRunCommandHandlerTests — pas de vraie transaction ici,
    /// l'atomicité réelle est prouvée par TenantUnitOfWorkTests.</summary>
    private sealed class PassthroughTenantUnitOfWork : ITenantUnitOfWork
    {
        public Task<Result> ExecuteAsync(
            Func<CancellationToken, Task<Result>> action,
            CancellationToken cancellationToken = default)
            => action(cancellationToken);

        public Task<Result<T>> ExecuteAsync<T>(
            Func<CancellationToken, Task<Result<T>>> action,
            CancellationToken cancellationToken = default)
            => action(cancellationToken);
    }

    private static DepreciationRateCategory CreateCategory() =>
        DepreciationRateCategory.Create(
            "OTHER", "Autres immobilisations", 20m, "228", "2828", "68112",
            isNonDepreciable: false, sortOrder: 99);

    private static Mock<ICurrentUser> CurrentUser()
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.Email).Returns("test@factutrust.tn");
        return user;
    }

    private static FixedAsset CreateInServiceAsset(DepreciationRateCategory category, string inventoryNumber = "IMMO-2026-T13")
    {
        var asset = FixedAsset.Create(
            inventoryNumber, "Machine test T13", category.Id,
            20m, 5m, "228", "2828", "68112",
            10_000m, 0m, 0m, new DateTime(2026, 1, 1)).Value;
        asset.PutInService(new DateTime(2026, 1, 1), "404");
        return asset;
    }

    /// <summary>Une ligne d'échéancier (annuité pleine 2 000 pour un actif 10 000 à 20 %).</summary>
    private static DepreciationScheduleLine MakeLine(Guid assetId, int year, decimal amount, decimal priorAcc, bool posted)
    {
        const decimal total = 10_000m;
        var line = DepreciationScheduleLine.Create(
            assetId, year, null,
            total - priorAcc,
            2_000m,
            priorAcc,
            amount,
            priorAcc + amount,
            total - priorAcc - amount).Value;
        if (posted)
            line.MarkPosted(Guid.NewGuid(), Guid.NewGuid());
        return line;
    }

    /// <summary>Construit l'état d'extourne pour le mock repository à partir des lignes de l'actif.</summary>
    private static IReadOnlyList<(DepreciationScheduleLine Line, bool IsReversed)> BuildReversalState(
        FixedAsset asset, HashSet<Guid> reversedLineIds)
    {
        return asset.ScheduleLines
            .Select(l => (l, reversedLineIds.Contains(l.Id)))
            .ToList();
    }

    // ==================================================================
    // Garde : ligne postée non extournée → régénération bloquée
    // ==================================================================

    [Fact]
    public async Task Handle_AllPostedNonReversed_StillBlocked()
    {
        var category = CreateCategory();
        var asset = CreateInServiceAsset(category);
        asset.AddScheduleLine(MakeLine(asset.Id, 2026, 2_000m, 0m, posted: true));
        asset.AddScheduleLine(MakeLine(asset.Id, 2027, 2_000m, 2_000m, posted: true));

        // Aucune ligne extournée → IsReversed=false pour toutes.
        var reversalState = BuildReversalState(asset, new HashSet<Guid>());

        var repo = new Mock<IFixedAssetRepository>();
        repo.Setup(x => x.GetByIdAsync(asset.Id, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);
        repo.Setup(x => x.GetScheduleLinesWithReversalStateAsync(asset.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reversalState);

        var handler = new GenerateDepreciationScheduleCommandHandler(
            repo.Object, new DepreciationEngine(), CurrentUser().Object, new PassthroughTenantUnitOfWork());

        var result = await handler.Handle(
            new GenerateDepreciationScheduleCommand(asset.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("non extournées", result.Error.Description);

        // Aucune mutation : ni dé-postage, ni merge, ni mise à jour du cumul.
        repo.Verify(x => x.SaveScheduleLineAsync(It.IsAny<DepreciationScheduleLine>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(x => x.ReplaceScheduleLinesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(x => x.UpdateDepreciationTotalsAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ==================================================================
    // Lignes extournées uniquement → dé-postage + merge + recalcul du cumul
    // ==================================================================

    [Fact]
    public async Task Handle_ReversedOnly_UnpostsMergesAndRecalculatesCumul()
    {
        var category = CreateCategory();
        var asset = CreateInServiceAsset(category);
        var line2026 = MakeLine(asset.Id, 2026, 2_000m, 0m, posted: true);
        var line2027 = MakeLine(asset.Id, 2027, 2_000m, 2_000m, posted: true);
        asset.AddScheduleLine(line2026);
        asset.AddScheduleLine(line2027);
        var originalLineId = line2026.Id;

        // Les deux lignes sont extournées → IsReversed=true.
        var reversedIds = new HashSet<Guid> { line2026.Id, line2027.Id };
        var reversalState = BuildReversalState(asset, reversedIds);

        // Asset rechargé (même instance pour vérifier les mutations en mémoire).
        var repo = new Mock<IFixedAssetRepository>();
        repo.Setup(x => x.GetByIdAsync(asset.Id, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);
        repo.Setup(x => x.GetScheduleLinesWithReversalStateAsync(asset.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reversalState);
        repo.Setup(x => x.SaveScheduleLineAsync(It.IsAny<DepreciationScheduleLine>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(x => x.ReplaceScheduleLinesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(x => x.UpdateDepreciationTotalsAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new GenerateDepreciationScheduleCommandHandler(
            repo.Object, new DepreciationEngine(), CurrentUser().Object, new PassthroughTenantUnitOfWork());

        var result = await handler.Handle(
            new GenerateDepreciationScheduleCommand(asset.Id), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);

        // Dé-postage des 2 lignes extournées (SaveScheduleLineAsync × 2).
        repo.Verify(x => x.SaveScheduleLineAsync(It.IsAny<DepreciationScheduleLine>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        // Merge du tableau régénéré.
        repo.Verify(x => x.ReplaceScheduleLinesAsync(asset.Id, It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(), It.IsAny<CancellationToken>()), Times.Once);
        // Recalcul du cumul (reversedPosted.Count > 0).
        repo.Verify(x => x.UpdateDepreciationTotalsAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()), Times.Once);

        // L'identité de la ligne est préservée (Unpost ne change pas l'Id).
        line2026.Id.Should().Be(originalLineId);
        // Unpost a conservé le JournalEntryId (lien d'audit) et mis IsPosted=false.
        line2026.IsPosted.Should().BeFalse();
        line2026.JournalEntryId.Should().NotBeNull("le lien d'audit vers l'écriture extournée est conservé");
    }

    // ==================================================================
    // Aucune ligne postée → régénération sans écriture sur le cumul de l'actif
    // ==================================================================

    [Fact]
    public async Task Handle_NoPostedLines_RegeneratesWithoutCumulWrite()
    {
        var category = CreateCategory();
        var asset = CreateInServiceAsset(category);
        asset.AddScheduleLine(MakeLine(asset.Id, 2026, 2_000m, 0m, posted: false));
        asset.AddScheduleLine(MakeLine(asset.Id, 2027, 2_000m, 2_000m, posted: false));

        // Aucune ligne postée → IsReversed=false pour toutes (pas d'extourne).
        var reversalState = BuildReversalState(asset, new HashSet<Guid>());

        var repo = new Mock<IFixedAssetRepository>();
        repo.Setup(x => x.GetByIdAsync(asset.Id, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);
        repo.Setup(x => x.GetScheduleLinesWithReversalStateAsync(asset.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reversalState);
        repo.Setup(x => x.ReplaceScheduleLinesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new GenerateDepreciationScheduleCommandHandler(
            repo.Object, new DepreciationEngine(), CurrentUser().Object, new PassthroughTenantUnitOfWork());

        var result = await handler.Handle(
            new GenerateDepreciationScheduleCommand(asset.Id), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);

        // Merge du tableau régénéré.
        repo.Verify(x => x.ReplaceScheduleLinesAsync(asset.Id, It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(), It.IsAny<CancellationToken>()), Times.Once);
        // Aucun dé-postage (pas de ligne postée).
        repo.Verify(x => x.SaveScheduleLineAsync(It.IsAny<DepreciationScheduleLine>(), It.IsAny<CancellationToken>()), Times.Never);
        // Aucune écriture sur le cumul (reversedPosted.Count == 0 → comportement préservé).
        repo.Verify(x => x.UpdateDepreciationTotalsAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ==================================================================
    // Échec pendant la régénération → rollback (pas de mise à jour du cumul)
    // ==================================================================

    [Fact]
    public async Task Handle_FailureAtReplaceScheduleLines_RollsBackNoCumulUpdate()
    {
        var category = CreateCategory();
        var asset = CreateInServiceAsset(category);
        var line2026 = MakeLine(asset.Id, 2026, 2_000m, 0m, posted: true);
        asset.AddScheduleLine(line2026);
        var reversedIds = new HashSet<Guid> { line2026.Id };
        var reversalState = BuildReversalState(asset, reversedIds);

        var repo = new Mock<IFixedAssetRepository>();
        repo.Setup(x => x.GetByIdAsync(asset.Id, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);
        repo.Setup(x => x.GetScheduleLinesWithReversalStateAsync(asset.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reversalState);
        repo.Setup(x => x.SaveScheduleLineAsync(It.IsAny<DepreciationScheduleLine>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        // Échec simulé pendant le merge.
        repo.Setup(x => x.ReplaceScheduleLinesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Erreur de persistance simulée"));

        var handler = new GenerateDepreciationScheduleCommandHandler(
            repo.Object, new DepreciationEngine(), CurrentUser().Object, new PassthroughTenantUnitOfWork());

        var result = await handler.Handle(
            new GenerateDepreciationScheduleCommand(asset.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        // Le cumul n'est jamais mis à jour (l'exception interrompt avant l'étape 3).
        repo.Verify(x => x.UpdateDepreciationTotalsAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ==================================================================
    // Bout en bout (exigé par le finding) :
    // comptabilisation → extourne → régénération (même Id) → recomptabilisation reliée
    // ==================================================================

    /// <summary>Construit un <see cref="AccountingService"/> réel pour la recomptabilisation,
    /// avec la garde « écriture active » (<see cref="IJournalEntryRepository.GetActiveBySourceAsync"/>)
    /// renvoyant <paramref name="activeBySource"/>.</summary>
    private static (AccountingService service, List<JournalEntry> captured) BuildAccountingService(
        JournalEntry? activeBySource = null)
    {
        static ChartOfAccount Acc(string number) =>
            ChartOfAccount.Create(number, $"Compte {number}", int.Parse(number[..1].ToString()), null, AccountNatureType.Debit).Value;

        var chart = new Mock<IChartOfAccountRepository>();
        chart.Setup(x => x.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(50);
        chart.Setup(x => x.GetByAccountNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string n, CancellationToken _) => Acc(n));

        var periodService = new Mock<IAccountingPeriodService>();
        periodService.Setup(x => x.EnsureOpenPeriodAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime d, CancellationToken _) =>
                Result.Success(AccountingPeriod.Create(d.Year, d.Month, new DateTime(d.Year, d.Month, 1), new DateTime(d.Year, d.Month, 1).AddMonths(1).AddDays(-1))));

        var journals = new Mock<IJournalEntryRepository>();
        journals.Setup(x => x.GetActiveBySourceAsync(AccountingService.SourceFixedAssetDepreciation, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeBySource);
        journals.Setup(x => x.ReserveNextEntryNumberAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var captured = new List<JournalEntry>();
        journals.Setup(x => x.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()))
            .Callback<JournalEntry, CancellationToken>((e, _) => captured.Add(e))
            .ReturnsAsync((JournalEntry e, CancellationToken _) => e);

        var withholding = new Mock<IWithholdingTaxRepository>();
        var categories = new Mock<IDepreciationRateCategoryRepository>();
        var ctxFactory = new Mock<ITenantDbContextFactory>();
        var settings = Options.Create(new AccountingSettings { BrouillardEnabled = false });

        var service = new AccountingService(
            chart.Object, periodService.Object, journals.Object, withholding.Object, categories.Object,
            ctxFactory.Object, NullLogger<AccountingService>.Instance, settings);

        return (service, captured);
    }

    /// <summary>Crée une écriture de dotation liée à une ligne d'échéancier.</summary>
    private static JournalEntry MakeDepreciationEntry(int entryNumber, Guid lineId, decimal amount)
    {
        var lines = new[]
        {
            new JournalLineInput("68112", $"Dotation — {lineId}", amount, 0, null, ThirdPartyKind.None),
            new JournalLineInput("2828", $"Amort. — {lineId}", 0, amount, null, ThirdPartyKind.None)
        };
        return JournalEntry.Create(
            entryNumber, "JO", new DateTime(2026, 12, 31), "Dotation amortissement", Guid.NewGuid(),
            isAutoGenerated: true, sourceEntityType: AccountingService.SourceFixedAssetDepreciation,
            sourceEntityId: lineId, lineInputs: lines).Value;
    }

    [Fact]
    public async Task EndToEnd_Comptabilisation_Extourne_Regeneration_Recomptabilisation()
    {
        var category = CreateCategory();
        var asset = CreateInServiceAsset(category);
        var line = MakeLine(asset.Id, 2026, 2_000m, 0m, posted: false);
        asset.AddScheduleLine(line);
        var originalLineId = line.Id;

        // --- 1. Comptabilisation : écriture entry1 créée et liée à la ligne ---
        var entry1 = MakeDepreciationEntry(1, line.Id, 2_000m);
        line.MarkPosted(entry1.Id, Guid.NewGuid());
        line.IsPosted.Should().BeTrue();
        line.JournalEntryId.Should().Be(entry1.Id);

        // --- 2. Extourne : l'écriture entry1 est marquée extournée ---
        entry1.MarkReversedBy(Guid.NewGuid());
        entry1.IsReversed.Should().BeTrue();
        // La ligne reste postée avec JournalEntryId pointant vers l'écriture extournée.
        line.IsPosted.Should().BeTrue();
        line.JournalEntryId.Should().Be(entry1.Id);

        // --- 3. Régénération : le handler dé-poste la ligne extournée et merge ---
        var reversedIds = new HashSet<Guid> { line.Id };
        var reversalState = BuildReversalState(asset, reversedIds);

        var repo = new Mock<IFixedAssetRepository>();
        repo.Setup(x => x.GetByIdAsync(asset.Id, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);
        repo.Setup(x => x.GetScheduleLinesWithReversalStateAsync(asset.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reversalState);
        repo.Setup(x => x.SaveScheduleLineAsync(It.IsAny<DepreciationScheduleLine>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(x => x.ReplaceScheduleLinesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(x => x.UpdateDepreciationTotalsAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new GenerateDepreciationScheduleCommandHandler(
            repo.Object, new DepreciationEngine(), CurrentUser().Object, new PassthroughTenantUnitOfWork());

        var regenResult = await handler.Handle(
            new GenerateDepreciationScheduleCommand(asset.Id), CancellationToken.None);

        Assert.True(regenResult.IsSuccess, regenResult.Error?.Description);

        // Après régénération : la ligne est dé-postée, MÊME Id, JournalEntryId d'audit intact.
        line.Id.Should().Be(originalLineId, "l'identité de la ligne est préservée à travers la régénération");
        line.IsPosted.Should().BeFalse("la ligne extournée a été dé-postée");
        line.JournalEntryId.Should().Be(entry1.Id, "le lien d'audit vers l'écriture extournée est conservé pendant la régénération");

        // --- 4. Recomptabilisation : une NOUVELLE écriture est créée et reliée ---
        // La garde « écriture active » ne voit pas entry1 (extournée) → null → autorise.
        var (accountingService, captured) = BuildAccountingService(activeBySource: null);

        var repostResult = await accountingService.GenerateFixedAssetDepreciationEntryAsync(
            asset, line, cancellationToken: CancellationToken.None);

        Assert.True(repostResult.IsSuccess, repostResult.Error?.Description);

        // Une nouvelle écriture active a été créée.
        var entry2 = Assert.Single(captured);
        entry2.IsReversed.Should().BeFalse("la nouvelle écriture est active");
        entry2.SourceEntityId.Should().Be(line.Id, "même source = même ligne (identité préservée)");

        // La ligne est re-postée et re-liée vers la nouvelle écriture active.
        line.IsPosted.Should().BeTrue();
        line.JournalEntryId.Should().Be(entry2.Id, "le re-lien écrase le JournalEntryId de l'écriture extournée");
        line.Id.Should().Be(originalLineId, "l'identité de la ligne est toujours la même après recomptabilisation");

        // L'écriture extournée reste intacte et rattachée à la même source (piste d'audit complète).
        entry1.IsReversed.Should().BeTrue("l'écriture extournée n'est pas modifiée par la recomptabilisation");
        entry1.SourceEntityId.Should().Be(line.Id, "l'écriture extournée reste rattachée à la même ligne");
        entry1.Id.Should().NotBe(entry2.Id, "deux écritures distinctes : l'extournée + l'active");
    }

    [Fact]
    public async Task EndToEnd_NonReversedPosted_StillBlocked()
    {
        var category = CreateCategory();
        var asset = CreateInServiceAsset(category);
        var line = MakeLine(asset.Id, 2026, 2_000m, 0m, posted: true);
        asset.AddScheduleLine(line);

        // L'écriture est ACTIVE (non extournée) → la régénération doit être bloquée.
        var reversalState = BuildReversalState(asset, new HashSet<Guid>());

        var repo = new Mock<IFixedAssetRepository>();
        repo.Setup(x => x.GetByIdAsync(asset.Id, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);
        repo.Setup(x => x.GetScheduleLinesWithReversalStateAsync(asset.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reversalState);

        var handler = new GenerateDepreciationScheduleCommandHandler(
            repo.Object, new DepreciationEngine(), CurrentUser().Object, new PassthroughTenantUnitOfWork());

        var result = await handler.Handle(
            new GenerateDepreciationScheduleCommand(asset.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("non extournées", result.Error.Description);
        // La ligne reste postée — aucun dé-postage.
        line.IsPosted.Should().BeTrue();
        repo.Verify(x => x.SaveScheduleLineAsync(It.IsAny<DepreciationScheduleLine>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(x => x.ReplaceScheduleLinesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
