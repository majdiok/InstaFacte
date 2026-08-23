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
        Assert.Empty(p.Params);
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

    [Fact]
    public void Parse_CursorPrefix_WithoutParams()
    {
        var p = ModelRef.Parse("cursor:composer-2.5");
        Assert.Equal(LlmProviderKind.Cursor, p.Kind);
        Assert.Equal("composer-2.5", p.ProviderModelId);
        Assert.Equal("cursor:composer-2.5", p.CanonicalModelRef);
        Assert.Empty(p.Params);
    }

    [Fact]
    public void Parse_CursorPrefix_WithParams_CanonicalizesOrder()
    {
        var p = ModelRef.Parse("cursor:composer-2.5|fast=true");
        Assert.Equal(LlmProviderKind.Cursor, p.Kind);
        Assert.Equal("composer-2.5", p.ProviderModelId);
        Assert.Equal("cursor:composer-2.5|fast=true", p.CanonicalModelRef);
        Assert.Single(p.Params);
        Assert.Equal("fast", p.Params[0].Id);
        Assert.Equal("true", p.Params[0].Value);
    }

    [Fact]
    public void Parse_CursorAutoSmart_ParsesOptimizeFor()
    {
        var p = ModelRef.Parse("cursor:auto-smart|optimize_for=balanced");
        Assert.Equal(LlmProviderKind.Cursor, p.Kind);
        Assert.Equal("auto-smart", p.ProviderModelId);
        Assert.Equal("cursor:auto-smart|optimize_for=balanced", p.CanonicalModelRef);
        Assert.True(CursorModelSelection.TryValidate(p, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void CursorModelSelection_AutoSmartWithoutOptimizeFor_IsInvalid()
    {
        var p = ModelRef.Parse("cursor:auto-smart");
        Assert.False(CursorModelSelection.TryValidate(p, out var error));
        Assert.Contains("optimize_for", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormatCursor_RoundtripsThroughParse()
    {
        var formatted = ModelRef.FormatCursor("composer-2.5", [new CursorModelParam("fast", "true")]);
        var parsed = ModelRef.Parse(formatted);
        Assert.Equal(formatted, parsed.CanonicalModelRef);
        Assert.Equal("composer-2.5", parsed.ProviderModelId);
    }

    [Fact]
    public void Enum_Cursor_IsTwo_OllamaAndOpenRouterUnchanged()
    {
        Assert.Equal(0, (int)LlmProviderKind.Ollama);
        Assert.Equal(1, (int)LlmProviderKind.OpenRouter);
        Assert.Equal(2, (int)LlmProviderKind.Cursor);
        Assert.Equal(3, (int)LlmProviderKind.Modal);
    }

    [Fact]
    public void Parse_ModalPrefix()
    {
        var p = ModelRef.Parse("modal:moonshotai/Kimi-K3");
        Assert.Equal(LlmProviderKind.Modal, p.Kind);
        Assert.Equal("moonshotai/Kimi-K3", p.ProviderModelId);
        Assert.Equal("modal:moonshotai/Kimi-K3", p.CanonicalModelRef);
        Assert.Empty(p.Params);
    }

    [Fact]
    public void Parse_UnprefixedMoonshotId_RemainsOllama()
    {
        var p = ModelRef.Parse("moonshotai/Kimi-K3");
        Assert.Equal(LlmProviderKind.Ollama, p.Kind);
        Assert.Equal("moonshotai/Kimi-K3", p.ProviderModelId);
        Assert.Equal("ollama:moonshotai/Kimi-K3", p.CanonicalModelRef);
    }
}
