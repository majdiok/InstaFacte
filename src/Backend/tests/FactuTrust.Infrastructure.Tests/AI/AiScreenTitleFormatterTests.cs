using FactuTrust.Application.Features.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class AiScreenTitleFormatterTests
{
    [Theory]
    [InlineData("accounting-ledger", "Analyse — Grand livre")]
    [InlineData("invoice-list", "Analyse — Liste des factures")]
    [InlineData("dashboard", "Analyse — Tableau de bord")]
    public void BuildScreenAnalysisConversationTitle_maps_known_screen_ids(string screenId, string expected)
    {
        Assert.Equal(expected, AiScreenTitleFormatter.BuildScreenAnalysisConversationTitle(screenId));
    }

    [Fact]
    public void BuildScreenAnalysisConversationTitle_returns_generic_title_when_screen_id_missing()
    {
        Assert.Equal("Analyse d'écran", AiScreenTitleFormatter.BuildScreenAnalysisConversationTitle(null));
        Assert.Equal("Analyse d'écran", AiScreenTitleFormatter.BuildScreenAnalysisConversationTitle(""));
    }

    [Fact]
    public void FormatScreenIdForTitle_humanizes_unknown_screen_ids()
    {
        Assert.Equal("Custom Report", AiScreenTitleFormatter.FormatScreenIdForTitle("custom-report"));
    }
}
