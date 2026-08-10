namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Rapprochement d'un nom de modèle demandé avec un nom de modèle réellement installé.
///
/// <para><b>Pourquoi ce helper existe</b> : la règle précédente comparait la base avant le premier
/// « : ». Elle déclarait donc <c>qwen2.5:7b-instruct</c> « installé » alors que seul
/// <c>qwen2.5:3b-instruct</c> l'était. Le contrôle de disponibilité passait, puis <c>/api/chat</c>
/// renvoyait 404 — un faux positif qui déplaçait le diagnostic très loin de sa cause. Une variante
/// de ce code vivait dans deux fichiers ; elle est désormais ici, et seulement ici.</para>
///
/// <para>La seule souplesse conservée est celle d'Ollama lui-même : un nom sans étiquette désigne
/// implicitement <c>:latest</c>.</para>
/// </summary>
public static class OllamaModelName
{
    private const string DefaultTag = "latest";

    /// <summary>
    /// Vrai si <paramref name="requested"/> désigne exactement <paramref name="installedName"/>,
    /// l'étiquette absente valant <c>:latest</c> de part et d'autre.
    /// </summary>
    public static bool Matches(string? requested, string? installedName)
    {
        var left = Normalize(requested);
        var right = Normalize(installedName);

        return left.Length > 0
            && right.Length > 0
            && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>« gemma3 » → « gemma3:latest » ; « gemma3:4b » inchangé.</summary>
    public static string Normalize(string? modelName)
    {
        var trimmed = (modelName ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return string.Empty;

        return trimmed.Contains(':', StringComparison.Ordinal)
            ? trimmed
            : $"{trimmed}:{DefaultTag}";
    }
}
