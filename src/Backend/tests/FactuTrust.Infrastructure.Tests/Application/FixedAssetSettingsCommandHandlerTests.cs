using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.FixedAssets;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.Services;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// Tests des handlers lecture/écriture des paramètres Immobilisations du dossier
/// (plan « Exercices décalés », P1). Le résolveur est pur → instancié réellement.
/// </summary>
public sealed class FixedAssetSettingsCommandHandlerTests
{
    private readonly FiscalYearResolver _resolver = new();

    private static ICurrentUser CurrentUser(string email = "test@factutrust.tn")
    {
        var mock = new Mock<ICurrentUser>();
        mock.SetupGet(x => x.Email).Returns(email);
        return mock.Object;
    }

    [Fact]
    public async Task Get_OnUnconfiguredTenant_ReturnsCivilDefaults()
    {
        var settings = new Mock<IFixedAssetSettingsRepository>();
        settings.Setup(x => x.GetForTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(FixedAssetSettings.CreateDefault());

        var handler = new GetFixedAssetSettingsQueryHandler(settings.Object, _resolver);
        var result = await handler.Handle(new GetFixedAssetSettingsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(1, result.Value.FiscalYearStartMonth);
        Assert.Equal("N/N+1", result.Value.FiscalYearLabelFormat);
        // Exercice civil → libellé d'exemple = année courante (4 chiffres).
        Assert.Matches(@"^\d{4}$", result.Value.FiscalYearLabelSample);
    }

    [Fact]
    public async Task Get_OnOffsetTenant_ReturnsConfiguredValuesAndNn1Sample()
    {
        var settings = new Mock<IFixedAssetSettingsRepository>();
        settings.Setup(x => x.GetForTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(FixedAssetSettings.Create(7, "N/N+1").Value);

        var handler = new GetFixedAssetSettingsQueryHandler(settings.Object, _resolver);
        var result = await handler.Handle(new GetFixedAssetSettingsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(7, result.Value.FiscalYearStartMonth);
        Assert.Equal("N/N+1", result.Value.FiscalYearLabelFormat);
        // Exercice décalé + format N/N+1 → libellé d'exemple « YYYY/YYYY ».
        Assert.Matches(@"^\d{4}/\d{4}$", result.Value.FiscalYearLabelSample);
    }

    [Fact]
    public async Task Update_ValidRequest_CallsUpsertAndReturnsPersistedDto()
    {
        var settings = new Mock<IFixedAssetSettingsRepository>();
        var persisted = FixedAssetSettings.Create(7, "N").Value;
        settings.Setup(x => x.UpsertAsync(7, "N", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(persisted);

        var handler = new UpdateFixedAssetSettingsCommandHandler(settings.Object, _resolver, CurrentUser());
        var result = await handler.Handle(
            new UpdateFixedAssetSettingsCommand(new UpdateFixedAssetSettingsRequest(7, "N")),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(7, result.Value.FiscalYearStartMonth);
        Assert.Equal("N", result.Value.FiscalYearLabelFormat);
        settings.Verify(x => x.UpsertAsync(7, "N", "test@factutrust.tn", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_InvalidStartMonth_ReturnsStableFailureWithoutUpsert()
    {
        var settings = new Mock<IFixedAssetSettingsRepository>();
        var handler = new UpdateFixedAssetSettingsCommandHandler(settings.Object, _resolver, CurrentUser());

        var result = await handler.Handle(
            new UpdateFixedAssetSettingsCommand(new UpdateFixedAssetSettingsRequest(13, "N/N+1")),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("1 et 12", result.Error.Description);
        settings.Verify(x => x.UpsertAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_InvalidLabelFormat_ReturnsStableFailureWithoutUpsert()
    {
        var settings = new Mock<IFixedAssetSettingsRepository>();
        var handler = new UpdateFixedAssetSettingsCommandHandler(settings.Object, _resolver, CurrentUser());

        var result = await handler.Handle(
            new UpdateFixedAssetSettingsCommand(new UpdateFixedAssetSettingsRequest(7, "X/Y")),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("N/N+1", result.Error.Description);
        settings.Verify(x => x.UpsertAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
