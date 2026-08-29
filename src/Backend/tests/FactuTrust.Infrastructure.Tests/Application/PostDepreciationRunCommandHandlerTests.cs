using System.Reflection;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.FixedAssets;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// T7/B4 : garde année future, atomicité par ligne via <see cref="ITenantUnitOfWork"/>,
/// AlreadyPostedCount pour le message de re-run.
/// </summary>
public sealed class PostDepreciationRunCommandHandlerTests
{
    /// <summary>Modèle : UpdateDraftJournalEntryCommandHandlerTests — pas de vraie transaction ici,
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

    private static DepreciationRateCategory CreateCategory(decimal rate = 15m) =>
        DepreciationRateCategory.Create(
            "OTHER", "Autres immobilisations", rate, "228", "2828", "68112",
            isNonDepreciable: false, sortOrder: 99);

    private static FixedAsset CreateInServiceAsset(DepreciationRateCategory category, string inventoryNumber)
    {
        var asset = FixedAsset.Create(
            inventoryNumber,
            "Machine test",
            category.Id,
            category.LegalRatePercent,
            category.UsefulLifeYears,
            "228",
            "2828",
            "68112",
            10_000m,
            0m,
            0m,
            new DateTime(2026, 1, 10)).Value;
        asset.PutInService(new DateTime(2026, 1, 10), "404");
        return asset;
    }

    private static DepreciationScheduleLine CreateLine(FixedAsset asset, int fiscalYear, decimal amount)
    {
        var line = DepreciationScheduleLine.Create(
            asset.Id, fiscalYear, 12, asset.TotalCapitalizedCost, amount, 0m, amount, amount,
            asset.TotalCapitalizedCost - amount).Value;

        // Le nav "FixedAsset" (private set, peuplé par EF via .Include en production) est
        // fixé par réflexion ici — même patron que SqlExceptionHelperTests pour les types dont
        // le constructeur public ne couvre pas les besoins du test.
        typeof(DepreciationScheduleLine).GetProperty(nameof(DepreciationScheduleLine.FixedAsset))!
            .SetValue(line, asset);
        return line;
    }

    [Fact]
    public async Task Handle_FutureFiscalYear_ShouldBeRejected()
    {
        var assets = new Mock<IFixedAssetRepository>();
        var accounting = new Mock<IAccountingService>();
        var handler = new PostDepreciationRunCommandHandler(assets.Object, accounting.Object, new PassthroughTenantUnitOfWork());

        var futureYear = DateTime.UtcNow.Year + 1;
        var result = await handler.Handle(
            new PostDepreciationRunCommand(new PostDepreciationRunRequest(futureYear)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("futur", result.Error.Description);
        assets.Verify(x => x.GetUnpostedScheduleLinesForYearAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CurrentFiscalYear_ShouldNotBeRejectedAsFuture()
    {
        var assets = new Mock<IFixedAssetRepository>();
        assets.Setup(x => x.GetUnpostedScheduleLinesForYearAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DepreciationScheduleLine>());
        assets.Setup(x => x.GetPostedScheduleLineCountForYearAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var accounting = new Mock<IAccountingService>();
        var handler = new PostDepreciationRunCommandHandler(assets.Object, accounting.Object, new PassthroughTenantUnitOfWork());

        var result = await handler.Handle(
            new PostDepreciationRunCommand(new PostDepreciationRunRequest(DateTime.UtcNow.Year)),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
    }

    [Fact]
    public async Task Handle_RerunWithNothingLeftToPost_ReportsAlreadyPostedCount()
    {
        var assets = new Mock<IFixedAssetRepository>();
        assets.Setup(x => x.GetUnpostedScheduleLinesForYearAsync(2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DepreciationScheduleLine>());
        assets.Setup(x => x.GetPostedScheduleLineCountForYearAsync(2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        var accounting = new Mock<IAccountingService>();
        var handler = new PostDepreciationRunCommandHandler(assets.Object, accounting.Object, new PassthroughTenantUnitOfWork());

        var result = await handler.Handle(
            new PostDepreciationRunCommand(new PostDepreciationRunRequest(2026)),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(0, result.Value.PostedCount);
        Assert.Equal(3, result.Value.AlreadyPostedCount);
        accounting.Verify(
            x => x.GenerateFixedAssetDepreciationEntryAsync(It.IsAny<FixedAsset>(), It.IsAny<DepreciationScheduleLine>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_LineFailure_RollsBackOnlyThatLine_OtherLinesStillPosted()
    {
        var category = CreateCategory();
        var assetOk = CreateInServiceAsset(category, "IMMO-2026-0001");
        var assetFailing = CreateInServiceAsset(category, "IMMO-2026-0002");
        var lineOk = CreateLine(assetOk, 2026, 1_000m);
        var lineFailing = CreateLine(assetFailing, 2026, 2_000m);

        var assets = new Mock<IFixedAssetRepository>();
        assets.Setup(x => x.GetUnpostedScheduleLinesForYearAsync(2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { lineOk, lineFailing });
        assets.Setup(x => x.GetPostedScheduleLineCountForYearAsync(2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Échec injecté au niveau de la persistance (SaveScheduleLineAsync) de la ligne "failing"
        // uniquement — mime un échec de la ligne à l'intérieur de la transaction par ligne.
        assets.Setup(x => x.SaveScheduleLineAsync(lineOk, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        assets.Setup(x => x.SaveScheduleLineAsync(lineFailing, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Erreur de persistance simulée"));
        assets.Setup(x => x.UpdateAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var accounting = new Mock<IAccountingService>();
        accounting
            .Setup(x => x.GenerateFixedAssetDepreciationEntryAsync(It.IsAny<FixedAsset>(), It.IsAny<DepreciationScheduleLine>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var handler = new PostDepreciationRunCommandHandler(assets.Object, accounting.Object, new PassthroughTenantUnitOfWork());

        var result = await handler.Handle(
            new PostDepreciationRunCommand(new PostDepreciationRunRequest(2026)),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(1, result.Value.PostedCount);
        Assert.Equal(1, result.Value.SkippedCount);
        Assert.Contains(result.Value.Errors, e => e.Contains("IMMO-2026-0002") && e.Contains("Erreur de persistance simulée"));

        // La ligne en échec n'est jamais mise à jour (UpdateAsset non appelé pour cette ligne
        // faute d'avoir atteint cette étape dans la transaction annulée) tandis que la ligne OK
        // est bien persistée.
        assets.Verify(x => x.SaveScheduleLineAsync(lineOk, It.IsAny<CancellationToken>()), Times.Once);
        assets.Verify(x => x.UpdateAsync(assetOk, It.IsAny<CancellationToken>()), Times.Once);
        assets.Verify(x => x.UpdateAsync(assetFailing, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NominalRun_PostsAllLinesAndComputesTotal()
    {
        var category = CreateCategory();
        var asset1 = CreateInServiceAsset(category, "IMMO-2026-0001");
        var asset2 = CreateInServiceAsset(category, "IMMO-2026-0002");
        var line1 = CreateLine(asset1, 2026, 1_000m);
        var line2 = CreateLine(asset2, 2026, 1_500m);

        var assets = new Mock<IFixedAssetRepository>();
        assets.Setup(x => x.GetUnpostedScheduleLinesForYearAsync(2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { line1, line2 });
        assets.Setup(x => x.GetPostedScheduleLineCountForYearAsync(2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        assets.Setup(x => x.SaveScheduleLineAsync(It.IsAny<DepreciationScheduleLine>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        assets.Setup(x => x.UpdateAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var accounting = new Mock<IAccountingService>();
        accounting
            .Setup(x => x.GenerateFixedAssetDepreciationEntryAsync(It.IsAny<FixedAsset>(), It.IsAny<DepreciationScheduleLine>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var handler = new PostDepreciationRunCommandHandler(assets.Object, accounting.Object, new PassthroughTenantUnitOfWork());

        var result = await handler.Handle(
            new PostDepreciationRunCommand(new PostDepreciationRunRequest(2026)),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(2, result.Value.PostedCount);
        Assert.Equal(0, result.Value.SkippedCount);
        Assert.Equal(2_500m, result.Value.TotalDepreciationAmount);
        Assert.Equal(2, result.Value.AlreadyPostedCount);
        Assert.Empty(result.Value.Errors);
    }
}
