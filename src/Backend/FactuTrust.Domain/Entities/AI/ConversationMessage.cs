using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.AI;

public class ConversationMessage : Entity
{
    public Guid ConversationId { get; private set; }
    public MessageRole Role { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public string? ToolName { get; private set; }
    public string? ToolCallId { get; private set; }

    /// <summary>
    /// When <see cref="Role"/> is Assistant and the model issued tool calls, JSON array of tool call descriptors for replay (OpenAI-compatible history).
    /// </summary>
    public string? ToolCallsJson { get; private set; }

    private ConversationMessage() { }

    internal static ConversationMessage Create(
        Guid conversationId,
        MessageRole role,
        string content,
        int sortOrder,
        string? toolName = null,
        string? toolCallId = null,
        string? toolCallsJson = null)
    {
        return new ConversationMessage
        {
            ConversationId = conversationId,
            Role = role,
            Content = content,
            SortOrder = sortOrder,
            ToolName = toolName,
            ToolCallId = toolCallId,
            ToolCallsJson = toolCallsJson
        };
    }
}
