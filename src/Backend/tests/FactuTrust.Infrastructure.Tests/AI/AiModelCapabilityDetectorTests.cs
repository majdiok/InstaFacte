using FactuTrust.Application.Features.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class AiModelCapabilityDetectorTests
{
    [Theory]
    [InlineData("nomic-embed-text:latest")]
    [InlineData("ollama:nomic-embed-text:latest")]
    [InlineData("bge-m3")]
    [InlineData("mxbai-embed-large")]
    public void DetectEmbeddingOnly_returns_true_for_embedding_models(string modelRef)
    {
        Assert.True(AiModelCapabilityDetector.DetectEmbeddingOnly(modelRef));
        Assert.False(AiModelCapabilityDetector.DetectChatCapable(modelRef));
    }

    [Theory]
    [InlineData("qwen2.5:7b-instruct")]
    [InlineData("ollama:qwen2.5:7b-instruct")]
    [InlineData("mistral:latest")]
    [InlineData("llava")]
    public void DetectChatCapable_returns_true_for_generation_models(string modelRef)
    {
        Assert.False(AiModelCapabilityDetector.DetectEmbeddingOnly(modelRef));
        Assert.True(AiModelCapabilityDetector.DetectChatCapable(modelRef));
    }

    [Fact]
    public void DetectVisionAndChat_CursorParsedRef_IsAlwaysTrue()
    {
        var parsed = ModelRef.Parse("cursor:composer-2.5");
        Assert.True(AiModelCapabilityDetector.DetectVisionSupport(parsed));
        Assert.True(AiModelCapabilityDetector.DetectChatCapable(parsed));
        Assert.False(AiModelCapabilityDetector.DetectVisionSupport(parsed.ProviderModelId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DetectChatCapable_returns_false_for_empty(string? modelRef)
    {
        Assert.False(AiModelCapabilityDetector.DetectChatCapable(modelRef));
    }
}
