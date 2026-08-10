using FactuTrust.Application.Features.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Rapprochement nom demandé / nom installé.
///
/// L'ancienne règle comparait la base avant le premier « : ». Elle déclarait donc
/// <c>qwen2.5:7b-instruct</c> installé alors que seul <c>qwen2.5:3b-instruct</c> l'était : le
/// contrôle de disponibilité passait, puis /api/chat renvoyait 404 — un faux positif qui déplaçait
/// le diagnostic très loin de sa cause.
/// </summary>
public sealed class OllamaModelNameTests
{
    [Theory]
    [InlineData("gemma3:4b", "gemma3:4b")]
    [InlineData("GEMMA3:4B", "gemma3:4b")]
    [InlineData("  gemma3:4b  ", "gemma3:4b")]
    public void Matches_SameTag_IsTrue(string requested, string installed)
    {
        Assert.True(OllamaModelName.Matches(requested, installed));
    }

    /// <summary>Souplesse conservée : Ollama traite un nom sans étiquette comme « :latest ».</summary>
    [Theory]
    [InlineData("gemma3", "gemma3:latest")]
    [InlineData("gemma3:latest", "gemma3")]
    [InlineData("mistral", "mistral")]
    public void Matches_ImplicitLatestTag_IsTrue(string requested, string installed)
    {
        Assert.True(OllamaModelName.Matches(requested, installed));
    }

    /// <summary>Le cœur du correctif : deux tailles du même modèle ne se valent pas.</summary>
    [Theory]
    [InlineData("qwen2.5:7b-instruct", "qwen2.5:3b-instruct")]
    [InlineData("gemma3:27b", "gemma3:4b")]
    [InlineData("gemma3", "gemma3:4b")]
    [InlineData("llama3.1:70b", "llama3.1:8b")]
    public void Matches_DifferentTagOfTheSameFamily_IsFalse(string requested, string installed)
    {
        Assert.False(OllamaModelName.Matches(requested, installed));
    }

    [Theory]
    [InlineData("gemma4:latest", "gemma3:4b")]
    [InlineData("inexistant", "gemma3:4b")]
    public void Matches_DifferentModel_IsFalse(string requested, string installed)
    {
        Assert.False(OllamaModelName.Matches(requested, installed));
    }

    [Theory]
    [InlineData(null, "gemma3:4b")]
    [InlineData("", "gemma3:4b")]
    [InlineData("   ", "gemma3:4b")]
    [InlineData("gemma3:4b", null)]
    [InlineData("gemma3:4b", "")]
    public void Matches_EmptyOperand_IsFalse(string? requested, string? installed)
    {
        Assert.False(OllamaModelName.Matches(requested, installed));
    }

    [Theory]
    [InlineData("gemma3", "gemma3:latest")]
    [InlineData("gemma3:4b", "gemma3:4b")]
    [InlineData("  ", "")]
    public void Normalize_AppendsLatestOnlyWhenTagIsAbsent(string input, string expected)
    {
        Assert.Equal(expected, OllamaModelName.Normalize(input));
    }
}
