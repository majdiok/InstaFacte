using System.Text.Json;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.AI.Tools;
using FactuTrust.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.AI;

public sealed class CursorToolCallbackService
{
    private readonly ICursorToolRunRegistry _registry;
    private readonly ILogger<CursorToolCallbackService> _logger;

    public CursorToolCallbackService(
        ICursorToolRunRegistry registry,
        ILogger<CursorToolCallbackService> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public async Task<(int Status, object Body)> ExecuteAsync(
        Guid runId,
        string? token,
        CursorToolCallbackRequest request,
        CancellationToken cancellationToken)
    {
        if (!_registry.TryGet(runId, out var ctx) || !CursorToolRunRegistry.TokenEquals(ctx.Token, token))
            return (401, new { error = "unauthorized" });

        var toolName = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(toolName))
            return (400, new { error = "missing tool name" });

        var callId = string.IsNullOrWhiteSpace(request.CallId)
            ? Guid.NewGuid().ToString("N")[..12]
            : request.CallId.Trim();
        var args = ToArguments(request.Arguments);

        ctx.ExtraEvents.Enqueue(ChatStreamEvent.ToolCallStart(toolName, callId));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        AiToolResult result;
        try
        {
            result = await ctx.ScopeFactory.ExecuteAsync(
                toolName,
                args,
                new AiToolExecutionContext(ctx.CorrelationId, ctx.Conversation.Id),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cursor tool {Tool} failed for run {RunId}", toolName, runId);
            result = AiToolResult.Error($"Erreur lors de l'exécution: {ex.Message}");
        }

        var content = result.Success ? result.Data : $"Erreur: {result.ErrorMessage}";
        ctx.Conversation.AddMessage(MessageRole.Tool, content, toolName, callId);
        ctx.ToolsExecuted++;
        ctx.ToolSources.Add((toolName, callId));
        // Grounding gate (Lot 1.3 du plan v3) : distinct de ToolsExecuted (incrémenté même en
        // erreur, ci-dessus, comportement conservé) — FirmGroundedReads ne compte que les lectures
        // firm réellement exploitables. Couverture Cursor garantie : aucune option d'exclusion.
        if (AiParallelDbToolPolicy.IsFirmReadOnly(toolName) && result.IsGrounded)
            ctx.FirmGroundedReads++;
        ctx.ExtraEvents.Enqueue(ChatStreamEvent.ToolCallEnd(toolName, callId, sw.ElapsedMilliseconds));

        if (result.Success && !string.IsNullOrWhiteSpace(result.Data))
        {
            if (toolName == "propose_client_actions")
                ctx.ExtraEvents.Enqueue(ChatStreamEvent.ClientActionsEvent(result.Data));
            if (toolName == FirmAgentTools.SendReminder &&
                FirmReminderClientActionExtractor.TryBuildClientActionsJson(result.Data, out var firmReminderActionsJson))
                ctx.ExtraEvents.Enqueue(ChatStreamEvent.ClientActionsEvent(firmReminderActionsJson!));
            if (toolName == "propose_follow_up_prompts")
            {
                ctx.ExtraEvents.Enqueue(ChatStreamEvent.SuggestedPromptsEvent(result.Data));
                AccumulatePrompts(result.Data, ctx.AccumulatedSuggestedPrompts);
            }
            if (toolName == "generate_dashboard_config")
            {
                ctx.AccumulatedDashboardJson = result.Data;
                ctx.ExtraEvents.Enqueue(ChatStreamEvent.DashboardEvent(result.Data));
            }
            // R7/PR 2.4 : la liste des outils « plan » poussés au client est centralisée dans le
            // registre (elle couvre désormais aussi studio_plan_changes et studio_plan_view).
            if (AiToolRegistry.StudioPlanEmittingTools.Contains(toolName))
                ctx.ExtraEvents.Enqueue(ChatStreamEvent.StudioPlanEvent(result.Data));
        }

        if (!result.Success && toolName is "studio_generate_app" or "studio_generate_system" or "studio_plan_app" or "studio_plan_system")
            ctx.StudioBuilderToolError = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? "La création du système a échoué."
                : result.ErrorMessage;

        return (200, new { content, isError = !result.Success });
    }

    private static Dictionary<string, object?> ToArguments(JsonElement? arguments)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (arguments is not { ValueKind: JsonValueKind.Object } obj)
            return dict;
        foreach (var prop in obj.EnumerateObject())
            dict[prop.Name] = ConvertValue(prop.Value);
        return dict;
    }

    private static object? ConvertValue(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => el.GetRawText()
    };

    private static void AccumulatePrompts(string json, List<string> accumulated)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return;
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.String)
                    continue;
                var s = el.GetString();
                if (!string.IsNullOrEmpty(s) && !accumulated.Contains(s))
                    accumulated.Add(s);
            }
        }
        catch
        {
            // ignore malformed follow-up JSON
        }
    }
}

public sealed class CursorToolCallbackRequest
{
    public string? Name { get; init; }
    public string? CallId { get; init; }
    public JsonElement? Arguments { get; init; }
}
