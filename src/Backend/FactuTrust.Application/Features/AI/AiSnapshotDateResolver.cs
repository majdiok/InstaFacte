using System;
using System.Globalization;

namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Résolution TOLÉRANTE de la date de l'état du stock (<c>as_of_date</c>) à la frontière de l'outil IA.
/// Un petit modèle omet parfois la date ou en passe une invalide / future, ce qui faisait échouer
/// <c>get_stock_snapshot</c> (et exposait une erreur + un nom d'outil à l'utilisateur). Ce résolveur ne lève
/// JAMAIS d'exception : date absente/illisible → aujourd'hui ; date future → ramenée à aujourd'hui ; date passée
/// valide → conservée. Logique PURE (testable). N'altère pas le handler de requête partagé avec les rapports.
/// </summary>
public static class AiSnapshotDateResolver
{
    private static readonly string[] AcceptedFormats =
    {
        "yyyy-MM-dd",
        "yyyy-MM-ddTHH:mm:ss",
        "dd/MM/yyyy"
    };

    /// <summary>
    /// Renvoie la date d'instantané effective (heure = minuit) bornée à <paramref name="today"/>.
    /// </summary>
    public static DateTime Resolve(string? raw, DateOnly today)
    {
        var todayDate = today.ToDateTime(TimeOnly.MinValue);

        if (string.IsNullOrWhiteSpace(raw))
            return todayDate;

        if (!DateTime.TryParseExact(
                raw.Trim(),
                AcceptedFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return todayDate;
        }

        var parsedDate = parsed.Date;
        return parsedDate > todayDate ? todayDate : parsedDate;
    }
}
