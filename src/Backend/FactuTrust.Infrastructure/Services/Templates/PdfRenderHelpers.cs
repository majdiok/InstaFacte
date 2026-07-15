using System.Globalization;
using System.Text;
using Humanizer;

namespace FactuTrust.Infrastructure.Services.Templates;

/// <summary>
/// Helpers de rendu PDF partagés entre <see cref="PdfService"/> (modèle historique) et les
/// modèles visuels unifiés (<see cref="DocumentTemplateBase"/>). Centralise la logique pour éviter
/// toute divergence entre les rendus.
/// </summary>
public static class PdfRenderHelpers
{
    private static readonly CultureInfo FrCulture = new("fr-FR");

    /// <summary>
    /// Nettoie le texte en remplaçant les caractères de contrôle (CR, LF, TAB, NUL) par des espaces.
    /// </summary>
    public static string CleanTextForPdf(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;

        return text
            .Replace("\r\n", " ")
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\t", " ")
            .Replace("\0", string.Empty)
            .Trim();
    }

    /// <summary>
    /// Formate un montant en toutes lettres en français (dinars + millimes), sur la valeur absolue.
    /// </summary>
    public static string FormatAmountInFrench(decimal amount)
    {
        amount = Math.Abs(amount);
        var dinars = (long)Math.Floor(amount);
        var milli = (int)Math.Round((amount - dinars) * 1000m, MidpointRounding.AwayFromZero);
        if (milli >= 1000)
        {
            dinars++;
            milli = 0;
        }

        var sb = new StringBuilder();
        sb.Append(dinars.ToWords(FrCulture));
        sb.Append(dinars == 1 ? " dinar" : " dinars");
        if (milli > 0)
        {
            sb.Append(" et ");
            sb.Append(milli.ToWords(FrCulture));
            sb.Append(milli == 1 ? " millime" : " millimes");
        }

        return sb.ToString();
    }

    /// <summary>Découpe une adresse multi-lignes en lignes non vides.</summary>
    public static IReadOnlyList<string> SplitAddressLines(string? multiLine)
    {
        if (string.IsNullOrWhiteSpace(multiLine))
            return Array.Empty<string>();

        return multiLine
            .Split(new[] { Environment.NewLine, "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();
    }
}
