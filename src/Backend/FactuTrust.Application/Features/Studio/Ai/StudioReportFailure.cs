using FactuTrust.Application.Features.Studio.Common.SqlReport;

namespace FactuTrust.Application.Features.Studio.Ai;

/// <summary>Une reformulation qui, elle, aboutit : l'état visé et la phrase à envoyer.</summary>
public sealed record StudioReportFailureSuggestion(string Preset, string Label, string Prompt);

/// <summary>
/// Ce que l'interface doit montrer quand un état ne peut pas être calculé : ce qui a été tenté,
/// pourquoi, et par quoi le remplacer.
/// </summary>
public sealed record StudioReportFailurePayload(
    string Message,
    string? Preset,
    string? Title,
    string? PeriodLabel,
    IReadOnlyList<StudioReportFailureSuggestion> Suggestions);

/// <summary>
/// Compose la charge utile d'un échec d'état.
///
/// Raison d'être : jusqu'ici un échec n'était qu'une phrase poussée dans la bulle de l'assistant —
/// l'utilisateur voyait un message d'erreur sans savoir ce qui avait été tenté ni quoi faire
/// ensuite. <see cref="StudioReportIntentRouter.SuggestPresets"/> existait déjà pour cela, écrite
/// pour « proposer des formulations qui fonctionnent quand rien n'a pu être exécuté », mais n'était
/// appelée nulle part. C'est ce chaînon manquant.
///
/// Pur et sans dépendance : entièrement testable unitairement.
/// </summary>
public static class StudioReportFailure
{
    public const int MaxSuggestions = 3;

    public static StudioReportFailurePayload Build(
        string? userMessage,
        string message,
        string? presetKey = null,
        string? periodLabel = null)
    {
        var attempted = presetKey is not null ? SqlReportPresetCatalog.Find(presetKey) : null;

        // On ne propose jamais de rejouer l'état qui vient d'échouer.
        var suggestions = StudioReportIntentRouter.SuggestPresets(userMessage, MaxSuggestions + 1)
            .Where(key => !string.Equals(key, attempted?.Key, StringComparison.OrdinalIgnoreCase))
            .Select(SqlReportPresetCatalog.Find)
            .Where(preset => preset is not null)
            .Take(MaxSuggestions)
            .Select(preset => new StudioReportFailureSuggestion(
                preset!.Key, preset.DisplayName, $"Montre-moi l'état « {preset.DisplayName} »"))
            .ToList();

        return new StudioReportFailurePayload(
            string.IsNullOrWhiteSpace(message) ? "Le calcul de l'état a échoué." : message,
            attempted?.Key,
            attempted?.DisplayName,
            periodLabel,
            suggestions);
    }
}
