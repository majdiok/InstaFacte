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

public sealed class FixedAssetCommandHandlersTests
{
    private static DepreciationRateCategory CreateCategory(decimal rate = 15m) =>
        DepreciationRateCategory.Create(
            "OTHER", "Autres immobilisations", rate, "218", "2818", "6818",
            isNonDepreciable: false, sortOrder: 99);

    private static FixedAsset CreateDraftAsset(
        DepreciationRateCategory category,
        Guid? supplierInvoiceId = null)
    {
        var asset = FixedAsset.Create(
            "IMMO-2026-0001",
            "Machine test",
            category.Id,
            category.LegalRatePercent,
            category.UsefulLifeYears,
            "218",
            "2818",
            "6818",
            10_000m,
            0m,
            0m,
            new DateTime(2026, 1, 10)).Value;

        if (supplierInvoiceId is not null)
            asset.LinkSupplierInvoiceSource(supplierInvoiceId.Value, Guid.NewGuid());

        return asset;
    }

    private static Mock<ICurrentUser> CurrentUser()
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.Email).Returns("test@factutrust.tn");
        return user;
    }

    // ------------------------------------------------------------------
    // Mise en service
    // ------------------------------------------------------------------

    [Fact]
    public async Task PutInService_ManualAsset_ShouldGenerateAcquisitionEntryAndSchedule()
    {
        var category = CreateCategory();
        var asset = CreateDraftAsset(category);

        var repo = new Mock<IFixedAssetRepository>();
        repo.Setup(x => x.GetByIdAsync(asset.Id, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);
        repo.Setup(x => x.PutInServiceInTransactionAsync(
                asset.Id,
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, DateTime _, string _, IReadOnlyList<DepreciationScheduleLine>? _, string _, CancellationToken _) =>
            {
                asset.PutInService(new DateTime(2026, 3, 1), "404");
                return Result.Success(asset);
            });

        var accounting = new Mock<IAccountingService>();
        accounting
            .Setup(x => x.GenerateFixedAssetAcquisitionEntryAsync(asset, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var handler = new PutFixedAssetInServiceCommandHandler(
            repo.Object, accounting.Object, new DepreciationEngine(), CurrentUser().Object);

        var result = await handler.Handle(
            new PutFixedAssetInServiceCommand(asset.Id, new PutFixedAssetInServiceRequest(new DateTime(2026, 3, 1), "404")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        accounting.Verify(
            x => x.GenerateFixedAssetAcquisitionEntryAsync(asset, It.IsAny<CancellationToken>()),
            Times.Once);
        repo.Verify(
            x => x.PutInServiceInTransactionAsync(
                asset.Id,
                It.IsAny<DateTime>(),
                "404",
                It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PutInService_AlreadyInServiceFromTransaction_ShouldStillCallAccountingOnce()
    {
        var category = CreateCategory();
        var draftPreview = CreateDraftAsset(category);
        var inServiceAsset = CreateDraftAsset(category);
        inServiceAsset.PutInService(new DateTime(2026, 3, 1), "404");

        var repo = new Mock<IFixedAssetRepository>();
        repo.Setup(x => x.GetByIdAsync(inServiceAsset.Id, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draftPreview);
        repo.Setup(x => x.PutInServiceInTransactionAsync(
                inServiceAsset.Id,
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(inServiceAsset));

        var accounting = new Mock<IAccountingService>();
        accounting
            .Setup(x => x.GenerateFixedAssetAcquisitionEntryAsync(inServiceAsset, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var handler = new PutFixedAssetInServiceCommandHandler(
            repo.Object, accounting.Object, new DepreciationEngine(), CurrentUser().Object);

        var result = await handler.Handle(
            new PutFixedAssetInServiceCommand(inServiceAsset.Id, new PutFixedAssetInServiceRequest(new DateTime(2026, 3, 1), "404")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        accounting.Verify(
            x => x.GenerateFixedAssetAcquisitionEntryAsync(inServiceAsset, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PutInService_SupplierInvoiceAsset_ShouldNotGenerateAcquisitionEntry()
    {
        var category = CreateCategory();
        var asset = CreateDraftAsset(category, supplierInvoiceId: Guid.NewGuid());

        var repo = new Mock<IFixedAssetRepository>();
        repo.Setup(x => x.GetByIdAsync(asset.Id, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);
        repo.Setup(x => x.PutInServiceInTransactionAsync(
                asset.Id,
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, DateTime _, string _, IReadOnlyList<DepreciationScheduleLine>? _, string _, CancellationToken _) =>
            {
                asset.PutInService(new DateTime(2026, 3, 1), "404");
                return Result.Success(asset);
            });

        var accounting = new Mock<IAccountingService>();

        var handler = new PutFixedAssetInServiceCommandHandler(
            repo.Object, accounting.Object, new DepreciationEngine(), CurrentUser().Object);

        var result = await handler.Handle(
            new PutFixedAssetInServiceCommand(asset.Id, new PutFixedAssetInServiceRequest(new DateTime(2026, 3, 1), "404")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        accounting.Verify(
            x => x.GenerateFixedAssetAcquisitionEntryAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()),
            Times.Never);
        repo.Verify(
            x => x.PutInServiceInTransactionAsync(
                asset.Id,
                It.IsAny<DateTime>(),
                "404",
                It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ------------------------------------------------------------------
    // Modification de brouillon
    // ------------------------------------------------------------------

    [Fact]
    public async Task UpdateDraft_ShouldApplyChangesAndOverrides()
    {
        var category = CreateCategory(rate: 15m);
        var asset = CreateDraftAsset(category);

        var repo = new Mock<IFixedAssetRepository>();
        repo.Setup(x => x.GetByIdAsync(asset.Id, false, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);

        var categories = new Mock<IDepreciationRateCategoryRepository>();
        categories.Setup(x => x.GetByIdAsync(category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        var handler = new UpdateFixedAssetCommandHandler(repo.Object, categories.Object, CurrentUser().Object);

        var request = new UpdateFixedAssetRequest(
            "Machine modifiée",
            12_000m,
            500m,
            1_000m,
            new DateTime(2026, 2, 1),
            "Desc",
            "Atelier 2",
            null,
            null,
            null,
            DepreciationMethod: DepreciationMethod.Accelerated,
            AccelerationCoefficient: 2m,
            DepreciationRatePercent: 10m,
            VatAmount: 2_280m);

        var result = await handler.Handle(new UpdateFixedAssetCommand(asset.Id, request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Machine modifiée", asset.Label);
        Assert.Equal(12_000m, asset.AcquisitionCost);
        Assert.Equal(500m, asset.CapitalizedFees);
        Assert.Equal(1_000m, asset.ResidualValue);
        Assert.Equal(DepreciationMethod.Accelerated, asset.DepreciationMethod);
        Assert.Equal(2m, asset.AccelerationCoefficient);
        Assert.Equal(10m, asset.DepreciationRatePercent);
        Assert.Equal(10m, asset.UsefulLifeYears); // 100 / 10 %
        Assert.Equal(2_280m, asset.VatAmount);
        repo.Verify(x => x.UpdateAsync(asset, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateDraft_WhenAssetInService_ShouldFail()
    {
        var category = CreateCategory();
        var asset = CreateDraftAsset(category);
        asset.PutInService(new DateTime(2026, 3, 1), "404");

        var repo = new Mock<IFixedAssetRepository>();
        repo.Setup(x => x.GetByIdAsync(asset.Id, false, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);

        var categories = new Mock<IDepreciationRateCategoryRepository>();
        categories.Setup(x => x.GetByIdAsync(category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        var handler = new UpdateFixedAssetCommandHandler(repo.Object, categories.Object, CurrentUser().Object);

        var request = new UpdateFixedAssetRequest(
            "Machine modifiée", 12_000m, 0m, 0m, new DateTime(2026, 2, 1), null, null, null, null, null);

        var result = await handler.Handle(new UpdateFixedAssetCommand(asset.Id, request), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("brouillon", result.Error.Description);
        repo.Verify(x => x.UpdateAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // Simulation du tableau (preview)
    // ------------------------------------------------------------------

    [Fact]
    public async Task PreviewSchedule_DraftAsset_ShouldSimulateWithoutPersisting()
    {
        var category = CreateCategory();
        var asset = CreateDraftAsset(category);

        var repo = new Mock<IFixedAssetRepository>();
        repo.Setup(x => x.GetByIdAsync(asset.Id, false, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);

        var handler = new PreviewDepreciationScheduleQueryHandler(repo.Object, new DepreciationEngine());

        var result = await handler.Handle(
            new PreviewDepreciationScheduleQuery(asset.Id, new DateTime(2026, 10, 1)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value.Lines);
        Assert.Equal(asset.DepreciableBase, result.Value.Lines.Sum(l => l.DepreciationAmount));
        repo.Verify(x => x.UpdateAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(
            x => x.ReplaceScheduleLinesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<DepreciationScheduleLine>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ------------------------------------------------------------------
    // Résolution taux / durée
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(null, null, 15.0, 6.67)]   // catégorie par défaut
    [InlineData(10.0, null, 10.0, 10.0)]   // surcharge taux → durée dérivée
    [InlineData(null, 4.0, 25.0, 4.0)]     // surcharge durée → taux dérivé
    public void RateResolver_ShouldSynchronizeRateAndLife(
        double? overrideRate, double? overrideLife, double expectedRate, double expectedLife)
    {
        var result = FixedAssetRateResolver.Resolve(
            isNonDepreciable: false,
            categoryRatePercent: 15m,
            categoryLifeYears: 6.67m,
            overrideRatePercent: (decimal?)overrideRate,
            overrideLifeYears: (decimal?)overrideLife);

        Assert.True(result.IsSuccess);
        Assert.Equal((decimal)expectedRate, result.Value.RatePercent);
        Assert.Equal((decimal)expectedLife, result.Value.LifeYears);
    }

    [Fact]
    public void RateResolver_InvalidRate_ShouldFail()
    {
        var result = FixedAssetRateResolver.Resolve(false, 15m, 6.67m, 150m, null);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void RateResolver_NonDepreciable_ShouldReturnZero()
    {
        var result = FixedAssetRateResolver.Resolve(true, 0m, 0m, 20m, null);
        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Value.RatePercent);
    }
}
