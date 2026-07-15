namespace FactuTrust.Application.Features.AI.DTOs;

/// <summary>Correlation data for AI tool execution (audit, logs).</summary>
public sealed record AiToolExecutionContext(
    string? CorrelationId = null,
    Guid? ConversationId = null)
{
    public static AiToolExecutionContext Empty { get; } = new();
}
