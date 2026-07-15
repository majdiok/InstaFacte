using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class OllamaChatNumCtxResolverTests
{
    private static OllamaSettings DefaultSettings() => new()
    {
        FixedChatNumCtx = 8192,
        CpuFixedChatNumCtx = 6144,
        CpuFixedChatNumCtxCeiling = 8192,
        NumCtx = 16384,
        NumCtxMin = 4096
    };

    private static OllamaInferenceProfile CpuProfile() => new(
        OllamaInferenceDevice.CpuOnly,
        NumGpu: 0,
        NumThread: 8,
        NumBatch: 128,
        PreferAdaptiveChatNumCtx: false);

    private static OllamaInferenceProfile GpuProfile() => new(
        OllamaInferenceDevice.Gpu,
        NumGpu: null,
        NumThread: null,
        NumBatch: 256,
        PreferAdaptiveChatNumCtx: false);

    [Fact]
    public void ResolveForModelLoad_Cpu_UsesPinnedCeiling()
    {
        var settings = DefaultSettings();
        var result = OllamaChatNumCtxResolver.ResolveForModelLoad(settings, CpuProfile());
        Assert.Equal(8192, result);
    }

    [Fact]
    public void ResolveForModelLoad_Gpu_UsesFixedChatNumCtx()
    {
        var settings = DefaultSettings();
        var result = OllamaChatNumCtxResolver.ResolveForModelLoad(settings, GpuProfile());
        Assert.Equal(8192, result);
    }

    [Fact]
    public void ResolveForChat_Cpu_ShortPrompt_UsesPinnedCeiling()
    {
        var settings = DefaultSettings();
        var result = OllamaChatNumCtxResolver.ResolveForChat(settings, CpuProfile(), promptChars: 2000, maxOutputTokens: 1536);
        Assert.Equal(8192, result);
    }

    [Fact]
    public void ResolveForChat_Cpu_LargePrompt_UsesCeilingNotAdaptive()
    {
        var settings = DefaultSettings();
        var result = OllamaChatNumCtxResolver.ResolveForChat(settings, CpuProfile(), promptChars: 40000, maxOutputTokens: 1536);
        Assert.Equal(8192, result);
    }

    [Fact]
    public void WarmUpAndChat_Cpu_UseSameValue_ForTypicalPrompt()
    {
        var settings = DefaultSettings();
        var profile = CpuProfile();
        var warm = OllamaChatNumCtxResolver.ResolveForModelLoad(settings, profile);
        var chat = OllamaChatNumCtxResolver.ResolveForChat(settings, profile, promptChars: 5000, maxOutputTokens: 1536);
        Assert.Equal(warm, chat);
    }

    [Fact]
    public void MayTruncatePrompt_WhenPromptExceedsNumCtx_ReturnsTrue()
    {
        Assert.True(OllamaChatNumCtxResolver.MayTruncatePrompt(promptChars: 30000, maxOutputTokens: 1536, numCtx: 8192));
        Assert.False(OllamaChatNumCtxResolver.MayTruncatePrompt(promptChars: 3000, maxOutputTokens: 512, numCtx: 8192));
    }
}