using System.Text.Json;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.AI;

public static class AiConversationMessageMapper
{
    private const int DefaultMaxContextMessages = 10;
    private const int DefaultMaxToolResultChars = 8000;

    public static List<OllamaChatMessage> BuildOllamaMessages(string systemPrompt, Conversation conversation, int maxMessages = DefaultMaxContextMessages, int maxToolResultChars = DefaultMaxToolResultChars)
    {
        var messages = new List<OllamaChatMessage>
        {
            new() { Role = "system", Content = systemPrompt }
        };

        var recentMessages = conversation.Messages
            .OrderBy(m => m.SortOrder)
            .TakeLast(Math.Max(1, maxMessages));

        foreach (var msg in recentMessages)
        {
            messages.Add(MapOllamaMessage(msg, maxToolResultChars));
        }

        return messages;
    }

    private static OllamaChatMessage MapOllamaMessage(ConversationMessage msg, int maxToolResultChars)
    {
        return msg.Role switch
        {
            MessageRole.Assistant when !string.IsNullOrEmpty(msg.ToolCallsJson)
                => new OllamaChatMessage
                {
                    Role = "assistant",
                    Content = msg.Content,
                    ToolCalls = ToolCallsPersistenceHelper.Deserialize(msg.ToolCallsJson)
                },
            MessageRole.Tool => new OllamaChatMessage
            {
                Role = "tool",
                Content = TruncateForLlm(msg.Content, maxToolResultChars)
            },
            MessageRole.Assistant => new OllamaChatMessage { Role = "assistant", Content = SanitizeAssistantHistory(msg.Content) },
            MessageRole.User => new OllamaChatMessage { Role = "user", Content = msg.Content },
            MessageRole.System => new OllamaChatMessage { Role = "system", Content = msg.Content },
            _ => new OllamaChatMessage { Role = "user", Content = msg.Content }
        };
    }

    /// <summary>
    /// Le SDK Cursor n'a pas de rôle system : le prompt FactuTrust et l'historique sont
    /// aplatis dans un unique message utilisateur (même fenêtre que Ollama/OpenAI).
    /// </summary>
    public static string BuildCursorUserMessage(
        string systemPrompt,
        Conversation conversation,
        int maxMessages = DefaultMaxContextMessages,
        int maxToolResultChars = DefaultMaxToolResultChars)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[SYSTEM]");
        sb.AppendLine(systemPrompt ?? string.Empty);
        sb.AppendLine();
        sb.AppendLine("[CONVERSATION]");

        var recentMessages = conversation.Messages
            .OrderBy(m => m.SortOrder)
            .TakeLast(Math.Max(1, maxMessages));

        foreach (var msg in recentMessages)
        {
            var role = msg.Role switch
            {
                MessageRole.Assistant => "assistant",
                MessageRole.Tool => "tool",
                MessageRole.System => "system",
                _ => "user"
            };
            var content = msg.Role == MessageRole.Tool
                ? TruncateForLlm(msg.Content, maxToolResultChars)
                : msg.Role == MessageRole.Assistant
                    ? SanitizeAssistantHistory(msg.Content)
                    : msg.Content;
            if (msg.Role == MessageRole.Tool && !string.IsNullOrEmpty(msg.ToolName))
                sb.AppendLine($"{role}({msg.ToolName}): {content}");
            else
                sb.AppendLine($"{role}: {content}");
        }

        return sb.ToString();
    }

    public static List<OpenAiChatMessagePayload> BuildOpenAiMessages(string systemPrompt, Conversation conversation, int maxMessages = DefaultMaxContextMessages, int maxToolResultChars = DefaultMaxToolResultChars)
    {
        var messages = new List<OpenAiChatMessagePayload>
        {
            new() { Role = "system", Content = systemPrompt }
        };

        var recentMessages = conversation.Messages
            .OrderBy(m => m.SortOrder)
            .TakeLast(Math.Max(1, maxMessages));

        foreach (var msg in recentMessages)
        {
            messages.Add(MapOpenAiMessage(msg, maxToolResultChars));
        }

        return messages;
    }

    private static OpenAiChatMessagePayload MapOpenAiMessage(ConversationMessage msg, int maxToolResultChars)
    {
        return msg.Role switch
        {
            MessageRole.Assistant when !string.IsNullOrEmpty(msg.ToolCallsJson)
                => new OpenAiChatMessagePayload
                {
                    Role = "assistant",
                    Content = string.IsNullOrEmpty(msg.Content) ? null : msg.Content,
                    ToolCalls = DeserializeOpenAiToolCalls(msg.ToolCallsJson)
                },
            MessageRole.Tool => new OpenAiChatMessagePayload
            {
                Role = "tool",
                ToolCallId = msg.ToolCallId,
                Content = TruncateForLlm(msg.Content, maxToolResultChars)
            },
            MessageRole.Assistant => new OpenAiChatMessagePayload { Role = "assistant", Content = SanitizeAssistantHistory(msg.Content) },
            MessageRole.User => new OpenAiChatMessagePayload { Role = "user", Content = msg.Content },
            MessageRole.System => new OpenAiChatMessagePayload { Role = "system", Content = msg.Content },
            _ => new OpenAiChatMessagePayload { Role = "user", Content = msg.Content }
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static List<OpenAiToolCallPayload> DeserializeOpenAiToolCalls(string json)
    {
        try
        {
            var items = JsonSerializer.Deserialize<List<StoredCallRecord>>(json, JsonOptions);
            if (items is null)
                return new List<OpenAiToolCallPayload>();

            return items.Select(item => new OpenAiToolCallPayload
            {
                Id = item.Id,
                Type = "function",
                Function = new OpenAiToolFunctionPayload
                {
                    Name = item.Name,
                    Arguments = string.IsNullOrEmpty(item.ArgumentsJson) ? "{}" : item.ArgumentsJson
                }
            }).ToList();
        }
        catch
        {
            return new List<OpenAiToolCallPayload>();
        }
    }

    private sealed class StoredCallRecord
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string ArgumentsJson { get; set; } = "";
    }

    private static string TruncateForLlm(string? content, int maxChars)
    {
        var limit = Math.Max(1, maxChars);
        if (string.IsNullOrEmpty(content) || content.Length <= limit)
            return content ?? string.Empty;
        return content[..limit] + "\n... [résultat tronqué]";
    }

    /// <summary>
    /// Retire le CJK des messages assistant rejoués vers le LLM (conversations déjà persistées
    /// avant le filet). Ne touche pas aux messages user/tool ni aux tours avec tool_calls.
    /// </summary>
    private static string SanitizeAssistantHistory(string? content) =>
        AssistantVisibleContentFormatter.StripDisallowedScripts(content);
}
