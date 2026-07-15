using FactuTrust.Application.Features.AI.Commands;
using FactuTrust.Application.Features.AI.DTOs;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Doublons stricts d'un même round (même nom + mêmes arguments) : un seul appel exécuté,
/// les autres pointent vers le canonique (ex. forecast_revenue ×3 sur la capture S3).
/// </summary>
public sealed class AiToolCallDeduplicationTests
{
    private static OllamaToolCall Call(string id, string name, Dictionary<string, object?>? args = null) =>
        new()
        {
            Id = id,
            Function = new OllamaToolCallFunction { Name = name, Arguments = args ?? new Dictionary<string, object?>() }
        };

    [Fact]
    public void IdenticalCalls_MapToFirstOccurrence()
    {
        var calls = new List<OllamaToolCall>
        {
            Call("a", "forecast_revenue", new() { ["scope_type"] = "Global" }),
            Call("b", "forecast_revenue", new() { ["scope_type"] = "Global" }),
            Call("c", "forecast_revenue", new() { ["scope_type"] = "Global" })
        };

        var map = SendChatMessageHandler.MapDuplicateToolCalls(calls);

        Assert.Equal(2, map.Count);
        Assert.Equal("a", map["b"]);
        Assert.Equal("a", map["c"]);
        Assert.False(map.ContainsKey("a"));
    }

    [Fact]
    public void DifferentArguments_AreNeverDeduplicated()
    {
        var calls = new List<OllamaToolCall>
        {
            Call("a", "forecast_revenue", new() { ["scope_type"] = "Global" }),
            Call("b", "forecast_revenue", new() { ["scope_type"] = "Product" })
        };

        Assert.Empty(SendChatMessageHandler.MapDuplicateToolCalls(calls));
    }

    [Fact]
    public void DifferentTools_SameArgs_AreNeverDeduplicated()
    {
        var calls = new List<OllamaToolCall>
        {
            Call("a", "get_sales_revenue", new() { ["preset"] = "today" }),
            Call("b", "get_client_payments", new() { ["preset"] = "today" })
        };

        Assert.Empty(SendChatMessageHandler.MapDuplicateToolCalls(calls));
    }

    [Fact]
    public void CallsWithoutIdOrFunction_AreIgnored()
    {
        var calls = new List<OllamaToolCall>
        {
            new() { Id = null, Function = new OllamaToolCallFunction { Name = "x" } },
            new() { Id = "a", Function = null },
            Call("b", "get_sales_revenue")
        };

        Assert.Empty(SendChatMessageHandler.MapDuplicateToolCalls(calls));
    }
}
