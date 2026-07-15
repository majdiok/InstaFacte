using FactuTrust.Application.Features.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class AiToolIntentRouterRankingAdviceTests
{
    // ── TryInferClientRankingTopN ────────────────────────────────────────────

    [Theory]
    [InlineData("Quels sont mes 5 meilleurs clients ?", 5)]
    [InlineData("top 3 clients", 3)]
    [InlineData("mes meilleurs clients", AiToolIntentRouter.DefaultClientRankingTopN)]
    [InlineData("les pires clients ce mois", AiToolIntentRouter.DefaultClientRankingTopN)]
    [InlineData("Top 10 clients par CA", 10)]
    [InlineData("classement des clients", AiToolIntentRouter.DefaultClientRankingTopN)]
    public void ClientRanking_Detected_WithTopN(string message, int expectedTopN)
    {
        Assert.Equal(expectedTopN, AiToolIntentRouter.TryInferClientRankingTopN(message));
    }

    [Theory]
    [InlineData("quel est mon chiffre d'affaires aujourd'hui ?")]
    [InlineData("mes meilleurs produits")]
    [InlineData("liste des clients")]
    [InlineData("")]
    [InlineData(null)]
    public void ClientRanking_NotDetected_ForOtherQuestions(string? message)
    {
        Assert.Null(AiToolIntentRouter.TryInferClientRankingTopN(message));
    }

    [Fact]
    public void ClientRanking_TopN_IsClampedTo50()
    {
        Assert.Equal(50, AiToolIntentRouter.TryInferClientRankingTopN("top 500 clients"));
    }

    // ── LooksLikeAdviceQuestion ──────────────────────────────────────────────

    [Theory]
    [InlineData("comment je peux gagner plus d'après les données qui existent")]
    [InlineData("quels sont vos conseils pour diminuer la perte de marchandises non vendus")]
    [InlineData("comment améliorer mes marges ?")]
    [InlineData("des suggestions pour optimiser mon stock ?")]
    public void AdviceQuestion_Detected(string message)
    {
        Assert.True(AiToolIntentRouter.LooksLikeAdviceQuestion(message));
    }

    [Theory]
    [InlineData("quel est mon CA aujourd'hui ?")]
    [InlineData("mes 5 meilleurs clients")]
    [InlineData("état du stock")]
    [InlineData("")]
    [InlineData(null)]
    public void AdviceQuestion_NotDetected_ForDataQuestions(string? message)
    {
        Assert.False(AiToolIntentRouter.LooksLikeAdviceQuestion(message));
    }

    [Fact]
    public void AdvicePromptSuffix_ForbidsInternalIdentifiers()
    {
        Assert.Contains("identifiants techniques internes", AiToolIntentRouter.AdvicePromptSuffix);
        Assert.Contains("CONCRÈTES", AiToolIntentRouter.AdvicePromptSuffix);
    }
}
