using FactuTrust.Application.Features.AI.Commands;
using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Resolves the effective assistant mode and mutation-tool exposure for chat requests.
/// </summary>
public static class AssistantModeResolver
{
    private static readonly string[] MutationKeywords =
    {
        "cree", "creer", "creez", "ajoute", "ajouter", "modifi", "met a jour", "mettre a jour", "mets a jour",
        "supprim", "efface", "effacer", "valide", "valider", "enregistr", "encaiss", "appliqu", "bon de commande",
        "ajuste", "ajuster", "renomm", "confirm", "vas-y", "vas y", "fais-le", "fais le"
    };

    public static AssistantMode Resolve(SendChatMessageCommand command)
    {
        if (command.Options?.AssistantMode == AssistantMode.ScreenAnalysis)
            return AssistantMode.ScreenAnalysis;

        if (command.Options?.AssistantMode == AssistantMode.Compliance)
            return AssistantMode.Compliance;

        if (command.Options?.AssistantMode == AssistantMode.StudioBuilder)
            return AssistantMode.StudioBuilder;

        if (!string.IsNullOrWhiteSpace(command.UiContext?.AnalysisSummary))
            return AssistantMode.ScreenAnalysis;

        return AssistantMode.Default;
    }

    public static bool ShouldEnableMutationTools(
        AssistantMode assistantMode,
        bool enableMutationToolsPlatformFlag,
        string? mutationIntentText)
    {
        if (!enableMutationToolsPlatformFlag)
            return false;

        return assistantMode == AssistantMode.StudioBuilder || QueryLikelyMutating(mutationIntentText);
    }

    public static bool QueryLikelyMutating(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var normalized = RemoveDiacritics(text).ToLowerInvariant();
        foreach (var kw in MutationKeywords)
        {
            if (normalized.Contains(kw, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static string RemoveDiacritics(string text)
    {
        var decomposed = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }
}
