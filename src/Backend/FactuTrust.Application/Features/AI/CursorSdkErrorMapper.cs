namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Messages utilisateur pour les erreurs renvoyées par le pont @cursor/sdk.
/// </summary>
public static class CursorSdkErrorMapper
{
    public static string ToUserMessage(string? rawError, out bool shouldLogAsError)
    {
        shouldLogAsError = false;
        if (string.IsNullOrWhiteSpace(rawError))
            return "L'inférence Cursor a échoué.";

        if (rawError.Contains("Unknown tool name(s) in disallowedTools", StringComparison.OrdinalIgnoreCase))
        {
            shouldLogAsError = true;
            return "Configuration serveur Cursor invalide. Contactez l'administrateur.";
        }

        if (rawError.Contains("sandboxing is not supported", StringComparison.OrdinalIgnoreCase)
            || rawError.Contains("sandboxOptions.enabled", StringComparison.OrdinalIgnoreCase))
        {
            shouldLogAsError = true;
            return "Configuration serveur Cursor invalide (sandbox). Contactez l'administrateur.";
        }

        if (rawError.Contains("Invalid User API Key", StringComparison.OrdinalIgnoreCase)
            || rawError.Contains("AuthenticationError", StringComparison.OrdinalIgnoreCase)
            || rawError.Contains("authentication", StringComparison.OrdinalIgnoreCase))
        {
            return "Clé API Cursor invalide ou expirée.";
        }

        // Erreur non reconnue : ne jamais renvoyer le message brut du SDK à l'utilisateur
        // (CWE-209 — il peut contenir des détails internes : chemins, config, stack). Le détail
        // est loggé côté serveur par l'appelant lorsque shouldLogAsError est vrai.
        shouldLogAsError = true;
        return "L'inférence Cursor a échoué. Contactez l'administrateur si le problème persiste.";
    }
}
