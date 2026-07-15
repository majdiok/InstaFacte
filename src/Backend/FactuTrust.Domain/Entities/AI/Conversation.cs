using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.AI;

public class Conversation : Entity
{
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateTime LastMessageAt { get; private set; }
    public string? SelectedModel { get; private set; }

    /// <summary>
    /// Expert de module de la conversation (miroir int de AssistantAgentScope côté Application).
    /// 0 = assistant global (valeur historique de toutes les conversations existantes).
    /// Stampé à la création, jamais modifié ensuite : sert au cloisonnement des historiques.
    /// </summary>
    public int AgentScope { get; private set; }

    private readonly List<ConversationMessage> _messages = new();
    public IReadOnlyList<ConversationMessage> Messages => _messages.AsReadOnly();

    private Conversation() { }

    public static Conversation Create(Guid userId, string title, string? model = null, int agentScope = 0)
    {
        var conversation = new Conversation
        {
            UserId = userId,
            Title = title,
            LastMessageAt = DateTime.UtcNow,
            SelectedModel = model,
            AgentScope = agentScope
        };
        return conversation;
    }

    public ConversationMessage AddMessage(
        MessageRole role,
        string content,
        string? toolName = null,
        string? toolCallId = null,
        string? toolCallsJson = null)
    {
        var message = ConversationMessage.Create(Id, role, content, _messages.Count, toolName, toolCallId, toolCallsJson);
        _messages.Add(message);
        LastMessageAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        return message;
    }

    public void UpdateTitle(string newTitle)
    {
        Title = newTitle;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Replaces in-memory messages after a partial DB load (chat context window).</summary>
    public void ReplaceMessagesForChatContext(IEnumerable<ConversationMessage> messages)
    {
        _messages.Clear();
        _messages.AddRange(messages.OrderBy(m => m.SortOrder));
    }
}
