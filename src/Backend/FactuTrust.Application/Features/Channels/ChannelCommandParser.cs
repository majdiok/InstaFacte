using System.Globalization;
using System.Text;

namespace FactuTrust.Application.Features.Channels;

public enum ChannelCommandKind
{
    /// <summary>Question libre transmise au pipeline IA.</summary>
    Question = 0,

    /// <summary>« LIER &lt;code&gt; » — liaison de l'identité externe à un compte FactuTrust.</summary>
    Link = 1,

    /// <summary>« DELIER » — suppression de la liaison.</summary>
    Unlink = 2,

    /// <summary>« AIDE » / « HELP » / « ? » — texte d'aide.</summary>
    Help = 3
}

public readonly record struct ChannelCommand(ChannelCommandKind Kind, string? Argument);

/// <summary>
/// Parseur des commandes de canal (insensible à la casse et aux accents : « délier » == « DELIER »).
/// Tout ce qui n'est pas une commande reconnue est une question libre.
/// </summary>
public static class ChannelCommandParser
{
    public static ChannelCommand Parse(string? text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return new ChannelCommand(ChannelCommandKind.Question, null);

        var normalized = RemoveDiacritics(trimmed).ToUpperInvariant();

        if (normalized is "AIDE" or "HELP" or "?")
            return new ChannelCommand(ChannelCommandKind.Help, null);

        if (normalized is "DELIER" or "UNLINK")
            return new ChannelCommand(ChannelCommandKind.Unlink, null);

        // « LIER » seul (code oublié) : commande Link sans argument — l'orchestrateur répond
        // avec la marche à suivre plutôt que de router vers l'IA.
        if (normalized is "LIER" or "LINK")
            return new ChannelCommand(ChannelCommandKind.Link, null);

        if (normalized.StartsWith("LIER ", StringComparison.Ordinal) ||
            normalized.StartsWith("LINK ", StringComparison.Ordinal))
        {
            var argument = NormalizeLinkCode(normalized[(normalized.IndexOf(' ') + 1)..]);
            return new ChannelCommand(ChannelCommandKind.Link, argument.Length == 0 ? null : argument);
        }

        return new ChannelCommand(ChannelCommandKind.Question, null);
    }

    /// <summary>Nettoie un code de liaison saisi : majuscules, sans espaces ni tirets.</summary>
    public static string NormalizeLinkCode(string? rawCode)
    {
        if (string.IsNullOrWhiteSpace(rawCode))
            return string.Empty;

        var builder = new StringBuilder(rawCode.Length);
        foreach (var c in rawCode.Trim().ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(c))
                builder.Append(c);
        }

        return builder.ToString();
    }

    private static string RemoveDiacritics(string text)
    {
        var formD = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(formD.Length);
        foreach (var c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
