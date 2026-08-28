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
    ///
    /// L'entrée n'est pas fiabilisée (les DTO d'auth ne valident pas le format de l'e-mail) :
    /// elle est donc d'abord passée par <see cref="Sanitize"/> pour retirer tout CR/LF avant
    /// d'être découpée/masquée, et la valeur retournée est elle-même sanitizée, afin d'éviter
    /// qu'un e-mail malveillant du type "a@example.com\r\nFAKE LOG" n'injecte de fausses lignes
    /// de log (CWE-117) via le domaine conservé après masquage.
    /// </summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "(empty)";

        var sanitized = Sanitize(email).Trim();
        if (sanitized.Length == 0)
            return "***";

        var atIndex = sanitized.IndexOf('@');

        // Pas de '@' (ou en première position) : on ne peut pas séparer local-part/domaine de
        // façon fiable, on masque entièrement plutôt que de risquer une fuite partielle.
        if (atIndex <= 0)
            return "***";

        var localPart = sanitized[..atIndex];
        var domainPart = sanitized[(atIndex + 1)..];

        var firstChar = localPart[0];
        return Sanitize($"{firstChar}***@{domainPart}");
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
