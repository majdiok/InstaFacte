using FactuTrust.Application.Features.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class ModelRefTests
{
    [Fact]
    public void Parse_LegacyPlainName_TreatsAsOllamaWithCanonicalPrefix()
    {
        var p = ModelRef.Parse("mistral:latest");
        Assert.Equal(LlmProviderKind.Ollama, p.Kind);
        Assert.Equal("mistral:latest", p.ProviderModelId);
        Assert.Equal("ollama:mistral:latest", p.CanonicalModelRef);
    }

    [Fact]
    public void Parse_ExplicitOllamaPrefix()
    {
        var p = ModelRef.Parse("ollama:qwen2.5:latest");
        Assert.Equal(LlmProviderKind.Ollama, p.Kind);
        Assert.Equal("qwen2.5:latest", p.ProviderModelId);
        Assert.Equal("ollama:qwen2.5:latest", p.CanonicalModelRef);
    }

    [Fact]
    public void Parse_OpenRouterPrefix()
    {
        var p = ModelRef.Parse("openrouter:anthropic/claude-3.5-sonnet");
        Assert.Equal(LlmProviderKind.OpenRouter, p.Kind);
        Assert.Equal("anthropic/claude-3.5-sonnet", p.ProviderModelId);
        Assert.Equal("openrouter:anthropic/claude-3.5-sonnet", p.CanonicalModelRef);
    }
}
