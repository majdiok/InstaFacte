namespace FactuTrust.Application.Common.Logging;

/// <summary>
/// Helpers pour éviter d'écrire des informations sensibles ou non fiabilisées (contrôlées par
/// l'utilisateur) directement dans les logs applicatifs.
///
/// <list type="bullet">
///   <item><see cref="MaskEmail"/> : masque une adresse e-mail (CWE-532 — fuite d'information
///   sensible dans les logs) tout en conservant assez de contexte pour le diagnostic.</item>
///   <item><see cref="Sanitize"/> : neutralise l'injection de logs (CWE-117) en retirant les
///   retours chariot/saut de ligne et en tronquant les valeurs d'origine utilisateur avant de les
///   interpoler dans un message de log.</item>
/// </list>
/// </summary>
public static class LogSanitizer
{
    /// <summary>
    /// Longueur maximale par défaut appliquée par <see cref="Sanitize"/> pour éviter qu'une
    /// entrée utilisateur trop longue ne pollue les logs.
    /// </summary>
    public const int DefaultMaxLength = 200;

    /// <summary>
    /// Masque une adresse e-mail pour le logging : "john@x.com" → "j***@x.com".
    /// Retourne une valeur sûre (jamais l'e-mail en clair) même pour les entrées invalides.
    /// </summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "(empty)";

        var trimmed = email.Trim();
        var atIndex = trimmed.IndexOf('@');

        // Pas de '@' (ou en première position) : on ne peut pas séparer local-part/domaine de
        // façon fiable, on masque entièrement plutôt que de risquer une fuite partielle.
        if (atIndex <= 0)
            return "***";

        var localPart = trimmed[..atIndex];
        var domainPart = trimmed[(atIndex + 1)..];

        var firstChar = localPart[0];
        return $"{firstChar}***@{domainPart}";
    }

    /// <summary>
    /// Neutralise une valeur d'origine utilisateur avant de la logger : retire les caractères
    /// CR/LF (CWE-117, injection de logs — usurpation de lignes de log) et tronque à
    /// <paramref name="maxLength"/> caractères pour éviter les logs excessivement volumineux.
    /// </summary>
    public static string Sanitize(string? value, int maxLength = DefaultMaxLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var withoutNewlines = value.Replace("\r", string.Empty).Replace("\n", string.Empty);

        if (maxLength > 0 && withoutNewlines.Length > maxLength)
            return withoutNewlines[..maxLength] + "…";

        return withoutNewlines;
    }
}
