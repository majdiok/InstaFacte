using FactuTrust.Application.Features.AI;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class AssistantDeterministicFallbackTests
{
    [Fact]
    public void IdentityQuestion_ReturnsInstaFactIntro()
    {
        var convo = Conversation.Create(Guid.NewGuid(), "t");
        var result = AssistantDeterministicFallback.TryBuild(
            convo,
            "vous etes qui ?",
            minMeaningfulChars: 80,
            enabled: true,
            toolsExecutedThisRequest: false,
            toolIntent: AiToolIntentRouter.AiToolIntent.Greeting);
        Assert.Contains("InstaFact", result);
        Assert.True(result.Length >= 80);
    }

    [Fact]
    public void GreetingIntent_WithoutTools_ReturnsGreetingTemplate()
    {
        var convo = Conversation.Create(Guid.NewGuid(), "t");
        var result = AssistantDeterministicFallback.TryBuild(
            convo,
            "salut",
            minMeaningfulChars: 80,
            enabled: true,
            toolsExecutedThisRequest: false,
            toolIntent: AiToolIntentRouter.AiToolIntent.Greeting);
        Assert.Contains("Bonjour", result);
        Assert.DoesNotContain("tableau de bord", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoToolsExecuted_DoesNotReturnDashboardCatchAll()
    {
        var convo = Conversation.Create(Guid.NewGuid(), "t");
        convo.AddMessage(MessageRole.Tool, "{\"ca\":999}", "get_sales_revenue", "stale");
        var result = AssistantDeterministicFallback.TryBuild(
            convo,
            "salut",
            minMeaningfulChars: 80,
            enabled: true,
            toolsExecutedThisRequest: false,
            toolIntent: AiToolIntentRouter.AiToolIntent.Greeting);
        Assert.DoesNotContain("999", result);
        Assert.DoesNotContain("tableau de bord", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToolResultJson_HumanizesRevenue()
    {
        var convo = Conversation.Create(Guid.NewGuid(), "t");
        convo.AddMessage(MessageRole.User, "CA?");
        convo.AddMessage(MessageRole.Tool, "{\"ca\":1250.5,\"periode\":\"16/06/2026\"}", "get_sales_revenue", "c1");
        var result = AssistantDeterministicFallback.TryBuild(
            convo,
            "CA?",
            minMeaningfulChars: 20,
            enabled: true,
            toolsExecutedThisRequest: true,
            toolIntent: AiToolIntentRouter.AiToolIntent.Sales);
        Assert.Contains("TND", result);
    }

    [Fact]
    public void Disabled_ReturnsEmpty()
    {
        var convo = Conversation.Create(Guid.NewGuid(), "t");
        var result = AssistantDeterministicFallback.TryBuild(
            convo,
            "vous etes qui ?",
            minMeaningfulChars: 80,
            enabled: false,
            toolsExecutedThisRequest: false,
            toolIntent: AiToolIntentRouter.AiToolIntent.Greeting);
        Assert.Equal(string.Empty, result);
    }
}
