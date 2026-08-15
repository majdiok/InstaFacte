using FactuTrust.Application.Features.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class AiAssistantAvailabilityResolverTests
{
    [Theory]
    [InlineData(true, "sk-or-1", false, false, null, true)]
    [InlineData(false, null, true, true, "cursor_key", true)]
    [InlineData(false, null, true, false, "cursor_key", false)]
    [InlineData(false, null, false, true, "cursor_key", false)]
    public void HasCloudProviderConfigured_matches_openrouter_and_cursor_rules(
        bool openRouterEnabled,
        string? openRouterApiKey,
        bool cursorSdkEnabled,
        bool cursorDbEnabled,
        string? cursorApiKey,
        bool expected)
    {
        var actual = AiAssistantAvailabilityResolver.HasCloudProviderConfigured(
            openRouterEnabled,
            openRouterApiKey,
            cursorSdkEnabled,
            cursorDbEnabled,
            cursorApiKey);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task IsHealthAvailableAsync_returns_true_when_ollama_ok()
    {
        var available = await AiAssistantAvailabilityResolver.IsHealthAvailableAsync(
            ollamaOk: true,
            hasCloudProvider: false,
            activeModel: ModelRef.Parse("ollama:mistral"),
            _ => Task.FromResult(false));

        Assert.True(available);
    }

    [Fact]
    public async Task IsHealthAvailableAsync_returns_false_when_no_provider()
    {
        var available = await AiAssistantAvailabilityResolver.IsHealthAvailableAsync(
            ollamaOk: false,
            hasCloudProvider: false,
            activeModel: ModelRef.Parse("ollama:mistral"),
            _ => Task.FromResult(true));

        Assert.False(available);
    }

    [Fact]
    public async Task IsHealthAvailableAsync_returns_true_for_openrouter_without_bridge_probe()
    {
        var probeCalled = false;
        var available = await AiAssistantAvailabilityResolver.IsHealthAvailableAsync(
            ollamaOk: false,
            hasCloudProvider: true,
            activeModel: ModelRef.Parse("openrouter:anthropic/claude-3.5-sonnet"),
            _ =>
            {
                probeCalled = true;
                return Task.FromResult(false);
            });

        Assert.True(available);
        Assert.False(probeCalled);
    }

    [Fact]
    public async Task IsHealthAvailableAsync_requires_cursor_bridge_when_active_model_is_cursor()
    {
        var available = await AiAssistantAvailabilityResolver.IsHealthAvailableAsync(
            ollamaOk: false,
            hasCloudProvider: true,
            activeModel: ModelRef.Parse("cursor:composer-2.5"),
            _ => Task.FromResult(true));

        Assert.True(available);

        var unavailable = await AiAssistantAvailabilityResolver.IsHealthAvailableAsync(
            ollamaOk: false,
            hasCloudProvider: true,
            activeModel: ModelRef.Parse("cursor:composer-2.5"),
            _ => Task.FromResult(false));

        Assert.False(unavailable);
    }
}
