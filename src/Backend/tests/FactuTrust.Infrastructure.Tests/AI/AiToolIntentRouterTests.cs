using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.Commands;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class AiToolIntentRouterTests
{
    [Theory]
    [InlineData("Bonjour", AiToolIntentRouter.AiToolIntent.Greeting)]
    [InlineData("Salut !", AiToolIntentRouter.AiToolIntent.Greeting)]
    [InlineData("Merci beaucoup", AiToolIntentRouter.AiToolIntent.Greeting)]
    public void Resolves_Greeting(string message, AiToolIntentRouter.AiToolIntent expected)
    {
        var intent = AiToolIntentRouter.Resolve(message, AssistantMode.Default);
        Assert.Equal(expected, intent);
    }

    [Theory]
    [InlineData("Quel est mon chiffre d'affaires ce mois-ci ?", AiToolIntentRouter.AiToolIntent.Sales)]
    [InlineData("Quels sont mes meilleurs clients ?", AiToolIntentRouter.AiToolIntent.Sales)]
    [InlineData("Combien ai-je encaissé cette semaine ?", AiToolIntentRouter.AiToolIntent.Sales)]
    public void Resolves_Sales(string message, AiToolIntentRouter.AiToolIntent expected)
    {
        var intent = AiToolIntentRouter.Resolve(message, AssistantMode.Default);
        Assert.Equal(expected, intent);
    }

    [Theory]
    [InlineData("Affiche l'état du stock", AiToolIntentRouter.AiToolIntent.Stock)]
    [InlineData("Quels articles sont en rupture ?", AiToolIntentRouter.AiToolIntent.Stock)]
    public void Resolves_Stock(string message, AiToolIntentRouter.AiToolIntent expected)
    {
        var intent = AiToolIntentRouter.Resolve(message, AssistantMode.Default);
        Assert.Equal(expected, intent);
    }

    // ── Détection du raccourci conformité (TryInferComplianceCheckInvoice) ──

    [Fact]
    public void ComplianceCheck_Detects_Latest_Invoice_Request()
    {
        var detection = AiToolIntentRouter.TryInferComplianceCheckInvoice(
            "Vérifie la conformité de ma dernière facture");
        Assert.NotNull(detection);
        Assert.Null(detection!.InvoiceNumber);
    }

    [Fact]
    public void ComplianceCheck_Extracts_Invoice_Number_Uppercased()
    {
        var detection = AiToolIntentRouter.TryInferComplianceCheckInvoice(
            "la facture fac-2026-000123 est-elle conforme ?");
        Assert.NotNull(detection);
        Assert.Equal("FAC-2026-000123", detection!.InvoiceNumber);
    }

    [Fact]
    public void ComplianceCheck_Detects_Non_Conforme_Phrasing()
    {
        var detection = AiToolIntentRouter.TryInferComplianceCheckInvoice(
            "Est-ce que ma facture est non conforme ?");
        Assert.NotNull(detection);
    }

    [Theory]
    [InlineData("Quelle est la conformité TVA de mon entreprise ?")] // pas de « facture »
    [InlineData("Vérifie ma facture")] // pas de mot conformité
    [InlineData("Quel est mon chiffre d'affaires ce mois-ci ?")]
    [InlineData("")]
    [InlineData(null)]
    public void ComplianceCheck_Returns_Null_For_Non_Compliance_Questions(string? message)
    {
        Assert.Null(AiToolIntentRouter.TryInferComplianceCheckInvoice(message));
    }

    /// <summary>Le suffixe de synthèse ne cite aucun identifiant snake_case (garde anti-fuite jamais sollicitée).</summary>
    [Fact]
    public void ComplianceShortcutPromptSuffix_Contains_No_Snake_Case()
    {
        Assert.False(AiToolIntentRouter.ComplianceShortcutPromptSuffix.Contains('_'));
    }

    [Fact]
    public void ScreenAnalysis_Always_Fallback()
    {
        var intent = AiToolIntentRouter.Resolve("Quel est mon CA ?", AssistantMode.ScreenAnalysis);
        Assert.Equal(AiToolIntentRouter.AiToolIntent.Fallback, intent);
    }

    [Fact]
    public void Greeting_Excludes_All_Tools()
    {
        Assert.False(AiToolIntentRouter.ShouldIncludeTool(
            "get_sales_revenue",
            AiToolIntentRouter.AiToolIntent.Greeting,
            enableMutationTools: false,
            isMutating: false));
    }

    [Fact]
    public void Sales_Includes_Revenue_Tool()
    {
        Assert.True(AiToolIntentRouter.ShouldIncludeTool(
            "get_sales_revenue",
            AiToolIntentRouter.AiToolIntent.Sales,
            enableMutationTools: false,
            isMutating: false));
    }

    [Fact]
    public void Fallback_Includes_Any_Read_Tool()
    {
        Assert.True(AiToolIntentRouter.ShouldIncludeTool(
            "get_stock_snapshot",
            AiToolIntentRouter.AiToolIntent.Fallback,
            enableMutationTools: false,
            isMutating: false));
    }

    [Fact]
    public void Fallback_CpuCoreSubset_Limits_Tools()
    {
        Assert.True(AiToolIntentRouter.ShouldIncludeTool(
            "get_sales_revenue",
            AiToolIntentRouter.AiToolIntent.Fallback,
            enableMutationTools: false,
            isMutating: false,
            useCpuCoreSubset: true));
        Assert.False(AiToolIntentRouter.ShouldIncludeTool(
            "get_product_sales_trend",
            AiToolIntentRouter.AiToolIntent.Fallback,
            enableMutationTools: false,
            isMutating: false,
            useCpuCoreSubset: true));
    }

    [Theory]
    [InlineData("Quel est mon CA ce mois-ci ?", AiToolIntentRouter.AiToolIntent.Sales)]
    [InlineData("Mon CA du mois", AiToolIntentRouter.AiToolIntent.Sales)]
    [InlineData("qu'est ce que j'ai gagné aujourd'hui", AiToolIntentRouter.AiToolIntent.Sales)]
    [InlineData("combien j'ai gagné aujourd'hui", AiToolIntentRouter.AiToolIntent.Sales)]
    public void Resolves_Sales_CaVariants(string message, AiToolIntentRouter.AiToolIntent expected)
    {
        var intent = AiToolIntentRouter.Resolve(message, AssistantMode.Default);
        Assert.Equal(expected, intent);
    }

    // ── Lot 2.1 : le fragment "ca" ne doit matcher que comme mot entier ──
    // (régression : "cabinet", "cadeau", "caisse", "occasion" contiennent "ca" mais ne sont
    // pas des questions de chiffre d'affaires ; aucun autre mot-clé Sales n'est présent dans
    // ces phrases, ce qui isole précisément le comportement du fragment "ca").
    [Theory]
    [InlineData("Le cabinet est ouvert")]
    [InlineData("J'ai achete un cadeau")]
    [InlineData("Ouvre la caisse")]
    [InlineData("C'est une occasion")]
    public void Does_Not_Resolve_Sales_On_Ca_Substring_Inside_Other_Words(string message)
    {
        var intent = AiToolIntentRouter.Resolve(message, AssistantMode.Default);
        Assert.NotEqual(AiToolIntentRouter.AiToolIntent.Sales, intent);
    }

    [Theory]
    [InlineData("mon ca aujourd'hui")]
    [InlineData("ca du mois")]
    [InlineData("CA ce mois-ci")]
    public void Resolves_Sales_On_Ca_As_Whole_Word(string message)
    {
        var intent = AiToolIntentRouter.Resolve(message, AssistantMode.Default);
        Assert.Equal(AiToolIntentRouter.AiToolIntent.Sales, intent);
    }

    // ── Lot 2 : le scope FirmMission n'est jamais affecté par une éventuelle
    // classification Sales (catalogue, raccourcis, budget de rounds neutralisent l'effet). ──
    [Fact]
    public void FirmMission_Scope_Does_Not_Expose_Sales_Tools_Even_If_Intent_Is_Sales()
    {
        var toolNames = AiAgentScopeCatalog.GetToolNames(AssistantAgentScope.FirmMission);
        Assert.DoesNotContain("get_sales_revenue", toolNames);
    }

    [Theory]
    [InlineData("Où en est le portefeuille du cabinet aujourd'hui ?")]
    [InlineData("Combien d'échéances sont en retard et chez combien de clients ?")]
    [InlineData("Combien de dossiers ont une échéance dans les 7 prochains jours ?")]
    [InlineData("Quelles échéances sont en retard et chez quels clients ?")]
    public void FirmMission_RoundBudget_Is_One_Regardless_Of_Raw_Intent_Misclassification(string message)
    {
        // Ces 4 questions du cabinet peuvent encore être classées Sales par le routeur générique
        // (via les mots-clés indépendants "client"/"combien", hors périmètre du correctif 2.1),
        // mais c'est sans effet observable pour le scope FirmMission : le budget de rounds est
        // forcé à 1 quel que soit l'intent (cf. Lot 2.2), et le catalogue FirmMission n'expose pas
        // get_sales_revenue (cf. test ci-dessus), donc aucun raccourci Sales ne peut se déclencher.
        var intent = AiToolIntentRouter.Resolve(message, AssistantMode.Default);
        var rounds = SendChatMessageHandler.ResolveMaxToolCallRounds(
            isScreenAnalysis: false,
            screenAnalysisMaxRounds: 3,
            defaultMaxRounds: 2,
            cpuMaxToolCallRounds: 1,
            assistantMode: AssistantMode.Default,
            toolIntent: intent,
            inferenceProfile: new OllamaInferenceProfile(OllamaInferenceDevice.CpuOnly, NumGpu: 0, NumThread: 8, NumBatch: 128, PreferAdaptiveChatNumCtx: false),
            agentScope: AssistantAgentScope.FirmMission);
        Assert.Equal(1, rounds);
    }

    [Fact]
    public void Sales_CpuIntentSubset_Excludes_Secondary_Tools()
    {
        Assert.True(AiToolIntentRouter.ShouldIncludeTool(
            "get_sales_revenue",
            AiToolIntentRouter.AiToolIntent.Sales,
            enableMutationTools: false,
            isMutating: false,
            useCpuIntentSubset: true));
        Assert.False(AiToolIntentRouter.ShouldIncludeTool(
            "get_product_performance",
            AiToolIntentRouter.AiToolIntent.Sales,
            enableMutationTools: false,
            isMutating: false,
            useCpuIntentSubset: true));
        Assert.False(AiToolIntentRouter.ShouldIncludeTool(
            "get_tunisian_commercial_calendar",
            AiToolIntentRouter.AiToolIntent.Sales,
            enableMutationTools: false,
            isMutating: false,
            useCpuIntentSubset: true));
    }

    [Theory]
    [InlineData("vous êtes qui ?", AiToolIntentRouter.AiToolIntent.Greeting)]
    [InlineData("qui es-tu ?", AiToolIntentRouter.AiToolIntent.Greeting)]
    public void Resolves_Identity_AsGreeting(string message, AiToolIntentRouter.AiToolIntent expected)
    {
        var intent = AiToolIntentRouter.Resolve(message, AssistantMode.Default);
        Assert.Equal(expected, intent);
    }

    [Theory]
    // Prompt exact émis par le bouton « Graphique » du frontend (suggestChartFollowUp).
    [InlineData("A partir des chiffres ci-dessus, genere un graphique pertinent (barres, lignes ou camembert) avec generate_dashboard_config.")]
    // Ancienne formulation (avec « ajouter ») doit aussi router vers Chart grâce à « chart » / generate_dashboard_config.
    [InlineData("A partir des donnees ci-dessus, utilise generate_dashboard_config pour ajouter une section \"chart\" pertinente (bar, line ou pie).")]
    [InlineData("Fais-moi un diagramme camembert")]
    [InlineData("Affiche un histogramme des ventes")]
    public void Resolves_Chart(string message)
    {
        var intent = AiToolIntentRouter.Resolve(message, AssistantMode.Default);
        Assert.Equal(AiToolIntentRouter.AiToolIntent.Chart, intent);
    }

    [Fact]
    public void Chart_Excludes_Crm_Mutation_Tools_Even_When_Mutations_Enabled()
    {
        // Cœur du correctif : une demande de graphique ne doit jamais exposer un outil d'écriture CRM,
        // même si EnableMutationTools est actif (cas appsettings.Development).
        Assert.False(AiToolIntentRouter.ShouldIncludeTool(
            "create_crm_opportunity",
            AiToolIntentRouter.AiToolIntent.Chart,
            enableMutationTools: true,
            isMutating: true));
        Assert.False(AiToolIntentRouter.ShouldIncludeTool(
            "create_crm_activity",
            AiToolIntentRouter.AiToolIntent.Chart,
            enableMutationTools: true,
            isMutating: true));
    }

    [Fact]
    public void Chart_Includes_Dashboard_And_Core_Read_Tools()
    {
        Assert.True(AiToolIntentRouter.ShouldIncludeTool(
            "generate_dashboard_config",
            AiToolIntentRouter.AiToolIntent.Chart,
            enableMutationTools: true,
            isMutating: false));
        Assert.True(AiToolIntentRouter.ShouldIncludeTool(
            "get_stock_snapshot",
            AiToolIntentRouter.AiToolIntent.Chart,
            enableMutationTools: true,
            isMutating: false));
    }

    [Fact]
    public void Chart_Excludes_NonChart_Read_Tools()
    {
        Assert.False(AiToolIntentRouter.ShouldIncludeTool(
            "get_product_performance",
            AiToolIntentRouter.AiToolIntent.Chart,
            enableMutationTools: false,
            isMutating: false));
    }

    [Theory]
    // Outils paramétriques/spécialisés qui déraillaient les suivis : exclus de l'intent Synthesis.
    [InlineData("analyze_seasonal_impact")]
    [InlineData("get_replenishment_recommendations")]
    [InlineData("forecast_revenue")]
    [InlineData("get_promotion_recommendations")]
    public void Synthesis_Excludes_Parametric_OffContext_Tools(string toolName)
    {
        Assert.False(AiToolIntentRouter.ShouldIncludeTool(
            toolName,
            AiToolIntentRouter.AiToolIntent.Synthesis,
            enableMutationTools: true,
            isMutating: false));
    }

    [Theory]
    // Cœur lecture + dashboard : disponible pour répondre à un suivi à partir des données/analyse.
    [InlineData("get_accounting_dashboard")]
    [InlineData("get_sales_revenue")]
    [InlineData("get_stock_snapshot")]
    [InlineData("generate_dashboard_config")]
    public void Synthesis_Includes_Core_Read_And_Dashboard_Tools(string toolName)
    {
        Assert.True(AiToolIntentRouter.ShouldIncludeTool(
            toolName,
            AiToolIntentRouter.AiToolIntent.Synthesis,
            enableMutationTools: true,
            isMutating: false));
    }

    [Fact]
    public void Synthesis_Excludes_Crm_Mutation_Tools()
    {
        Assert.False(AiToolIntentRouter.ShouldIncludeTool(
            "create_crm_activity",
            AiToolIntentRouter.AiToolIntent.Synthesis,
            enableMutationTools: true,
            isMutating: true));
    }

    [Theory]
    // Non-régression : Synthesis n'est JAMAIS déduit par mots-clés (il vient du flag de suivi).
    [InlineData("Quels sont les 3 principaux risques à traiter cette semaine ?")]
    [InlineData("Quelles actions concrètes recommandes-tu en priorité ?")]
    public void Synthesis_Is_Never_Inferred_By_Keywords(string message)
    {
        var intent = AiToolIntentRouter.Resolve(message, AssistantMode.Default);
        Assert.NotEqual(AiToolIntentRouter.AiToolIntent.Synthesis, intent);
    }

    [Theory]
    // Non-régression : les demandes métier classiques ne basculent pas vers Chart.
    [InlineData("Quel est mon chiffre d'affaires ce mois-ci ?", AiToolIntentRouter.AiToolIntent.Sales)]
    [InlineData("Affiche l'état du stock", AiToolIntentRouter.AiToolIntent.Stock)]
    public void Chart_DoesNot_Capture_Business_Queries(string message, AiToolIntentRouter.AiToolIntent expected)
    {
        var intent = AiToolIntentRouter.Resolve(message, AssistantMode.Default);
        Assert.Equal(expected, intent);
    }

    [Theory]
    [InlineData("qu'est ce que j'ai gagné aujourd'hui", "today")]
    [InlineData("CA aujourd'hui", "today")]
    [InlineData("CA ce mois", "current_month")]
    [InlineData("Quel est mon chiffre d'affaires ce mois-ci ?", "current_month")]
    [InlineData("mon CA du mois en cours", "current_month")]
    public void InferSalesPreset_ReturnsExpected(string message, string expected)
    {
        Assert.Equal(expected, AiToolIntentRouter.InferSalesPreset(message));
    }
}