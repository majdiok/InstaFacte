using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.FixedAssets;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// T4/B1 : cession — prorata de l'année de sortie, atomicité (UoW), pré-conditions (exercices
/// antérieurs comptabilisés, ligne de cession non postée), créance 452, cession gratuite.
/// </summary>
public sealed class DisposeFixedAssetCommandHandlerTests
{
    /// <summary>Modèle : PostDepreciationRunCommandHandlerTests — pas de vraie transaction ici,
    /// l'atomicité réelle (enrôlement des contextes) est prouvée par TenantUnitOfWorkTests.</summary>
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

    private static DepreciationRateCategory CreateCategory(decimal rate = 20m) =>
        DepreciationRateCategory.Create(
            "OTHER", "Autres immobilisations", rate, "228", "2828", "68112",
            isNonDepreciable: false, sortOrder: 99);

    private static Mock<ICurrentUser> CurrentUser()
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.Email).Returns("test@factutrust.tn");
        return user;
    }

    private static FixedAsset CreateInServiceAsset(DepreciationRateCategory category, string inventoryNumber = "IMMO-2026-0001")
    {
        var asset = FixedAsset.Create(
            inventoryNumber,
            "Machine test",
            category.Id,
            20m,
            5m,
            "228",
            "2828",
            "68112",
            10_000m,
            0m,
            0m,
            new DateTime(2026, 1, 1)).Value;
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

    /// <summary>Construit un actif « rechargé » (même Id, InService) avec la ligne de cession proratisée.</summary>
    private static FixedAsset BuildReloaded(FixedAsset original, int disposalYear, decimal prorata, params (int year, decimal amount, decimal prior, bool posted)[] lines)
    {
        var clone = FixedAsset.Create(
            original.InventoryNumber,
            original.Label,
            original.DepreciationRateCategoryId,
            original.DepreciationRatePercent,
            original.UsefulLifeYears,
            original.AssetAccountNumber,
            original.DepreciationAccountNumber,
            original.ExpenseAccountNumber,
            original.AcquisitionCost,
            original.CapitalizedFees,
            original.ResidualValue,
            original.AcquisitionDate).Value;
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(clone, original.Id);
        clone.PutInService(original.InServiceDate!.Value, original.CreditAccountNumber!);
        foreach (var (year, amount, prior, posted) in lines)
            clone.AddScheduleLine(MakeLine(clone.Id, year, amount, prior, posted));
        return clone;
    }

    private static readonly DateTime DisposalDate = new(2028, 6, 30);

    // ------------------------------------------------------------------
    // Pré-conditions (avant la transaction — aucune mutation)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Handle_PriorFiscalYearUnposted_ShouldRejectWithExplicitMessage()
    {
        var category = CreateCategory();
        var asset = CreateInServiceAsset(category);
        // 2024 postée, 2025 NON postée (trou), 2026 non postée — cession en 2026.
        asset.AddScheduleLine(MakeLine(asset.Id, 2024, 2_000m, 0m, posted: true));
        asset.AddScheduleLine(MakeLine(asset.Id, 2025, 2_000m, 2_000m, posted: false));
        asset.AddScheduleLine(MakeLine(asset.Id, 2026, 2_000m, 4_000m, posted: false));

        var repo = new Mock<IFixedAssetRepository>();
        repo.Setup(x => x.GetByIdAsync(asset.Id, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);

        var handler = new DisposeFixedAssetCommandHandler(
            repo.Object, new DepreciationEngine(), new Mock<IAccountingService>().Object,
            CurrentUser().Object, new PassthroughTenantUnitOfWork());

        var result = await handler.Handle(
            new DisposeFixedAssetCommand(asset.Id, new DisposeFixedAssetRequest(new DateTime(2026, 6, 30), 5_000m, "5321")),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("antérieurs", result.Error.Description);
        Assert.Contains("2025", result.Error.Description);
        repo.Verify(x => x.ReplaceUnpostedScheduleLinesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(x => x.UpdateAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DisposalYearLineAlreadyPosted_ShouldReject()
    {
        var category = CreateCategory();
        var asset = CreateInServiceAsset(category);
        // 2026, 2027 postées ; 2028 (année de cession) DÉJÀ postée.
        asset.AddScheduleLine(MakeLine(asset.Id, 2026, 2_000m, 0m, posted: true));
        asset.AddScheduleLine(MakeLine(asset.Id, 2027, 2_000m, 2_000m, posted: true));
        asset.AddScheduleLine(MakeLine(asset.Id, 2028, 2_000m, 4_000m, posted: true));

        var repo = new Mock<IFixedAssetRepository>();
        repo.Setup(x => x.GetByIdAsync(asset.Id, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);

        var handler = new DisposeFixedAssetCommandHandler(
            repo.Object, new DepreciationEngine(), new Mock<IAccountingService>().Object,
            CurrentUser().Object, new PassthroughTenantUnitOfWork());

        var result = await handler.Handle(
            new DisposeFixedAssetCommand(asset.Id, new DisposeFixedAssetRequest(DisposalDate, 5_000m, "5321")),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("année de cession", result.Error.Description);
        repo.Verify(x => x.ReplaceUnpostedScheduleLinesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // Validation des comptes de règlement (452 / trésorerie / rebut)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Handle_ProceedsPositive_NoSettlementAccount_ShouldReject()
    {
        var category = CreateCategory();
        var asset = CreateInServiceAsset(category);
        asset.AddScheduleLine(MakeLine(asset.Id, 2026, 2_000m, 0m, posted: true));
        asset.AddScheduleLine(MakeLine(asset.Id, 2027, 2_000m, 2_000m, posted: true));

        var repo = new Mock<IFixedAssetRepository>();
        repo.Setup(x => x.GetByIdAsync(asset.Id, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);

        var handler = new DisposeFixedAssetCommandHandler(
            repo.Object, new DepreciationEngine(), new Mock<IAccountingService>().Object,
            CurrentUser().Object, new PassthroughTenantUnitOfWork());

        var result = await handler.Handle(
            new DisposeFixedAssetCommand(asset.Id, new DisposeFixedAssetRequest(DisposalDate, 5_000m, null, null)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("compte de règlement", result.Error.Description);
    }

    [Fact]
    public async Task Handle_ProceedsPositive_BothAccountsProvided_ShouldReject()
    {
        var category = CreateCategory();
        var asset = CreateInServiceAsset(category);
        asset.AddScheduleLine(MakeLine(asset.Id, 2026, 2_000m, 0m, posted: true));
        asset.AddScheduleLine(MakeLine(asset.Id, 2027, 2_000m, 2_000m, posted: true));

        var repo = new Mock<IFixedAssetRepository>();
        repo.Setup(x => x.GetByIdAsync(asset.Id, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);

        var handler = new DisposeFixedAssetCommandHandler(
            repo.Object, new DepreciationEngine(), new Mock<IAccountingService>().Object,
            CurrentUser().Object, new PassthroughTenantUnitOfWork());

        var result = await handler.Handle(
            new DisposeFixedAssetCommand(asset.Id, new DisposeFixedAssetRequest(DisposalDate, 5_000m, "5321", "452")),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("un seul", result.Error.Description);
    }

    [Fact]
    public async Task Handle_ReceivableNotStartingWith452_ShouldReject()
    {
        var category = CreateCategory();
        var asset = CreateInServiceAsset(category);
        asset.AddScheduleLine(MakeLine(asset.Id, 2026, 2_000m, 0m, posted: true));
        asset.AddScheduleLine(MakeLine(asset.Id, 2027, 2_000m, 2_000m, posted: true));

        var repo = new Mock<IFixedAssetRepository>();
        repo.Setup(x => x.GetByIdAsync(asset.Id, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);

        var handler = new DisposeFixedAssetCommandHandler(
            repo.Object, new DepreciationEngine(), new Mock<IAccountingService>().Object,
            CurrentUser().Object, new PassthroughTenantUnitOfWork());

        var result = await handler.Handle(
            new DisposeFixedAssetCommand(asset.Id, new DisposeFixedAssetRequest(DisposalDate, 5_000m, null, "404")),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("452", result.Error.Description);
    }

    // ------------------------------------------------------------------
    // Nominal : lignes mixtes, prorata, lignes futures supprimées
    // ------------------------------------------------------------------

    [Fact]
    public async Task Handle_NominalMixedLines_ShouldProrateAndPostDepreciationAtDisposalDate()
    {
        var category = CreateCategory();
        var asset = CreateInServiceAsset(category);
        // 2026, 2027 postées (cumul 4 000) ; 2028 (cession) non postée annuité pleine ; 2029 future.
        asset.AddScheduleLine(MakeLine(asset.Id, 2026, 2_000m, 0m, posted: true));
        asset.AddScheduleLine(MakeLine(asset.Id, 2027, 2_000m, 2_000m, posted: true));
        asset.AddScheduleLine(MakeLine(asset.Id, 2028, 2_000m, 4_000m, posted: false));
        asset.AddScheduleLine(MakeLine(asset.Id, 2029, 2_000m, 6_000m, posted: false));

        var reloaded = BuildReloaded(asset, 2028, 1_000m,
            (2026, 2_000m, 0m, true),
            (2027, 2_000m, 2_000m, true),
            (2028, 1_000m, 4_000m, false));  // prorata 180/360 × 2 000 = 1 000

        var repo = new Mock<IFixedAssetRepository>();
        repo.SetupSequence(x => x.GetByIdAsync(asset.Id, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset)
            .ReturnsAsync(reloaded);
        repo.Setup(x => x.ReplaceUnpostedScheduleLinesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(x => x.SaveScheduleLineAsync(It.IsAny<DepreciationScheduleLine>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(x => x.UpdateAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var accounting = new Mock<IAccountingService>();
        accounting.Setup(x => x.GenerateFixedAssetDepreciationEntryAsync(It.IsAny<FixedAsset>(), It.IsAny<DepreciationScheduleLine>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        accounting.Setup(x => x.GenerateFixedAssetDisposalEntryAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var handler = new DisposeFixedAssetCommandHandler(
            repo.Object, new DepreciationEngine(), accounting.Object,
            CurrentUser().Object, new PassthroughTenantUnitOfWork());

        var result = await handler.Handle(
            new DisposeFixedAssetCommand(asset.Id, new DisposeFixedAssetRequest(DisposalDate, 5_000m, "5321")),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);

        // Prorata en place (pas de régénération complète) ; ligne de cession mise à jour.
        repo.Verify(x => x.ReplaceUnpostedScheduleLinesAsync(asset.Id, It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(x => x.ReplaceScheduleLinesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(), It.IsAny<CancellationToken>()), Times.Never);

        // Dotation complémentaire datée à la date de cession (T3).
        accounting.Verify(
            x => x.GenerateFixedAssetDepreciationEntryAsync(
                It.IsAny<FixedAsset>(),
                It.IsAny<DepreciationScheduleLine>(),
                It.Is<DateTime?>(d => d == DisposalDate),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Écriture de sortie + persistance.
        accounting.Verify(x => x.GenerateFixedAssetDisposalEntryAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(x => x.UpdateAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ------------------------------------------------------------------
    // 452 — créance sur cession (règlement à terme)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Handle_Receivable452_ShouldPassReceivableToDisposalEntry()
    {
        var category = CreateCategory();
        var asset = CreateInServiceAsset(category);
        asset.AddScheduleLine(MakeLine(asset.Id, 2026, 2_000m, 0m, posted: true));
        asset.AddScheduleLine(MakeLine(asset.Id, 2027, 2_000m, 2_000m, posted: true));
        asset.AddScheduleLine(MakeLine(asset.Id, 2028, 2_000m, 4_000m, posted: false));

        var reloaded = BuildReloaded(asset, 2028, 1_000m,
            (2026, 2_000m, 0m, true),
            (2027, 2_000m, 2_000m, true),
            (2028, 1_000m, 4_000m, false));

        var repo = new Mock<IFixedAssetRepository>();
        repo.SetupSequence(x => x.GetByIdAsync(asset.Id, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset)
            .ReturnsAsync(reloaded);
        repo.Setup(x => x.ReplaceUnpostedScheduleLinesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(x => x.SaveScheduleLineAsync(It.IsAny<DepreciationScheduleLine>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(x => x.UpdateAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        FixedAsset? captured = null;
        var accounting = new Mock<IAccountingService>();
        accounting.Setup(x => x.GenerateFixedAssetDepreciationEntryAsync(It.IsAny<FixedAsset>(), It.IsAny<DepreciationScheduleLine>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        accounting.Setup(x => x.GenerateFixedAssetDisposalEntryAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()))
            .Callback<FixedAsset, CancellationToken>((a, _) => captured = a)
            .ReturnsAsync(Result.Success());

        var handler = new DisposeFixedAssetCommandHandler(
            repo.Object, new DepreciationEngine(), accounting.Object,
            CurrentUser().Object, new PassthroughTenantUnitOfWork());

        var result = await handler.Handle(
            new DisposeFixedAssetCommand(asset.Id, new DisposeFixedAssetRequest(DisposalDate, 5_000m, null, "452")),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.NotNull(captured);
        Assert.Equal("452", captured!.DisposalReceivableAccount);
        Assert.Null(captured.DisposalTreasuryAccount);
    }

    // ------------------------------------------------------------------
    // Cession gratuite / mise au rebut (produit = 0, aucun compte de règlement)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Handle_ScrapProceedsZero_NoSettlementAccount_ShouldSucceed()
    {
        var category = CreateCategory();
        var asset = CreateInServiceAsset(category);
        asset.AddScheduleLine(MakeLine(asset.Id, 2026, 2_000m, 0m, posted: true));
        asset.AddScheduleLine(MakeLine(asset.Id, 2027, 2_000m, 2_000m, posted: true));
        asset.AddScheduleLine(MakeLine(asset.Id, 2028, 2_000m, 4_000m, posted: false));

        var reloaded = BuildReloaded(asset, 2028, 1_000m,
            (2026, 2_000m, 0m, true),
            (2027, 2_000m, 2_000m, true),
            (2028, 1_000m, 4_000m, false));

        var repo = new Mock<IFixedAssetRepository>();
        repo.SetupSequence(x => x.GetByIdAsync(asset.Id, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset)
            .ReturnsAsync(reloaded);
        repo.Setup(x => x.ReplaceUnpostedScheduleLinesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(x => x.SaveScheduleLineAsync(It.IsAny<DepreciationScheduleLine>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(x => x.UpdateAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        FixedAsset? captured = null;
        var accounting = new Mock<IAccountingService>();
        accounting.Setup(x => x.GenerateFixedAssetDepreciationEntryAsync(It.IsAny<FixedAsset>(), It.IsAny<DepreciationScheduleLine>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        accounting.Setup(x => x.GenerateFixedAssetDisposalEntryAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()))
            .Callback<FixedAsset, CancellationToken>((a, _) => captured = a)
            .ReturnsAsync(Result.Success());

        var handler = new DisposeFixedAssetCommandHandler(
            repo.Object, new DepreciationEngine(), accounting.Object,
            CurrentUser().Object, new PassthroughTenantUnitOfWork());

        var result = await handler.Handle(
            new DisposeFixedAssetCommand(asset.Id, new DisposeFixedAssetRequest(DisposalDate, 0m, null, null)),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.NotNull(captured);
        Assert.Equal(0m, captured!.DisposalProceeds);
        Assert.Null(captured.DisposalTreasuryAccount);
        Assert.Null(captured.DisposalReceivableAccount);
        Assert.Equal(FixedAssetStatus.Disposed, captured.Status);
    }

    // ------------------------------------------------------------------
    // Non-régression : aucune ligne comptabilisée → régénération complète
    // ------------------------------------------------------------------

    [Fact]
    public async Task Handle_NoPostedLines_ShouldRegenerateFullSchedule()
    {
        var category = CreateCategory();
        var asset = CreateInServiceAsset(category);
        // Aucune ligne d'échéancier — la régénération complète (comportement préservé) s'applique.

        var reloaded = BuildReloaded(asset, 2028, 1_000m, (2028, 1_000m, 0m, false));

        var repo = new Mock<IFixedAssetRepository>();
        repo.SetupSequence(x => x.GetByIdAsync(asset.Id, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset)
            .ReturnsAsync(reloaded);
        repo.Setup(x => x.ReplaceScheduleLinesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(x => x.SaveScheduleLineAsync(It.IsAny<DepreciationScheduleLine>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(x => x.UpdateAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var accounting = new Mock<IAccountingService>();
        accounting.Setup(x => x.GenerateFixedAssetDepreciationEntryAsync(It.IsAny<FixedAsset>(), It.IsAny<DepreciationScheduleLine>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        accounting.Setup(x => x.GenerateFixedAssetDisposalEntryAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var handler = new DisposeFixedAssetCommandHandler(
            repo.Object, new DepreciationEngine(), accounting.Object,
            CurrentUser().Object, new PassthroughTenantUnitOfWork());

        var result = await handler.Handle(
            new DisposeFixedAssetCommand(asset.Id, new DisposeFixedAssetRequest(DisposalDate, 5_000m, "5321")),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        // Régénération complète, pas de merge ciblé des lignes non postées.
        repo.Verify(x => x.ReplaceScheduleLinesAsync(asset.Id, It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(x => x.ReplaceUnpostedScheduleLinesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // Rollback tout-ou-rien (finding 1 — BLOQUANT)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Handle_FailureAtLineReplacement_ShouldRollback_NoDepreciationNoDisposal()
    {
        var category = CreateCategory();
        var asset = CreateInServiceAsset(category);
        asset.AddScheduleLine(MakeLine(asset.Id, 2026, 2_000m, 0m, posted: true));
        asset.AddScheduleLine(MakeLine(asset.Id, 2027, 2_000m, 2_000m, posted: true));
        asset.AddScheduleLine(MakeLine(asset.Id, 2028, 2_000m, 4_000m, posted: false));

        var repo = new Mock<IFixedAssetRepository>();
        repo.Setup(x => x.GetByIdAsync(asset.Id, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);
        repo.Setup(x => x.ReplaceUnpostedScheduleLinesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Erreur de persistance simulée"));

        var accounting = new Mock<IAccountingService>();

        var handler = new DisposeFixedAssetCommandHandler(
            repo.Object, new DepreciationEngine(), accounting.Object,
            CurrentUser().Object, new PassthroughTenantUnitOfWork());

        var result = await handler.Handle(
            new DisposeFixedAssetCommand(asset.Id, new DisposeFixedAssetRequest(DisposalDate, 5_000m, "5321")),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        // Aucune écriture, aucune persistance d'actif après l'échec du remplacement.
        accounting.Verify(x => x.GenerateFixedAssetDepreciationEntryAsync(It.IsAny<FixedAsset>(), It.IsAny<DepreciationScheduleLine>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
        accounting.Verify(x => x.GenerateFixedAssetDisposalEntryAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(x => x.UpdateAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_FailureAtDepreciationEntry_ShouldRollback_NoDisposalNoUpdate()
    {
        var category = CreateCategory();
        var asset = CreateInServiceAsset(category);
        asset.AddScheduleLine(MakeLine(asset.Id, 2026, 2_000m, 0m, posted: true));
        asset.AddScheduleLine(MakeLine(asset.Id, 2027, 2_000m, 2_000m, posted: true));
        asset.AddScheduleLine(MakeLine(asset.Id, 2028, 2_000m, 4_000m, posted: false));

        var reloaded = BuildReloaded(asset, 2028, 1_000m,
            (2026, 2_000m, 0m, true),
            (2027, 2_000m, 2_000m, true),
            (2028, 1_000m, 4_000m, false));

        var repo = new Mock<IFixedAssetRepository>();
        repo.SetupSequence(x => x.GetByIdAsync(asset.Id, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset)
            .ReturnsAsync(reloaded);
        repo.Setup(x => x.ReplaceUnpostedScheduleLinesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var accounting = new Mock<IAccountingService>();
        accounting.Setup(x => x.GenerateFixedAssetDepreciationEntryAsync(It.IsAny<FixedAsset>(), It.IsAny<DepreciationScheduleLine>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Validation("Depreciation", "Période clôturée")));

        var handler = new DisposeFixedAssetCommandHandler(
            repo.Object, new DepreciationEngine(), accounting.Object,
            CurrentUser().Object, new PassthroughTenantUnitOfWork());

        var result = await handler.Handle(
            new DisposeFixedAssetCommand(asset.Id, new DisposeFixedAssetRequest(DisposalDate, 5_000m, "5321")),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        // L'échec de la dotation annule l'écriture de sortie et la persistance de l'actif.
        accounting.Verify(x => x.GenerateFixedAssetDisposalEntryAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(x => x.UpdateAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(x => x.SaveScheduleLineAsync(It.IsAny<DepreciationScheduleLine>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_FailureAtDisposalEntry_ShouldRollback()
    {
        var category = CreateCategory();
        var asset = CreateInServiceAsset(category);
        asset.AddScheduleLine(MakeLine(asset.Id, 2026, 2_000m, 0m, posted: true));
        asset.AddScheduleLine(MakeLine(asset.Id, 2027, 2_000m, 2_000m, posted: true));
        asset.AddScheduleLine(MakeLine(asset.Id, 2028, 2_000m, 4_000m, posted: false));

        var reloaded = BuildReloaded(asset, 2028, 1_000m,
            (2026, 2_000m, 0m, true),
            (2027, 2_000m, 2_000m, true),
            (2028, 1_000m, 4_000m, false));

        var repo = new Mock<IFixedAssetRepository>();
        repo.SetupSequence(x => x.GetByIdAsync(asset.Id, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset)
            .ReturnsAsync(reloaded);
        repo.Setup(x => x.ReplaceUnpostedScheduleLinesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(x => x.SaveScheduleLineAsync(It.IsAny<DepreciationScheduleLine>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(x => x.UpdateAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var accounting = new Mock<IAccountingService>();
        accounting.Setup(x => x.GenerateFixedAssetDepreciationEntryAsync(It.IsAny<FixedAsset>(), It.IsAny<DepreciationScheduleLine>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        accounting.Setup(x => x.GenerateFixedAssetDisposalEntryAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Validation("Disposal", "Compte introuvable")));

        var handler = new DisposeFixedAssetCommandHandler(
            repo.Object, new DepreciationEngine(), accounting.Object,
            CurrentUser().Object, new PassthroughTenantUnitOfWork());

        var result = await handler.Handle(
            new DisposeFixedAssetCommand(asset.Id, new DisposeFixedAssetRequest(DisposalDate, 5_000m, "5321")),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        // L'écriture de sortie est la dernière étape — son échec renvoie un Result en échec.
        Assert.Contains("Compte introuvable", result.Error.Description);
    }
}
