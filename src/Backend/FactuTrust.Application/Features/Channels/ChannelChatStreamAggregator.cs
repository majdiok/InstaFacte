using System.Text;
using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Application.Features.Channels;

/// <summary>Résultat de l'agrégation d'un flux de chat pour un canal texte (WhatsApp).</summary>
public sealed record ChannelChatAggregate(string Content, string? Error, Guid? ConversationId);

/// <summary>
/// Agrège le flux <see cref="ChatStreamEvent"/> du pipeline IA en un texte final unique :
/// <list type="bullet">
///   <item><c>content</c> — chunks spéculatifs cumulés ;</item>
///   <item><c>content_replace</c> — corps réconcilié post-synthèse qui ÉCRASE le buffer
///     (peut survenir plusieurs fois, le dernier gagne) ;</item>
///   <item><c>error</c> — capturé (le flux peut continuer) ;</item>
///   <item><c>done</c> — porte le ConversationId ;</item>
///   <item>tout le reste (phase, tool_call_*, heartbeat, sources, suggested_prompts, dashboard,
///     client_actions, studio_progress) — ignoré : sans équivalent sur un canal texte.</item>
/// </list>
/// Statique et sans dépendance : testable unitairement avec des flux synthétiques.
/// </summary>
public static class ChannelChatStreamAggregator
{
    public static async Task<ChannelChatAggregate> AggregateAsync(
        IAsyncEnumerable<ChatStreamEvent> stream, CancellationToken cancellationToken = default)
    {
        var buffer = new StringBuilder();
        string? error = null;
        Guid? conversationId = null;

        await foreach (var evt in stream.WithCancellation(cancellationToken))
        {
            switch (evt.Type)
            {
                case "content":
                    buffer.Append(evt.Content);
                    break;
                case "content_replace":
                    buffer.Clear();
                    buffer.Append(evt.Content);
                    break;
                case "error":
                    error = evt.Error;
                    break;
                case "done":
                    conversationId = evt.ConversationId;
                    break;
            }
        }

        return new ChannelChatAggregate(buffer.ToString(), error, conversationId);
    }
}
