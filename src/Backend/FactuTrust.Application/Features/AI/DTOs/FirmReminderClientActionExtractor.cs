using System.Text.Json;

namespace FactuTrust.Application.Features.AI.DTOs;

/// <summary>
/// Extrait l'action en attente structurée (<c>confirm_firm_reminder</c>) embarquée dans le payload
/// JSON d'une PREVIEW de <c>send_fiscal_deadline_reminder</c>, et la met à la forme JSON attendue
/// par l'événement SSE <c>client_actions</c> (tableau). Vit dans l'Application (et non
/// l'Infrastructure) pour rester partageable sans violation de couche entre le chemin tenant
/// (<c>SendChatMessageCommand</c>, Application) et le chemin Cursor
/// (<c>CursorToolCallbackService</c>, Infrastructure, qui référence déjà l'Application).
/// </summary>
public static class FirmReminderClientActionExtractor
{
    /// <summary>Nom du champ porté par le payload d'outil de la PREVIEW (FirmAgentToolExecutor).</summary>
    public const string PendingActionProperty = "actionEnAttente";

    /// <summary>
    /// Renvoie <c>true</c> et le tableau JSON <c>[ { kind: "confirm_firm_reminder", ... } ]</c> si le
    /// payload d'outil contient une action en attente exploitable ; sinon <c>false</c>.
    /// </summary>
    public static bool TryBuildClientActionsJson(string? toolResultData, out string? clientActionsJson)
    {
        clientActionsJson = null;

        if (string.IsNullOrWhiteSpace(toolResultData))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(toolResultData);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            if (!doc.RootElement.TryGetProperty(PendingActionProperty, out var action)
                || action.ValueKind != JsonValueKind.Object)
                return false;

            clientActionsJson = $"[{action.GetRawText()}]";
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
