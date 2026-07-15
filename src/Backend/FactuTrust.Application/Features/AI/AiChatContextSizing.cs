namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Calcule une fenêtre de contexte (num_ctx) adaptée à la taille réelle du prompt chat,
/// bornée à [floor, configuredMaxNumCtx]. Sur CPU, le prefill coûte O(num_ctx) — réduire
/// la fenêtre quand le prompt est court fait gagner significativement en latence du 1ᵉʳ token,
/// sans jamais tronquer le prompt réel (on prend toujours au moins ce qu'il faut).
///
/// Logique calquée sur <c>InvoiceImportParsing.ResolveImportNumCtx</c> : on conserve le
/// même style (heuristique ~3 caractères/token, arrondi au multiple de 2048 supérieur)
/// pour rester cohérent avec ce qui est déjà éprouvé dans le projet.
/// </summary>
public static class AiChatContextSizing
{
    /// <summary>
    /// Renvoie le <c>num_ctx</c> effectif à envoyer à Ollama. <c>null</c> = laisser Ollama
    /// décider (valeur par défaut du modèle), comportement historique quand
    /// <c>configuredMaxNumCtx &lt;= 0</c>.
    /// </summary>
    /// <param name="configuredMaxNumCtx">Plafond configuré (<c>Ollama:NumCtx</c>).</param>
    /// <param name="configuredMinNumCtx">Plancher souhaité (<c>Ollama:NumCtxMin</c>).</param>
    /// <param name="promptChars">Somme des longueurs en caractères de tous les messages envoyés au modèle (system + historique).</param>
    /// <param name="maxOutputTokens">Plafond de tokens produits (<c>num_predict</c>).</param>
    public static int? Resolve(
        int configuredMaxNumCtx,
        int configuredMinNumCtx,
        int promptChars,
        int maxOutputTokens)
    {
        if (configuredMaxNumCtx <= 0)
            return null;

        // Heuristique partagée avec l'import de factures : ~3 caractères par token (estimation prudente,
        // robuste pour le français mêlant chiffres/JSON/code).
        var approxInputTokens = System.Math.Max(0, promptChars) / 3;

        // Marge de sécurité pour les tokens spéciaux, headers de message, et l'éventuelle réponse.
        var needed = approxInputTokens + System.Math.Max(1, maxOutputTokens) + 512;

        // Arrondi au multiple de 2048 supérieur — Ollama est plus efficace sur les frontières usuelles.
        var rounded = ((needed + 2047) / 2048) * 2048;

        // Plancher : la valeur configurée, mais ne jamais dépasser le plafond.
        var lowerBound = System.Math.Min(System.Math.Max(0, configuredMinNumCtx), configuredMaxNumCtx);
        if (lowerBound <= 0)
            lowerBound = System.Math.Min(4096, configuredMaxNumCtx);

        return System.Math.Clamp(rounded, lowerBound, configuredMaxNumCtx);
    }
}
