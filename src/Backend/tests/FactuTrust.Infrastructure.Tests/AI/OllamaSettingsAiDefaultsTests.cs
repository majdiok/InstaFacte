using FactuTrust.Application.Configuration;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Verrouille les valeurs par défaut des réglages introduits pour fiabiliser les réponses de l'assistant.
/// Évite qu'une régression silencieuse (renommage / défaut modifié) ne désactive les correctifs.
/// </summary>
public sealed class OllamaSettingsAiDefaultsTests
{
    [Fact]
    public void MonthPresetShortcut_EnabledByDefault()
    {
        Assert.True(new OllamaSettings().SalesMonthPresetShortcutEnabled);
    }

    [Fact]
    public void DefaultRankingRows_Is50()
    {
        Assert.Equal(50, new OllamaSettings().DefaultRankingRows);
    }

    [Fact]
    public void TodayPresetShortcut_RemainsEnabledByDefault()
    {
        Assert.True(new OllamaSettings().SalesTodayPresetShortcutEnabled);
    }

    [Fact]
    public void RedactInternalToolNames_EnabledByDefault()
    {
        Assert.True(new OllamaSettings().RedactInternalToolNamesEnabled);
    }

    [Fact]
    public void ComplianceCheckShortcut_EnabledByDefault()
    {
        Assert.True(new OllamaSettings().ComplianceCheckShortcutEnabled);
    }

    /// <summary>
    /// Bascule « Modèle avancé » du Studio : désactivée par défaut en C#, activée uniquement par
    /// configuration (appsettings). Un défaut à true exposerait la bascule sur toute installation
    /// n'ayant pas mis à jour sa configuration.
    /// </summary>
    [Fact]
    public void EnableStudioAiAdvancedModel_DisabledByDefault()
    {
        Assert.False(new OllamaSettings().EnableStudioAiAdvancedModel);
    }
}
