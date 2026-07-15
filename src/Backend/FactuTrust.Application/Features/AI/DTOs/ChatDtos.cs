using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.AI.DTOs;

public sealed record ConversationDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime LastMessageAt { get; init; }
    public string? SelectedModel { get; init; }
    public int MessageCount { get; init; }
    /// <summary>Expert de module de la conversation (miroir int d'AssistantAgentScope, 0 = global).</summary>
    public int AgentScope { get; init; }
}

public sealed record ConversationDetailDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime LastMessageAt { get; init; }
    public string? SelectedModel { get; init; }
    public List<ChatMessageDto> Messages { get; init; } = new();
}

public sealed record ChatMessageDto
{
    public Guid Id { get; init; }
    public MessageRole Role { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public string? ToolName { get; init; }
    public string? ToolCallId { get; init; }
}

public sealed record ChatStreamEvent
{
    public required string Type { get; init; }
    public string? Content { get; init; }
    public string? ToolName { get; init; }
    public string? ToolCallId { get; init; }
    public Guid? ConversationId { get; init; }
    public string? Error { get; init; }
    /// <summary>JSON array of validated client navigation actions (type <c>client_actions</c>).</summary>
    public string? ClientActions { get; init; }
    /// <summary>JSON array of follow-up question strings (type <c>suggested_prompts</c>).</summary>
    public string? SuggestedPrompts { get; init; }
    /// <summary>JSON array of tool names and call ids used in the request (type <c>sources</c>).</summary>
    public string? Sources { get; init; }
    /// <summary>
    /// JSON object representing a validated <c>DashboardConfig</c> (title + sections),
    /// emitted right after <c>generate_dashboard_config</c> succeeds (type <c>dashboard</c>).
    /// </summary>
    public string? Dashboard { get; init; }
    /// <summary>Backend processing phase identifier (type <c>phase</c>).</summary>
    public string? Phase { get; init; }
    /// <summary>Phase execution status: running, completed, failed, or cancelled.</summary>
    public string? PhaseStatus { get; init; }
    /// <summary>Elapsed milliseconds reported for the phase when known.</summary>
    public long? ElapsedMs { get; init; }
    /// <summary>1-based orchestration round for repeated LLM phases.</summary>
    public int? Round { get; init; }
    /// <summary>Milliseconds to first token for the phase when available.</summary>
    public long? FirstTokenMs { get; init; }
    /// <summary>Whether the phase required tool calls.</summary>
    public bool? HadToolCalls { get; init; }
    /// <summary>Optional human-readable detail associated with the phase.</summary>
    public string? Detail { get; init; }

    public static ChatStreamEvent ContentChunk(string text) =>
        new() { Type = "content", Content = text };

    /// <summary>Replaces the entire assistant bubble content (post-synthesis enhanced body).</summary>
    public static ChatStreamEvent ContentReplace(string fullText) =>
        new() { Type = "content_replace", Content = fullText };

    public static ChatStreamEvent ToolCallStart(string toolName, string callId) =>
        new() { Type = "tool_call_start", ToolName = toolName, ToolCallId = callId };

    public static ChatStreamEvent ToolCallEnd(string toolName, string callId, long? elapsedMs = null) =>
        new() { Type = "tool_call_end", ToolName = toolName, ToolCallId = callId, ElapsedMs = elapsedMs };

    public static ChatStreamEvent Done(Guid conversationId) =>
        new() { Type = "done", ConversationId = conversationId };

    public static ChatStreamEvent ErrorEvent(string message) =>
        new() { Type = "error", Error = message };

    /// <summary>Keeps SSE connections alive through idle proxies (no semantic content).</summary>
    public static ChatStreamEvent Heartbeat() =>
        new() { Type = "heartbeat" };

    public static ChatStreamEvent ClientActionsEvent(string clientActionsJson) =>
        new() { Type = "client_actions", ClientActions = clientActionsJson };

    public static ChatStreamEvent SuggestedPromptsEvent(string promptsJson) =>
        new() { Type = "suggested_prompts", SuggestedPrompts = promptsJson };

    public static ChatStreamEvent SourcesEvent(string sourcesJson) =>
        new() { Type = "sources", Sources = sourcesJson };

    /// <summary>
    /// Emitted right after <c>generate_dashboard_config</c> succeeds so the UI can render the
    /// dashboard immediately, without depending on the LLM to copy the JSON into its reply.
    /// </summary>
    public static ChatStreamEvent DashboardEvent(string dashboardJson) =>
        new() { Type = "dashboard", Dashboard = dashboardJson };

    /// <summary>Studio system build progress step (type <c>studio_progress</c>).</summary>
    public static ChatStreamEvent StudioProgressEvent(string stepJson) =>
        new() { Type = "studio_progress", Content = stepJson };

    public static ChatStreamEvent PhaseEvent(
        string phase,
        string phaseStatus,
        long? elapsedMs = null,
        int? round = null,
        long? firstTokenMs = null,
        bool? hadToolCalls = null,
        string? detail = null) =>
        new()
        {
            Type = "phase",
            Phase = phase,
            PhaseStatus = phaseStatus,
            ElapsedMs = elapsedMs,
            Round = round,
            FirstTokenMs = firstTokenMs,
            HadToolCalls = hadToolCalls,
            Detail = detail
        };
}

public sealed record AiWarmUpRequest
{
    public string? Model { get; init; }
}
