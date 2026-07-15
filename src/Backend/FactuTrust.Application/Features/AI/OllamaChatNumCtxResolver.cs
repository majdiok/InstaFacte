using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Résout le <c>num_ctx</c> de chargement Ollama de façon identique pour le chat, le warm-up et le keep-alive.
/// Sur CPU, une valeur FIXE évite les rechargements coûteux quand warm-up et chat divergent (contexte adaptatif).
/// </summary>
public static class OllamaChatNumCtxResolver
{
    /// <summary>
    /// <c>num_ctx</c> pour warm-up / keep-alive (aucune estimation de prompt disponible).
    /// </summary>
    public static int? ResolveForModelLoad(OllamaSettings settings, OllamaInferenceProfile? profile)
    {
        if (profile?.Device == OllamaInferenceDevice.CpuOnly)
            return ResolveCpuPinnedNumCtx(settings);

        if (settings.FixedChatNumCtx > 0)
            return settings.FixedChatNumCtx;

        if (settings.AdaptiveContextEnabled && settings.NumCtx > 0)
            return settings.NumCtx;

        return settings.NumCtx > 0 ? settings.NumCtx : null;
    }

    /// <summary>
    /// <c>num_ctx</c> pour une requête chat (tours agent, synthèse).
    /// </summary>
    public static int? ResolveForChat(
        OllamaSettings settings,
        OllamaInferenceProfile? profile,
        int promptChars,
        int maxOutputTokens)
    {
        if (profile?.Device == OllamaInferenceDevice.CpuOnly)
            return ResolveCpuForChat(settings, promptChars, maxOutputTokens);

        if (profile?.PreferAdaptiveChatNumCtx == true)
        {
            return AiChatContextSizing.Resolve(
                settings.NumCtx,
                settings.NumCtxMin,
                promptChars,
                Math.Max(1, maxOutputTokens));
        }

        if (settings.FixedChatNumCtx > 0)
            return settings.FixedChatNumCtx;

        return settings.AdaptiveContextEnabled
            ? AiChatContextSizing.Resolve(
                settings.NumCtx,
                settings.NumCtxMin,
                promptChars,
                Math.Max(1, maxOutputTokens))
            : (settings.NumCtx > 0 ? settings.NumCtx : null);
    }

    private static int? ResolveCpuForChat(OllamaSettings settings, int promptChars, int maxOutputTokens)
    {
        var pinned = ResolveCpuPinnedNumCtx(settings);
        if (pinned is null or <= 0)
            return null;

        var approxInputTokens = Math.Max(0, promptChars) / 3;
        var needed = approxInputTokens + Math.Max(1, maxOutputTokens) + 512;
        if (needed > pinned.Value)
        {
            // Dépassement : on conserve la valeur épinglée (pas de 3ᵉ taille adaptative).
            // L'appelant doit journaliser — risque de troncature Ollama.
        }

        return pinned;
    }

    /// <summary>
    /// Valeur unique CPU pour warm-up, keep-alive et chat : le plafond si configuré (&gt; base),
    /// sinon la base. Évite tout rechargement Ollama entre 6144 et 8192.
    /// </summary>
    public static int? ResolveCpuPinnedNumCtx(OllamaSettings settings)
    {
        var baseCtx = ResolveCpuFixed(settings);
        if (baseCtx is null or <= 0)
            return null;

        var ceiling = ResolveCpuCeiling(settings);
        return ceiling > baseCtx.Value ? ceiling : baseCtx;
    }

    public static bool MayTruncatePrompt(int promptChars, int maxOutputTokens, int? numCtx) =>
        numCtx is > 0
        && Math.Max(0, promptChars) / 3 + Math.Max(1, maxOutputTokens) + 512 > numCtx.Value;

    private static int ResolveCpuCeiling(OllamaSettings settings) =>
        settings.CpuFixedChatNumCtxCeiling > 0
            ? settings.CpuFixedChatNumCtxCeiling
            : Math.Min(Math.Max(settings.NumCtx, 0), 8192);

    private static int? ResolveCpuFixed(OllamaSettings settings)
    {
        if (settings.CpuFixedChatNumCtx > 0)
            return settings.CpuFixedChatNumCtx;

        // Rétrocompat : si non configuré, utiliser FixedChatNumCtx ou plancher NumCtxMin.
        if (settings.FixedChatNumCtx > 0)
            return settings.FixedChatNumCtx;

        return settings.NumCtxMin > 0 ? settings.NumCtxMin : 4096;
    }
}