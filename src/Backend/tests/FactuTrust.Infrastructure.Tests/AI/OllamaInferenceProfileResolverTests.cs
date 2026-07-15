using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.AI;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class OllamaInferenceProfileResolverTests
{
    [Fact]
    public async Task ResolveForPlatformAsync_GpuMode_DoesNotForceNumGpu()
    {
        var platform = new Mock<IPlatformAiSettingsService>();
        platform.Setup(x => x.GetInferenceDeviceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OllamaInferenceDevice.Gpu);

        var resolver = CreateResolver(platform.Object, new OllamaSettings
        {
            NumThread = 4,
            NumBatch = 256,
            FixedChatNumCtx = 8192
        });

        var profile = await resolver.ResolveForPlatformAsync();

        Assert.Equal(OllamaInferenceDevice.Gpu, profile.Device);
        Assert.Null(profile.NumGpu);
        Assert.Equal(4, profile.NumThread);
        Assert.Equal(256, profile.NumBatch);
        Assert.False(profile.PreferAdaptiveChatNumCtx);
    }

    [Fact]
    public async Task ResolveForPlatformAsync_CpuOnlyMode_ForcesCpuProfile()
    {
        var platform = new Mock<IPlatformAiSettingsService>();
        platform.Setup(x => x.GetInferenceDeviceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OllamaInferenceDevice.CpuOnly);

        var resolver = CreateResolver(platform.Object, new OllamaSettings
        {
            FixedChatNumCtx = 8192,
            CpuNumBatch = 128
        });

        var profile = await resolver.ResolveForPlatformAsync();

        Assert.Equal(OllamaInferenceDevice.CpuOnly, profile.Device);
        Assert.Equal(0, profile.NumGpu);
        Assert.Equal(Math.Max(4, Environment.ProcessorCount - 1), profile.NumThread);
        Assert.Equal(128, profile.NumBatch);
        Assert.False(profile.PreferAdaptiveChatNumCtx);
    }

    [Fact]
    public void ApplyTo_SetsHardwareFieldsOnOptions()
    {
        var resolver = CreateResolver(Mock.Of<IPlatformAiSettingsService>(), new OllamaSettings());
        var profile = resolver.ResolveForDevice(OllamaInferenceDevice.CpuOnly);

        var merged = profile.ApplyTo(new FactuTrust.Application.Features.AI.DTOs.OllamaOptions
        {
            Temperature = 0.2,
            NumPredict = 512,
            NumCtx = 4096
        });

        Assert.Equal(0, merged.NumGpu);
        Assert.Equal(Math.Max(4, Environment.ProcessorCount - 1), merged.NumThread);
        Assert.Equal(128, merged.NumBatch);
        Assert.Equal(0.2, merged.Temperature);
        Assert.Equal(512, merged.NumPredict);
        Assert.Equal(4096, merged.NumCtx);
    }

    private static OllamaInferenceProfileResolver CreateResolver(
        IPlatformAiSettingsService platform,
        OllamaSettings settings) =>
        new(platform, Options.Create(settings));
}