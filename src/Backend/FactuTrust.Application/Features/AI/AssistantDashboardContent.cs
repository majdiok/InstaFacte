using System.Text.Json;
using System.Text.RegularExpressions;

namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Detects whether an assistant message already embeds a valid <c>DashboardConfig</c> payload
/// inside a <c>```json</c> markdown fence, and produces the persistence appendix when the LLM
/// omitted the JSON. Mirrors the frontend extraction logic in
/// <c>assistant-message-display.ts</c> so the two stay in sync.
/// </summary>
public static class AssistantDashboardContent
{
    private static readonly Regex JsonFenceRegex = new(
        "```json\\s*([\\s\\S]*?)\\s*```",
        RegexOptions.Compiled);

    /// <summary>
    /// Returns true when <paramref name="content"/> contains at least one <c>```json</c> fence
    /// whose payload exposes both <c>title</c> and a <c>sections</c> array, i.e. a
    /// <c>DashboardConfig</c> shape.
    /// </summary>
    public static bool HasDashboardFence(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return false;

        var matches = JsonFenceRegex.Matches(content);
        foreach (Match m in matches)
        {
            var raw = m.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    continue;
                if (doc.RootElement.TryGetProperty("title", out _) &&
                    doc.RootElement.TryGetProperty("sections", out var sections) &&
                    sections.ValueKind == JsonValueKind.Array)
                    return true;
            }
            catch (JsonException)
            {
                // Invalid JSON fence — keep scanning subsequent fences.
            }
        }
        return false;
    }

    /// <summary>
    /// Builds the persistence appendix appended to the assistant content when the LLM did not
    /// re-emit the dashboard JSON itself. Keeps the same fence format expected by the frontend
    /// extractor so existing hydration logic continues to work unchanged.
    /// </summary>
    public static string BuildAppendix(string dashboardJson)
        => "\n\n```json\n" + dashboardJson + "\n```\n";
}
