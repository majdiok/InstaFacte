namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Calcul PUR de la borne de flush pour le streaming spéculatif du contenu assistant : on streame les
/// tokens en direct pendant le round final, puis un <c>content_replace</c> réconcilie avec le corps
/// post-traité. La borne garantit qu'un segment flushé :
///  - retient une fenêtre de garde (<see cref="DefaultHoldbackChars"/>) en fin de texte,
///  - se termine sur un espace (un identifiant d'outil interne ne peut donc pas chevaucher la frontière,
///    la substitution <see cref="AssistantVisibleContentFormatter.SanitizeInternalToolNames"/> reste sûre),
///  - ne coupe JAMAIS l'intérieur d'une fence <c>```</c> ouverte (pas de JSON partiel à l'écran).
/// </summary>
public static class AiLiveContentStreamer
{
    /// <summary>Fenêtre de garde en fin de texte (jamais flushée avant la réconciliation).</summary>
    public const int DefaultHoldbackChars = 64;

    /// <summary>Volume en attente qui force un flush sans attendre l'intervalle.</summary>
    public const int MinFlushChars = 160;

    /// <summary>Intervalle minimal entre deux flushes (throttle SSE ≈ 5 événements/s max).</summary>
    public const int FlushIntervalMs = 200;

    /// <summary>
    /// Position (exclusive) jusqu'où <paramref name="text"/> peut être flushé sans risque.
    /// Renvoie <paramref name="flushedUpTo"/> si rien de flushable.
    /// </summary>
    public static int ComputeFlushableLength(string text, int flushedUpTo, int holdbackChars = DefaultHoldbackChars)
    {
        if (string.IsNullOrEmpty(text) || flushedUpTo >= text.Length)
            return flushedUpTo;

        var boundary = text.Length - Math.Max(0, holdbackChars);
        if (boundary <= flushedUpTo)
            return flushedUpTo;

        // Reculer au dernier espace : aucun token (nom d'outil, mot) coupé en deux.
        while (boundary > flushedUpTo && !char.IsWhiteSpace(text[boundary - 1]))
            boundary--;
        if (boundary <= flushedUpTo)
            return flushedUpTo;

        // Jamais à l'intérieur d'une fence ``` ouverte : si le préfixe [0, boundary) contient un nombre
        // impair de ```, reculer avant l'ouverture de la dernière fence.
        if ((CountFenceMarkers(text, boundary) & 1) == 1)
        {
            var lastFence = text.LastIndexOf("```", boundary - 1, StringComparison.Ordinal);
            boundary = Math.Max(flushedUpTo, lastFence);
        }

        return boundary <= flushedUpTo ? flushedUpTo : boundary;
    }

    private static int CountFenceMarkers(string text, int upTo)
    {
        var count = 0;
        var index = 0;
        while (index <= upTo - 3)
        {
            var found = text.IndexOf("```", index, upTo - index, StringComparison.Ordinal);
            if (found < 0)
                break;
            count++;
            index = found + 3;
        }
        return count;
    }
}
