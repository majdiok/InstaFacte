using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.DTOs;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class ConversationalFastPathTests
{
    [Theory]
    [InlineData("salut", true)]
    [InlineData("vous etes qui ?", true)]
    [InlineData("Bonjour", true)]
    public void ShouldUseConversationalFastPath_ForGreetingAndIdentity(string message, bool expected)
    {
        var intent = AiToolIntentRouter.Resolve(message, AssistantMode.Default);
        var result = AiToolIntentRouter.ShouldUseConversationalFastPath(intent, message);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Quel est mon chiffre d''affaires ce mois-ci ?", false)]
    [InlineData("qu''est ce que j''ai gagne aujourd''hui", false)]
    public void ShouldNotUseFastPath_ForSalesQuestions(string message, bool expected)
    {
        var intent = AiToolIntentRouter.Resolve(message, AssistantMode.Default);
        var result = AiToolIntentRouter.ShouldUseConversationalFastPath(intent, message);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildIdentityResponse_IsMeaningfulAtDefaultThreshold()
    {
        var body = AssistantDeterministicFallback.BuildIdentityResponse();
        Assert.True(AssistantVisibleContentFormatter.HasMeaningfulAssistantText(body, minChars: 80));
    }

    [Fact]
    public void BuildGreetingResponse_DoesNotMentionDashboard()
    {
        var body = AssistantDeterministicFallback.BuildGreetingResponse();
        Assert.DoesNotContain("tableau de bord", body, StringComparison.OrdinalIgnoreCase);
    }
}