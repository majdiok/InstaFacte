using FactuTrust.Application.Common.Fiscal;
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
/// P3 « Exercices décalés » — run de dotations (<see cref="PostDepreciationRunCommandHandler"/>) :
/// garde anti-futur par exercice (et non année civile), date d'écriture = fin d'exercice,
/// libellé d'exercice dans le résultat. Parité stricte exercice civil (31/12, libellé « N »).
/// </summary>
public sealed class PostDepreciationRunOffsetFiscalYearTests
{
    /// <summary>Modèle : PostDepreciationRunCommandHandlerTests — pas de vraie transaction ici.</summary>
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
            "OTHER", "Autres immobilisations", 15m, "228", "2828", "68112",
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
        typeof(DepreciationScheduleLine).GetProperty(nameof(DepreciationScheduleLine.FixedAsset))!
            .SetValue(line, asset);
        return line;
    }

    private static Mock<IFixedAssetSettingsRepository> CreateOffsetSettingsRepo(int startMonth, string labelFormat)
    {
        var repo = new Mock<IFixedAssetSettingsRepository>();
        repo.Setup(x => x.GetForTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(FixedAssetSettings.Create(startMonth, labelFormat).Value);
        return repo;
    }

    [Fact]
    public async Task Handle_OffsetFiscalYear_FutureFiscalYear_IsRejected()
    {
        var settingsRepo = CreateOffsetSettingsRepo(7, FixedAssetSettings.LabelFormatNn1);
        var assets = new Mock<IFixedAssetRepository>();
        var accounting = new Mock<IAccountingService>();
        var handler = new PostDepreciationRunCommandHandler(
            assets.Object, accounting.Object, new PassthroughTenantUnitOfWork(), settingsRepo.Object);

        // Exercice courant (décalé juillet→juin) selon la date réelle, +1 = exercice futur.
        var currentFiscalYear = FiscalYearMath.Key(DateTime.UtcNow, 7);
        var futureFiscalYear = currentFiscalYear + 1;

        var result = await handler.Handle(
            new PostDepreciationRunCommand(new PostDepreciationRunRequest(futureFiscalYear)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("futur", result.Error.Description);
        assets.Verify(x => x.GetUnpostedScheduleLinesForYearAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OffsetFiscalYear_CurrentFiscalYear_IsAccepted()
    {
        var settingsRepo = CreateOffsetSettingsRepo(7, FixedAssetSettings.LabelFormatNn1);
        var assets = new Mock<IFixedAssetRepository>();
        assets.Setup(x => x.GetUnpostedScheduleLinesForYearAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DepreciationScheduleLine>());
        assets.Setup(x => x.GetPostedScheduleLineCountForYearAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var accounting = new Mock<IAccountingService>();
        var handler = new PostDepreciationRunCommandHandler(
            assets.Object, accounting.Object, new PassthroughTenantUnitOfWork(), settingsRepo.Object);

        var currentFiscalYear = FiscalYearMath.Key(DateTime.UtcNow, 7);

        var result = await handler.Handle(
            new PostDepreciationRunCommand(new PostDepreciationRunRequest(currentFiscalYear)),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
    }

    [Fact]
    public async Task Handle_OffsetFiscalYear_EntryDateIsFiscalYearEnd_AndLabelIsNn1()
    {
        const int startMonth = 7;
        var settingsRepo = CreateOffsetSettingsRepo(startMonth, FixedAssetSettings.LabelFormatNn1);
        var category = CreateCategory();
        var asset = CreateInServiceAsset(category, "IMMO-2026-0001");
        var currentFiscalYear = FiscalYearMath.Key(DateTime.UtcNow, startMonth);
        // Exercice courant (passé la garde anti-futur) : la dotation doit être datée en fin d'exercice.
        var year = currentFiscalYear;
        var line = CreateLine(asset, year, 1_000m);

        var assets = new Mock<IFixedAssetRepository>();
        assets.Setup(x => x.GetUnpostedScheduleLinesForYearAsync(year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { line });
        assets.Setup(x => x.GetPostedScheduleLineCountForYearAsync(year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        assets.Setup(x => x.SaveScheduleLineAsync(It.IsAny<DepreciationScheduleLine>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        assets.Setup(x => x.UpdateAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        DateTime? capturedEntryDate = null;
        var accounting = new Mock<IAccountingService>();
        accounting
            .Setup(x => x.GenerateFixedAssetDepreciationEntryAsync(It.IsAny<FixedAsset>(), It.IsAny<DepreciationScheduleLine>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Callback<FixedAsset, DepreciationScheduleLine, DateTime?, CancellationToken>((_, _, d, _) => capturedEntryDate = d)
            .ReturnsAsync(Result.Success());

        var handler = new PostDepreciationRunCommandHandler(
            assets.Object, accounting.Object, new PassthroughTenantUnitOfWork(), settingsRepo.Object);

        var result = await handler.Handle(
            new PostDepreciationRunCommand(new PostDepreciationRunRequest(year)),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(1, result.Value.PostedCount);
        // Fin d'exercice juillet→juin : 30/06 de l'année suivante.
        Assert.Equal(FiscalYearMath.EndDateTime(year, startMonth), capturedEntryDate);
        Assert.Equal($"{year}/{year + 1}", result.Value.FiscalYearLabel);
    }

    [Fact]
    public async Task Handle_CivilFiscalYear_EntryDateIsDecember31_AndLabelIsYear_Parity()
    {
        // Aucun dépôt de paramètres → défaut usine (exercice civil, mois 1). Parité stricte avec le
        // comportement historique : écriture datée au 31/12 de l'exercice, libellé « N ».
        var category = CreateCategory();
        var asset = CreateInServiceAsset(category, "IMMO-2026-0001");
        var year = DateTime.UtcNow.Year;
        var line = CreateLine(asset, year, 1_000m);

        var assets = new Mock<IFixedAssetRepository>();
        assets.Setup(x => x.GetUnpostedScheduleLinesForYearAsync(year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { line });
        assets.Setup(x => x.GetPostedScheduleLineCountForYearAsync(year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        assets.Setup(x => x.SaveScheduleLineAsync(It.IsAny<DepreciationScheduleLine>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        assets.Setup(x => x.UpdateAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        DateTime? capturedEntryDate = null;
        var accounting = new Mock<IAccountingService>();
        accounting
            .Setup(x => x.GenerateFixedAssetDepreciationEntryAsync(It.IsAny<FixedAsset>(), It.IsAny<DepreciationScheduleLine>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Callback<FixedAsset, DepreciationScheduleLine, DateTime?, CancellationToken>((_, _, d, _) => capturedEntryDate = d)
            .ReturnsAsync(Result.Success());

        // Pas de dépôt de paramètres (4ᵉ argument omis) → comportement civil par défaut.
        var handler = new PostDepreciationRunCommandHandler(
            assets.Object, accounting.Object, new PassthroughTenantUnitOfWork());

        var result = await handler.Handle(
            new PostDepreciationRunCommand(new PostDepreciationRunRequest(year)),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(new DateTime(year, 12, 31), capturedEntryDate);
        Assert.Equal(year.ToString(), result.Value.FiscalYearLabel);
    }

    [Fact]
    public async Task Handle_OffsetFiscalYear_LabelNFormat_UsesStartYearOnly()
    {
        // Format « N » sur exercice décalé → libellé = année de début seule (décision D2).
        const int startMonth = 7;
        var settingsRepo = CreateOffsetSettingsRepo(startMonth, FixedAssetSettings.LabelFormatN);
        var assets = new Mock<IFixedAssetRepository>();
        assets.Setup(x => x.GetUnpostedScheduleLinesForYearAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DepreciationScheduleLine>());
        assets.Setup(x => x.GetPostedScheduleLineCountForYearAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        var accounting = new Mock<IAccountingService>();
        var handler = new PostDepreciationRunCommandHandler(
            assets.Object, accounting.Object, new PassthroughTenantUnitOfWork(), settingsRepo.Object);

        var year = FiscalYearMath.Key(DateTime.UtcNow, startMonth);

        var result = await handler.Handle(
            new PostDepreciationRunCommand(new PostDepreciationRunRequest(year)),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(year.ToString(), result.Value.FiscalYearLabel);
        Assert.Equal(2, result.Value.AlreadyPostedCount);
    }
}
