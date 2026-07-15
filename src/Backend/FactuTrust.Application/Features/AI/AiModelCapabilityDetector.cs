namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Heuristiques d'auto-détection des capacités d'un modèle LLM à partir de son
/// nom/référence. Utilisé pour décider, par exemple, si on doit envoyer les
/// images d'une pièce jointe au modèle (modèles vision multimodaux).
/// </summary>
public static class AiModelCapabilityDetector
{
    private static readonly string[] VisionMarkers =
    {
        "llava", "vision", "gemma3", "gemma-3", "gemma:3",
        "qwen2-vl", "qwen2.5-vl", "qwen-vl",
        "llama3.2-vision", "llama-3.2-vision",
        "minicpm-v", "moondream", "bakllava",
        "pixtral", "internvl",
        "phi-3.5-vision", "phi-4-vision",
        "claude-3", "claude-4",
        "gpt-4o", "gpt-4-turbo", "gpt-4v",
        "gemini-1.5", "gemini-2"
    };

    /// <summary>
    /// Vrai si le modèle nommé semble supporter l'entrée image (vision multimodale).
    /// Détection conservative : faux par défaut si le nom est inconnu.
    /// </summary>
    public static bool DetectVisionSupport(string? modelRef)
    {
        if (string.IsNullOrWhiteSpace(modelRef)) return false;
        var name = modelRef.ToLowerInvariant();
        foreach (var marker in VisionMarkers)
        {
            if (name.Contains(marker, StringComparison.Ordinal)) return true;
        }
        return false;
    }
}
