using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class AiConversationMessageMapperTests
{
    [Fact]
    public void BuildOllamaMessages_StripsCjkFromAssistantHistory_KeepsUserAndToolIntact()
    {
        const string french = "Aucun collaborateur n'a été trouvé dans votre portefeuille actuel.";
        const string cjkUser = "助手：question utilisateur";
        const string cjkTool = "{\"collaborateurs\":[],\"note\":\"中文\"}";

        var convo = Conversation.Create(Guid.NewGuid(), "t");
        convo.AddMessage(MessageRole.User, cjkUser);
        convo.AddMessage(MessageRole.Tool, cjkTool, "get_firm_collaborator_workload", "c1");
        convo.AddMessage(MessageRole.Assistant, french + "\n\n助手：未找到任何协作人员。");

        var messages = AiConversationMessageMapper.BuildOllamaMessages("sys", convo);

        var user = messages.Single(m => m.Role == "user");
        Assert.Equal(cjkUser, user.Content);

        var tool = messages.Single(m => m.Role == "tool");
        Assert.Equal(cjkTool, tool.Content);

        var assistant = messages.Single(m => m.Role == "assistant");
        Assert.Equal(french, assistant.Content?.Trim());
        Assert.DoesNotContain("助手", assistant.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildOpenAiMessages_StripsCjkFromAssistantHistory_KeepsUserIntact()
    {
        const string french = "La charge est répartie entre trois collaborateurs.";
        var convo = Conversation.Create(Guid.NewGuid(), "t");
        convo.AddMessage(MessageRole.User, "Comment se répartit la charge ?");
        convo.AddMessage(MessageRole.Assistant, french + "\n助手：翻译。");

        var messages = AiConversationMessageMapper.BuildOpenAiMessages("sys", convo);

        var assistant = messages.Single(m => m.Role == "assistant");
        Assert.Equal(french, (assistant.Content as string)?.Trim());
        Assert.DoesNotContain("助手", assistant.Content as string, StringComparison.Ordinal);

        var user = messages.Single(m => m.Role == "user");
        Assert.Equal("Comment se répartit la charge ?", user.Content);
    }

    [Fact]
    public void BuildOllamaMessages_DoesNotStripAssistantToolCallTurn()
    {
        var toolCallsJson = ToolCallsPersistenceHelper.Serialize(
        [
            new OllamaToolCall
            {
                Id = "1",
                Function = new OllamaToolCallFunction { Name = "get_firm_collaborator_workload" }
            }
        ]);
        var convo = Conversation.Create(Guid.NewGuid(), "t");
        convo.AddMessage(MessageRole.Assistant, "助手：préambule", toolCallsJson: toolCallsJson);

        var messages = AiConversationMessageMapper.BuildOllamaMessages("sys", convo);
        var assistant = messages.Single(m => m.Role == "assistant");
        Assert.Equal("助手：préambule", assistant.Content);
        Assert.NotNull(assistant.ToolCalls);
        Assert.NotEmpty(assistant.ToolCalls);
    }
}
