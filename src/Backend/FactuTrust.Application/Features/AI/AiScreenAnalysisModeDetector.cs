using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Application.Features.AI;

public static class AiScreenAnalysisModeDetector
{
    public static bool IsScreenAnalysis(ChatUiContextDto? uiContext, ChatRequestOptionsDto? options)
    {
        if (options?.AssistantMode == AssistantMode.ScreenAnalysis)
            return true;

        return !string.IsNullOrWhiteSpace(uiContext?.AnalysisSummary);
    }

    public static string? ResolveScreenId(ChatUiContextDto? uiContext)
    {
        if (!string.IsNullOrWhiteSpace(uiContext?.ScreenId))
            return uiContext.ScreenId;

        if (string.IsNullOrWhiteSpace(uiContext?.AnalysisSummary))
            return null;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(uiContext.AnalysisSummary);
            if (doc.RootElement.TryGetProperty("screenId", out var sid) && sid.ValueKind == System.Text.Json.JsonValueKind.String)
                return sid.GetString();
        }
        catch
        {
            // ignore malformed snapshot
        }

        return null;
    }
}
