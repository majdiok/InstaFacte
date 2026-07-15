using FactuTrust.Application.Features.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Calque sur <c>InvoiceImportNumCtxTests</c> : valide que la résolution adaptative
/// du <c>num_ctx</c> pour le chat respecte les mêmes invariants (plancher, plafond,
/// arrondi 2048, null si non configuré).
/// </summary>
public sealed class AiChatContextSizingTests
{
    [Fact]
    public void Resolve_ShortPrompt_UsesReducedContext()
    {
        // ~1000 chars ≈ 333 tokens d'entrée + 1536 sortie + 512 marge = ~2381 → arrondi 4096.
        var result = AiChatContextSizing.Resolve(
            configuredMaxNumCtx: 16384,
            configuredMinNumCtx: 4096,
            promptChars: 1000,
            maxOutputTokens: 1536);

        Assert.NotNull(result);
        Assert.True(result < 16384, "La fenêtre doit être réduite pour un prompt court.");
        Assert.True(result >= 4096, "La fenêtre ne doit jamais descendre sous le plancher.");
    }

    [Fact]
    public void Resolve_LongPrompt_CapsAtMax()
    {
        // ~60000 chars ≈ 20000 tokens d'entrée + 1536 + 512 = > 16384 → cap.
        var result = AiChatContextSizing.Resolve(
            configuredMaxNumCtx: 16384,
            configuredMinNumCtx: 4096,
            promptChars: 60000,
            maxOutputTokens: 1536);

        Assert.Equal(16384, result);
    }

    [Fact]
    public void Resolve_MediumPrompt_ReturnsIntermediateBucket()
    {
        // ~15000 chars ≈ 5000 tokens + 2048 + 512 = ~7560 → arrondi 8192.
        var result = AiChatContextSizing.Resolve(
            configuredMaxNumCtx: 16384,
            configuredMinNumCtx: 4096,
            promptChars: 15000,
            maxOutputTokens: 2048);

        Assert.Equal(8192, result);
    }

    [Fact]
    public void Resolve_ZeroConfigured_ReturnsNull()
    {
        Assert.Null(AiChatContextSizing.Resolve(
            configuredMaxNumCtx: 0,
            configuredMinNumCtx: 4096,
            promptChars: 5000,
            maxOutputTokens: 1536));
    }

    [Fact]
    public void Resolve_MinAboveMax_ClampsToMax()
    {
        // Configuration aberrante (min > max) — on doit toujours respecter le max.
        var result = AiChatContextSizing.Resolve(
            configuredMaxNumCtx: 4096,
            configuredMinNumCtx: 8192,
            promptChars: 1000,
            maxOutputTokens: 512);

        Assert.NotNull(result);
        Assert.True(result <= 4096, "La fenêtre ne doit jamais dépasser le maximum configuré.");
    }

    [Fact]
    public void Resolve_NegativePromptChars_TreatedAsZero()
    {
        // Garde-fou anti-NaN/négatif.
        var result = AiChatContextSizing.Resolve(
            configuredMaxNumCtx: 16384,
            configuredMinNumCtx: 4096,
            promptChars: -100,
            maxOutputTokens: 1024);

        Assert.NotNull(result);
        Assert.True(result >= 4096);
        Assert.True(result <= 16384);
    }
}
