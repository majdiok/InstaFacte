using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI;
using FactuTrust.Domain.Enums;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.AI;

public sealed class OllamaInferenceProfileResolver : IOllamaInferenceProfileResolver
{
    private readonly IPlatformAiSettingsService _platformAiSettings;
    private readonly OllamaSettings _settings;

    public OllamaInferenceProfileResolver(
        IPlatformAiSettingsService platformAiSettings,
        IOptions<OllamaSettings> settings)
    {
        _platformAiSettings = platformAiSettings;
        _settings = settings.Value;
    }

    public async Task<OllamaInferenceProfile> ResolveForPlatformAsync(CancellationToken cancellationToken = default)
    {
        var device = await _platformAiSettings.GetInferenceDeviceAsync(cancellationToken);
        return ResolveForDevice(device);
    }

    public OllamaInferenceProfile ResolveForDevice(OllamaInferenceDevice device) =>
        device switch
        {
            OllamaInferenceDevice.CpuOnly => new OllamaInferenceProfile(
                Device: OllamaInferenceDevice.CpuOnly,
                NumGpu: 0,
                NumThread: ResolveCpuNumThread(),
                NumBatch: ResolveCpuNumBatch(),
                PreferAdaptiveChatNumCtx: false),
            _ => new OllamaInferenceProfile(
                Device: OllamaInferenceDevice.Gpu,
                NumGpu: null,
                NumThread: _settings.NumThread,
                NumBatch: _settings.NumBatch,
                PreferAdaptiveChatNumCtx: _settings.FixedChatNumCtx <= 0
                    && _settings.AdaptiveContextEnabled)
        };

    private int ResolveCpuNumThread()
    {
        if (_settings.NumThread is > 0)
            return _settings.NumThread.Value;

        return Math.Max(4, Environment.ProcessorCount - 1);
    }

    private int ResolveCpuNumBatch()
    {
        if (_settings.CpuNumBatch > 0)
            return _settings.CpuNumBatch;

        return _settings.NumBatch ?? 128;
    }
}