using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.Channels;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Channels;

/// <summary>
/// Agrégation du flux ChatStreamEvent pour un canal texte : chunks « content » cumulés,
/// « content_replace » écrase (le dernier gagne), « error » capturé, « done » porte le
/// ConversationId, tout le reste ignoré.
/// </summary>
public sealed class ChannelChatStreamAggregatorTests
{
    private static async IAsyncEnumerable<ChatStreamEvent> Stream(params ChatStreamEvent[] events)
    {
        foreach (var evt in events)
        {
            yield return evt;
            await Task.Yield();
        }
    }

    [Fact]
    public async Task ContentChunks_AreAccumulated()
    {
        var result = await ChannelChatStreamAggregator.AggregateAsync(Stream(
            ChatStreamEvent.ContentChunk("Le CA "),
            ChatStreamEvent.ContentChunk("est de "),
            ChatStreamEvent.ContentChunk("12 500 TND.")));

        Assert.Equal("Le CA est de 12 500 TND.", result.Content);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task ContentReplace_OverwritesSpeculativeChunks()
    {
        var result = await ChannelChatStreamAggregator.AggregateAsync(Stream(
            ChatStreamEvent.ContentChunk("brouillon spéculatif"),
            ChatStreamEvent.ContentReplace("Corps final réconcilié.")));

        Assert.Equal("Corps final réconcilié.", result.Content);
    }

    [Fact]
    public async Task MultipleContentReplace_LastOneWins()
    {
        var result = await ChannelChatStreamAggregator.AggregateAsync(Stream(
            ChatStreamEvent.ContentReplace("première synthèse"),
            ChatStreamEvent.ContentChunk(" chunk tardif"),
            ChatStreamEvent.ContentReplace("Synthèse définitive.")));

        Assert.Equal("Synthèse définitive.", result.Content);
    }

    [Fact]
    public async Task Error_IsCaptured_WithoutLosingContent()
    {
        var result = await ChannelChatStreamAggregator.AggregateAsync(Stream(
            ChatStreamEvent.ContentChunk("Réponse partielle"),
            ChatStreamEvent.ErrorEvent("Modèle indisponible")));

        Assert.Equal("Réponse partielle", result.Content);
        Assert.Equal("Modèle indisponible", result.Error);
    }

    [Fact]
    public async Task Done_CarriesConversationId_AndNoiseEventsAreIgnored()
    {
        var conversationId = Guid.NewGuid();

        var result = await ChannelChatStreamAggregator.AggregateAsync(Stream(
            ChatStreamEvent.PhaseEvent("model_generation", "running"),
            ChatStreamEvent.ToolCallStart("get_sales_revenue", "call-1"),
            ChatStreamEvent.ToolCallEnd("get_sales_revenue", "call-1", 1200),
            ChatStreamEvent.Heartbeat(),
            ChatStreamEvent.ContentChunk("Réponse."),
            ChatStreamEvent.SuggestedPromptsEvent("[]"),
            ChatStreamEvent.Done(conversationId)));

        Assert.Equal("Réponse.", result.Content);
        Assert.Equal(conversationId, result.ConversationId);
    }

    [Fact]
    public async Task EmptyStream_GivesEmptyContent()
    {
        var result = await ChannelChatStreamAggregator.AggregateAsync(Stream());

        Assert.Equal(string.Empty, result.Content);
        Assert.Null(result.Error);
        Assert.Null(result.ConversationId);
    }
}
