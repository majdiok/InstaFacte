using FactuTrust.Application.Features.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Borne de flush du streaming spéculatif : fenêtre de garde respectée, coupe sur un espace
/// (aucun token/nom d'outil scindé), jamais à l'intérieur d'une fence ``` ouverte.
/// </summary>
public sealed class AiLiveContentStreamerTests
{
    [Fact]
    public void ShortText_UnderHoldback_NothingFlushable()
    {
        var text = new string('a', AiLiveContentStreamer.DefaultHoldbackChars);
        Assert.Equal(0, AiLiveContentStreamer.ComputeFlushableLength(text, 0));
    }

    [Fact]
    public void Boundary_SnapsBack_ToLastWhitespace()
    {
        // 100 'a' + espace + 100 'b' : holdback 64 → borne brute à 137 (dans les 'b') → recule à 101.
        var text = new string('a', 100) + " " + new string('b', 100);

        var flushTo = AiLiveContentStreamer.ComputeFlushableLength(text, 0);

        Assert.Equal(101, flushTo);
        Assert.True(char.IsWhiteSpace(text[flushTo - 1]));
    }

    [Fact]
    public void OpenFence_IsNeverFlushedInside()
    {
        var text = "Voici le tableau :\n```json\n{\"a\":1, \"b\":2, " + new string('x', 200);

        var flushTo = AiLiveContentStreamer.ComputeFlushableLength(text, 0);

        // La borne recule avant l'ouverture de la fence (index de "```").
        Assert.True(flushTo <= text.IndexOf("```", StringComparison.Ordinal));
    }

    [Fact]
    public void ClosedFence_IsFlushable()
    {
        var text = "Avant.\n```json\n{\"a\":1}\n```\nAprès la fence, une longue suite de texte "
                   + new string('y', 120) + " fin";

        var flushTo = AiLiveContentStreamer.ComputeFlushableLength(text, 0);

        Assert.True(flushTo > text.IndexOf("Après", StringComparison.Ordinal));
    }

    [Fact]
    public void SuccessiveFlushes_ConcatenateToPrefix_WithoutOverlapOrLoss()
    {
        var text = string.Join(" ", Enumerable.Range(1, 120).Select(i => $"mot{i}"));
        var flushed = 0;
        var rebuilt = string.Empty;

        // Simule 3 vagues d'arrivée de texte.
        foreach (var upTo in new[] { text.Length / 3, 2 * text.Length / 3, text.Length })
        {
            var partial = text[..upTo];
            var flushTo = AiLiveContentStreamer.ComputeFlushableLength(partial, flushed);
            if (flushTo > flushed)
            {
                rebuilt += partial[flushed..flushTo];
                flushed = flushTo;
            }
        }

        Assert.True(rebuilt.Length > 0);
        Assert.StartsWith(rebuilt, text, StringComparison.Ordinal);
    }

    [Fact]
    public void FlushedUpTo_IsNeverExceededBackwards()
    {
        var text = new string('a', 50) + " " + new string('b', 200);
        var first = AiLiveContentStreamer.ComputeFlushableLength(text, 0);
        var second = AiLiveContentStreamer.ComputeFlushableLength(text, first);
        Assert.True(second >= first);
    }
}
