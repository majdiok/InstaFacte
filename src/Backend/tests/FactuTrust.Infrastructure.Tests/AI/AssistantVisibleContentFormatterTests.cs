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

    [Fact]
    public void RedactStudioInternalLeaks_UnclosedJsonFence_Removed()
    {
        const string content =
            "Voici le brouillon :\n```json\n{\n  \"entity\": {\n    \"displayName\": \"Contrats Clients\",\n    \"displayNamePlural\": \"Contrats Clients\"\n  },\n  \"fields\": [\n    { \"label\": \"Montant\"";

        var clean = AssistantVisibleContentFormatter.RedactStudioInternalLeaks(content);

        Assert.DoesNotContain("```", clean, StringComparison.Ordinal);
        Assert.DoesNotContain("\"entity\"", clean, StringComparison.Ordinal);
        Assert.DoesNotContain("Montant", clean, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(clean));
    }

    [Fact]
    public void RedactStudioInternalLeaks_BareEntitySpec_Removed_KeepsProse()
    {
        const string content =
            "Je prépare la table des contrats.\n{\"entity\":{\"displayName\":\"Contrats Clients\"},\"fields\":[{\"label\":\"Montant\",\"type\":\"money\"}]}";

        var clean = AssistantVisibleContentFormatter.RedactStudioInternalLeaks(content);

        Assert.Contains("Je prépare la table des contrats.", clean, StringComparison.Ordinal);
        Assert.DoesNotContain("\"entity\"", clean, StringComparison.Ordinal);
        Assert.DoesNotContain("Montant", clean, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactStudioInternalLeaks_BareEntitySpecOnly_ReturnsGenericAck()
    {
        const string content =
            "{\"entity\":{\"displayName\":\"Contrats Clients\"},\"fields\":[{\"label\":\"Montant\",\"type\":\"money\"}]}";

        var clean = AssistantVisibleContentFormatter.RedactStudioInternalLeaks(content);

        // Le texte de repli est désormais une constante : sa longueur est contractuelle (elle doit
        // franchir MinAssistantTextCharsForCompleteResponse, cf. StudioSilentFailureGuardsTests) et
        // il ne présume plus d'une création de table.
        Assert.Equal(AssistantVisibleContentFormatter.StudioRedactionFallback, clean);
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

    // ── StripDisallowedScripts : filet anti-dérive CJK (Qwen) ─────────────────────────────────────

    [Fact]
    public void StripDisallowedScripts_LeavesFrenchProseUnchanged()
    {
        const string content = "Ce mois-ci votre chiffre d'affaires s'élève à 1 250,500 TND (16/06/2026).";
        Assert.False(AssistantVisibleContentFormatter.ContainsCjkScript(content));
        Assert.Equal(content, AssistantVisibleContentFormatter.StripDisallowedScripts(content));
    }

    [Fact]
    public void StripDisallowedScripts_KeepsFrench_DropsChineseParagraphsAndTranslatorNote()
    {
        const string french =
            "Aucun collaborateur n'a été trouvé dans votre portefeuille actuel. "
            + "Cela pourrait signifier que tous les dossiers sont bien suivis par vos équipes, "
            + "ou qu'il y a une partie du portefeuille qui n'a pas pu être lue.";
        var content =
            french
            + "\n\n助手：未找到任何协作人员。\n\n"
            + "注意：以上翻译保持了原文的语气和内容，并已根据中文表达习惯进行了适当调整。";

        var stripped = AssistantVisibleContentFormatter.StripDisallowedScripts(content);

        Assert.Equal(french, stripped.Trim());
        Assert.DoesNotContain("助手", stripped, StringComparison.Ordinal);
        Assert.False(AssistantVisibleContentFormatter.ContainsCjkScript(stripped));
        Assert.True(AssistantVisibleContentFormatter.HasMeaningfulAssistantText(stripped, minChars: 80));
    }

    [Fact]
    public void StripDisallowedScripts_AllCjk_YieldsEmpty_NotMeaningful()
    {
        const string content = "助手：未找到任何协作人员。请检查您的投资组合。";
        var stripped = AssistantVisibleContentFormatter.StripDisallowedScripts(content);

        Assert.True(string.IsNullOrWhiteSpace(stripped));
        Assert.False(AssistantVisibleContentFormatter.HasMeaningfulAssistantText(stripped, minChars: 80));
    }

    [Fact]
    public void StripDisallowedScripts_PreservesArabic()
    {
        const string content = "Le client شركة النور a un solde de 200,000 TND.";
        Assert.False(AssistantVisibleContentFormatter.ContainsCjkScript(content));
        Assert.Equal(content, AssistantVisibleContentFormatter.StripDisallowedScripts(content));
    }

    [Fact]
    public void StripDisallowedScripts_IsIdempotent()
    {
        const string content = "Réponse française.\n\n助手：中文续写。";
        var once = AssistantVisibleContentFormatter.StripDisallowedScripts(content);
        var twice = AssistantVisibleContentFormatter.StripDisallowedScripts(once);
        Assert.Equal(once, twice);
    }

    [Fact]
    public void StripDisallowedScripts_PreservesJsonFences_EvenWithCjkInside()
    {
        const string content =
            "Voici le tableau.\n```json\n{\"title\":\"Trésorerie\",\"note\":\"中文\"}\n```\n助手：忽略。";

        var stripped = AssistantVisibleContentFormatter.StripDisallowedScripts(content);

        Assert.Contains("```json", stripped, StringComparison.Ordinal);
        Assert.Contains("\"note\":\"中文\"", stripped, StringComparison.Ordinal);
        Assert.Contains("Voici le tableau.", stripped, StringComparison.Ordinal);
        Assert.DoesNotContain("助手", stripped, StringComparison.Ordinal);
    }

    [Fact]
    public void StripDisallowedScripts_NullAndEmpty_AreSafe()
    {
        Assert.Equal(string.Empty, AssistantVisibleContentFormatter.StripDisallowedScripts(null));
        Assert.Equal(string.Empty, AssistantVisibleContentFormatter.StripDisallowedScripts(""));
    }

    [Fact]
    public void SanitizeVisibleProse_StripsCjkAfterToolNameSubstitution()
    {
        const string content = "Utilisez get_sales_revenue.\n助手：忽略。";
        var sanitized = AssistantVisibleContentFormatter.SanitizeVisibleProse(content);
        Assert.DoesNotContain("get_sales_revenue", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("l'analyse du chiffre d'affaires", sanitized, StringComparison.Ordinal);
        Assert.False(AssistantVisibleContentFormatter.ContainsCjkScript(sanitized));
    }
}
