using System;
using System.Linq;
using FactuTrust.Application.Features.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class AssistantVisibleContentFormatterTests
{
    [Fact]
    public void MeasureVisibleProseLength_IgnoresJsonFences()
    {
        const string content = "Bonjour\n```json\n{\"ca\":1}\n```";
        Assert.Equal(7, AssistantVisibleContentFormatter.MeasureVisibleProseLength(content));
    }

    [Fact]
    public void HasMeaningfulAssistantText_RejectsShortPreamble()
    {
        Assert.False(AssistantVisibleContentFormatter.HasMeaningfulAssistantText("Pour", minChars: 80));
    }

    [Fact]
    public void HasMeaningfulAssistantText_AcceptsLongProse()
    {
        var prose = new string('a', 100);
        Assert.True(AssistantVisibleContentFormatter.HasMeaningfulAssistantText(prose, minChars: 80));
    }

    [Fact]
    public void EnhanceForDisplay_HumanizesCaJsonOnlyReply()
    {
        const string content = "Pour\n```json\n{\"ca\":1250.5,\"periode\":\"16/06/2026\"}\n```";
        var enhanced = AssistantVisibleContentFormatter.EnhanceForDisplay(content, minMeaningfulChars: 80);
        Assert.Contains("TND", enhanced, StringComparison.Ordinal);
        Assert.Contains("16/06/2026", enhanced, StringComparison.Ordinal);
        Assert.True(enhanced.Length > 20);
    }

    [Fact]
    public void EnhanceForDisplay_LeavesLongProseUnchanged()
    {
        var prose = new string('x', 120);
        var enhanced = AssistantVisibleContentFormatter.EnhanceForDisplay(prose, minMeaningfulChars: 80);
        Assert.Equal(prose, enhanced);
    }

    private static int BulletCount(string text) =>
        text.Split('\n').Count(l => l.StartsWith("- ", StringComparison.Ordinal));

    [Fact]
    public void EnhanceForDisplay_HumanizesShortArray_AllRowsNoSuffix()
    {
        var rows = string.Join(",", Enumerable.Range(1, 3).Select(i => $"{{\"name\":\"Cli {i}\",\"amount\":{i * 10}}}"));
        var content = $"x\n```json\n[{rows}]\n```";

        var enhanced = AssistantVisibleContentFormatter.EnhanceForDisplay(content, minMeaningfulChars: 80);

        Assert.Equal(3, BulletCount(enhanced));
        Assert.DoesNotContain("autre(s) ligne(s)", enhanced, StringComparison.Ordinal);
    }

    [Fact]
    public void EnhanceForDisplay_HumanizesLargeArray_CapsAt50WithSuffix()
    {
        var rows = string.Join(",", Enumerable.Range(1, 60).Select(i => $"{{\"name\":\"Cli {i}\",\"amount\":{i}}}"));
        var content = $"x\n```json\n[{rows}]\n```";

        var enhanced = AssistantVisibleContentFormatter.EnhanceForDisplay(content, minMeaningfulChars: 80);

        Assert.Equal(50, BulletCount(enhanced));
        Assert.Contains("et 10 autre(s) ligne(s).", enhanced, StringComparison.Ordinal);
    }

    [Fact]
    public void EnhanceForDisplay_HumanizesRevenueEnvelope_TotalAndBreakdown()
    {
        const string envelope =
            "{\"totalRevenue\":2143.5,\"rowCount\":3,\"currency\":\"TND\",\"groupBy\":\"Product\"," +
            "\"rows\":[{\"groupKey\":\"ordinateur portable\",\"revenue\":1498,\"quantity\":1}," +
            "{\"groupKey\":\"bureau\",\"revenue\":476,\"quantity\":1}," +
            "{\"groupKey\":\"lit\",\"revenue\":169.5,\"quantity\":1}]}";
        var content = $"x\n```json\n{envelope}\n```";

        var enhanced = AssistantVisibleContentFormatter.EnhanceForDisplay(content, minMeaningfulChars: 80);

        // Le total déterministe (2 143,500) prime ; jamais la 1re ligne (1498) seule.
        Assert.Contains("CA total", enhanced, StringComparison.Ordinal);
        Assert.Contains("143,500", enhanced, StringComparison.Ordinal);
        Assert.Contains("ordinateur portable", enhanced, StringComparison.Ordinal);
        Assert.Equal(3, BulletCount(enhanced));
    }

    [Fact]
    public void EnhanceForDisplay_RevenueEnvelopeEmpty_SaysNoSales()
    {
        const string envelope =
            "{\"totalRevenue\":0,\"rowCount\":0,\"currency\":\"TND\",\"groupBy\":\"Product\",\"rows\":[]}";
        var content = $"x\n```json\n{envelope}\n```";

        var enhanced = AssistantVisibleContentFormatter.EnhanceForDisplay(content, minMeaningfulChars: 80);

        Assert.Contains("Aucune vente", enhanced, StringComparison.Ordinal);
        Assert.Equal(0, BulletCount(enhanced));
    }

    [Theory]
    [InlineData("je peux utiliser la fonction `resolve_reporting_period` pour obtenir des dates.")]
    [InlineData("Erreur lors de l'appel de get_stock_snapshot.")]
    [InlineData("get_sales_revenue(group_by=Client)")]
    public void ContainsInternalToolName_DetectsLeaks(string content)
    {
        Assert.True(AssistantVisibleContentFormatter.ContainsInternalToolName(content));
    }

    [Theory]
    [InlineData("Ce mois-ci votre chiffre d'affaires s'élève à 1 250,500 TND.")]
    [InlineData("Votre stock actuel comporte 3 articles en rupture.")]
    [InlineData("Cette fonction commerciale est essentielle pour vos marges.")]
    [InlineData("")]
    public void ContainsInternalToolName_IgnoresNormalProse(string content)
    {
        Assert.False(AssistantVisibleContentFormatter.ContainsInternalToolName(content));
    }

    [Fact]
    public void RedactStudioInternalLeaks_DropsToolNameAndJsonLines_KeepsUsefulProse()
    {
        const string content =
            "Oui, bien sûr ! Vous pouvez ajouter d'autres champs. Voici quelques suggestions :\n" +
            "1. Statut : pour suivre l'état du rendez-vous.\n" +
            "2. Durée : pour spécifier la durée.\n" +
            "Voici comment vous pouvez modifier le JSON pour inclure ces champs :\n" +
            "```json\n{\"system\":{}}\n```\n" +
            "Après avoir mis à jour le JSON, vous pouvez appeler `studio_generate_system` avec ce nouveau JSON.";

        var clean = AssistantVisibleContentFormatter.RedactStudioInternalLeaks(content);

        Assert.Contains("Statut", clean, StringComparison.Ordinal);
        Assert.Contains("Durée", clean, StringComparison.Ordinal);
        Assert.DoesNotContain("studio_generate_system", clean, StringComparison.Ordinal);
        Assert.DoesNotContain("JSON", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("```", clean, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactStudioInternalLeaks_AllInternal_ReturnsGenericAck()
    {
        const string content =
            "Voici le JSON mis à jour :\n```json\n{\"x\":1}\n```\n" +
            "Appelez `studio_generate_system` avec ce JSON.";

        var clean = AssistantVisibleContentFormatter.RedactStudioInternalLeaks(content);

        Assert.False(string.IsNullOrWhiteSpace(clean));
        Assert.DoesNotContain("studio_generate_system", clean, StringComparison.Ordinal);
        Assert.DoesNotContain("JSON", clean, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RedactStudioInternalLeaks_NormalProse_Unchanged()
    {
        const string content = "D'accord, j'ajoute les champs Statut et Durée à la table Rendez-vous.";
        var clean = AssistantVisibleContentFormatter.RedactStudioInternalLeaks(content);
        Assert.Equal(content, clean);
    }

    // ── SanitizeInternalToolNames : substitution par libellés (jamais de perte de contenu) ──────

    [Fact]
    public void Sanitize_ReplacesKnownToolNames_WithFrenchLabels()
    {
        const string content =
            "Utilisez les outils de prévision (forecast_product_demand) et get_promotion_recommendations pour optimiser.";

        var sanitized = AssistantVisibleContentFormatter.SanitizeInternalToolNames(content);

        Assert.DoesNotContain("forecast_product_demand", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("get_promotion_recommendations", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("la prévision de la demande produit", sanitized, StringComparison.Ordinal);
        Assert.Contains("les recommandations de promotion", sanitized, StringComparison.Ordinal);
        Assert.Contains("pour optimiser", sanitized, StringComparison.Ordinal);
        Assert.False(AssistantVisibleContentFormatter.ContainsInternalToolName(sanitized));
    }

    [Fact]
    public void Sanitize_AbsorbsBackticks_AroundToolNames()
    {
        const string content = "Analysez l'impact avec `analyze_seasonal_impact` avant de planifier.";

        var sanitized = AssistantVisibleContentFormatter.SanitizeInternalToolNames(content);

        Assert.DoesNotContain("`", sanitized, StringComparison.Ordinal);
        Assert.Contains("l'analyse de l'impact saisonnier", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitize_ReplacesUnknownPrefixedTokens_WithGenericLabel()
    {
        const string content = "Utilisez get_super_magic_report pour tout voir.";

        var sanitized = AssistantVisibleContentFormatter.SanitizeInternalToolNames(content);

        Assert.DoesNotContain("get_super_magic_report", sanitized, StringComparison.Ordinal);
        Assert.Contains("une analyse interne", sanitized, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Ce mois-ci votre chiffre d'affaires s'élève à 1 250,500 TND.")]
    [InlineData("Le code produit REF_2026 reste inchangé.")]
    public void Sanitize_LeavesNormalProseUntouched(string content)
    {
        Assert.Equal(content, AssistantVisibleContentFormatter.SanitizeInternalToolNames(content));
    }

    [Fact]
    public void Sanitize_PreservesJsonFences_Untouched()
    {
        const string content =
            "Voir get_sales_revenue.\n```json\n{\"tool\":\"get_sales_revenue\",\"x\":1}\n```";

        var sanitized = AssistantVisibleContentFormatter.SanitizeInternalToolNames(content);

        Assert.Contains("\"tool\":\"get_sales_revenue\"", sanitized, StringComparison.Ordinal);
        Assert.Contains("l'analyse du chiffre d'affaires", sanitized, StringComparison.Ordinal);
    }

    // ── ReplaceSectionsOnlyFences : indicateurs lisibles au lieu de JSON brut ────────────────────

    [Fact]
    public void SectionsFence_WithoutTitle_BecomesReadableBullets()
    {
        const string fence =
            "{\"sections\":[{\"type\":\"kpi_card\",\"title\":\"Solde Caisse Primaire\",\"data\":{\"value\":0,\"label\":\"TND\"}}," +
            "{\"type\":\"kpi_card\",\"title\":\"Solde Caisse Secondaire\",\"data\":{\"value\":6600.86,\"label\":\"TND\"}}]}";
        var content = "Indicateurs clés :\n```json\n" + fence + "\n```";

        var enhanced = AssistantVisibleContentFormatter.EnhanceForDisplay(content, minMeaningfulChars: 80);

        Assert.DoesNotContain("```", enhanced, StringComparison.Ordinal);
        Assert.Contains("- **Solde Caisse Primaire** :", enhanced, StringComparison.Ordinal);
        Assert.Contains("- **Solde Caisse Secondaire** :", enhanced, StringComparison.Ordinal);
        Assert.Contains("TND", enhanced, StringComparison.Ordinal);
    }

    [Fact]
    public void SectionsFence_WithTitle_IsDashboardTransport_LeftIntact()
    {
        const string fence = "{\"title\":\"Trésorerie\",\"sections\":[{\"type\":\"kpi_card\",\"title\":\"Solde\"}]}";
        var longProse = new string('x', 120);
        var content = longProse + "\n```json\n" + fence + "\n```";

        var enhanced = AssistantVisibleContentFormatter.EnhanceForDisplay(content, minMeaningfulChars: 80);

        Assert.Contains("```json", enhanced, StringComparison.Ordinal);
        Assert.Contains("\"title\":\"Trésorerie\"", enhanced, StringComparison.Ordinal);
    }

    [Fact]
    public void SectionsJsonFence_EchoOfToolArguments_BecomesBullets_EvenWithTitle()
    {
        // Écho des ARGUMENTS de generate_dashboard_config (capture « TABLEAU DE BORD SUGGÉRÉ »).
        const string fence =
            "{\"title\":\"Récapitulatif Trésorerie\",\"sections_json\":[" +
            "{\"type\":\"kpi_card\",\"title\":\"Solde Caisse Primaire\",\"data\":{\"value\":0,\"label\":\"TND\"}}," +
            "{\"type\":\"table\",\"title\":\"Flux de Trésorerie par Mode de Paiement\"}]}";
        var content = "Tableau de bord suggéré :\n```json\n" + fence + "\n```";

        var enhanced = AssistantVisibleContentFormatter.EnhanceForDisplay(content, minMeaningfulChars: 80);

        Assert.DoesNotContain("sections_json", enhanced, StringComparison.Ordinal);
        Assert.DoesNotContain("```", enhanced, StringComparison.Ordinal);
        Assert.Contains("- **Solde Caisse Primaire** :", enhanced, StringComparison.Ordinal);
        Assert.Contains("- Flux de Trésorerie par Mode de Paiement", enhanced, StringComparison.Ordinal);
    }

    [Fact]
    public void SectionsJsonFence_AsJsonString_AlsoBecomesBullets()
    {
        // sections_json transmis en CHAÎNE contenant un tableau JSON (forme réelle des arguments outil).
        const string fence =
            "{\"title\":\"Récap\",\"sections_json\":\"[{\\\"type\\\":\\\"kpi_card\\\",\\\"title\\\":\\\"Solde\\\",\\\"data\\\":{\\\"value\\\":10,\\\"label\\\":\\\"TND\\\"}}]\"}";
        var content = "x\n```json\n" + fence + "\n```";

        var enhanced = AssistantVisibleContentFormatter.EnhanceForDisplay(content, minMeaningfulChars: 80);

        Assert.DoesNotContain("sections_json", enhanced, StringComparison.Ordinal);
        Assert.Contains("- **Solde** :", enhanced, StringComparison.Ordinal);
    }

    [Fact]
    public void SectionsFence_InvalidJson_LeftIntact()
    {
        var longProse = new string('x', 120);
        var content = longProse + "\n```json\n{\"sections\":[oops\n```";

        var enhanced = AssistantVisibleContentFormatter.EnhanceForDisplay(content, minMeaningfulChars: 80);

        Assert.Contains("{\"sections\":[oops", enhanced, StringComparison.Ordinal);
    }

    // ── Enveloppe CA avec période ────────────────────────────────────────────────────────────────

    [Fact]
    public void RevenueEnvelope_WithPeriod_CitesPeriodInHeaderAndEmptyMessage()
    {
        const string envelope =
            "{\"totalRevenue\":100,\"rowCount\":1,\"currency\":\"TND\",\"groupBy\":\"Client\"," +
            "\"period\":{\"from\":\"2026-06-03\",\"to\":\"2026-07-02\"}," +
            "\"rows\":[{\"groupKey\":\"Client A\",\"revenue\":100,\"quantity\":1}]}";
        var content = $"x\n```json\n{envelope}\n```";

        var enhanced = AssistantVisibleContentFormatter.EnhanceForDisplay(content, minMeaningfulChars: 80);
        Assert.Contains("du 03/06/2026 au 02/07/2026", enhanced, StringComparison.Ordinal);

        const string empty =
            "{\"totalRevenue\":0,\"rowCount\":0,\"currency\":\"TND\",\"groupBy\":\"Client\"," +
            "\"period\":{\"from\":\"2026-07-01\",\"to\":\"2026-07-02\"},\"rows\":[]}";
        var enhancedEmpty = AssistantVisibleContentFormatter.EnhanceForDisplay(
            $"x\n```json\n{empty}\n```", minMeaningfulChars: 80);
        Assert.Contains("Aucune vente sur la periode du 01/07/2026 au 02/07/2026", enhancedEmpty, StringComparison.Ordinal);
    }
}
