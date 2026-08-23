namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Normalise l'URL de base d'un endpoint Modal OpenAI-compatible
/// (<c>https://…/v1</c>, sans <c>/chat/completions</c>).
/// </summary>
public static class ModalEndpointUrl
{
    public static bool TryNormalize(string? raw, out string? normalized, out string? error)
    {
        normalized = null;
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        var s = raw.Trim();
        if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            error = "L'URL Modal doit utiliser HTTPS.";
            return false;
        }

        if (!Uri.TryCreate(s, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            error = "L'URL Modal est invalide.";
            return false;
        }

        s = s.TrimEnd('/');
        const string chatCompletions = "/chat/completions";
        if (s.EndsWith(chatCompletions, StringComparison.OrdinalIgnoreCase))
            s = s[..^chatCompletions.Length].TrimEnd('/');

        if (!s.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            s += "/v1";

        normalized = s;
        return true;
    }
}
