using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.AI.Tools;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class AiAgentScopeCatalogTests
{
    /// <summary>
    /// Garde anti-dérive (même esprit que AiToolFrenchLabelsTests) : tout nom de tout sous-ensemble de
    /// scope DOIT exister dans AiToolRegistry.All — un renommage/suppression d'outil casse le build au
    /// lieu de vider silencieusement le catalogue d'un expert.
    /// </summary>
    [Fact]
    public void Every_Scope_Tool_Exists_In_Registry()
    {
        var registryNames = AiToolRegistry.All.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var scope in AiAgentScopeCatalog.AllScopes)
        {
            var unknown = AiAgentScopeCatalog.GetToolNames(scope)
                .Where(name => !registryNames.Contains(name))
                .ToList();
            Assert.True(unknown.Count == 0, $"Scope {scope} : outils inconnus du registre : {string.Join(", ", unknown)}");
        }
    }

    [Fact]
    public void Cpu_Subset_Is_Contained_In_Full_Subset()
    {
        foreach (var scope in AiAgentScopeCatalog.AllScopes)
        {
            var full = AiAgentScopeCatalog.GetToolNames(scope);
            var outside = AiAgentScopeCatalog.GetCpuToolNames(scope)
                .Where(name => !full.Contains(name))
                .ToList();
            Assert.True(outside.Count == 0, $"Scope {scope} : outils CPU hors du sous-ensemble complet : {string.Join(", ", outside)}");
        }
    }

    /// <summary>La variante CPU reste petite (≤ 8) et en lecture seule : c'est sa raison d'être.</summary>
    [Fact]
    public void Cpu_Subset_Is_Small_And_ReadOnly()
    {
        foreach (var scope in AiAgentScopeCatalog.AllScopes)
        {
            var cpu = AiAgentScopeCatalog.GetCpuToolNames(scope);
            Assert.True(cpu.Count is > 0 and <= 8, $"Scope {scope} : variante CPU de taille {cpu.Count} (attendu 1-8)");
            foreach (var name in cpu)
            {
                var def = AiToolRegistry.GetToolDefinition(name);
                Assert.NotNull(def);
                Assert.False(def!.IsMutating, $"Scope {scope} : outil CPU mutant interdit : {name}");
            }
        }
    }

    /// <summary>Le noyau commun (période, suivi, dashboard, calendrier) est présent dans chaque expert.</summary>
    [Fact]
    public void Core_Tools_Present_In_Every_Scope()
    {
        string[] core =
        {
            "resolve_reporting_period",
            "propose_follow_up_prompts",
            "generate_dashboard_config",
            "get_tunisian_commercial_calendar"
        };
        foreach (var scope in AiAgentScopeCatalog.AllScopes)
        {
            var tools = AiAgentScopeCatalog.GetToolNames(scope);
            foreach (var name in core)
                Assert.True(tools.Contains(name), $"Scope {scope} : outil noyau manquant : {name}");
        }
    }

    [Fact]
    public void Persona_And_DisplayName_Defined_For_Every_Scope()
    {
        foreach (var scope in AiAgentScopeCatalog.AllScopes)
        {
            Assert.False(string.IsNullOrWhiteSpace(AiAgentScopeCatalog.GetDisplayName(scope)));
            Assert.False(string.IsNullOrWhiteSpace(AiAgentScopeCatalog.GetPersonaPromptSection(scope, compact: false)));
            Assert.False(string.IsNullOrWhiteSpace(AiAgentScopeCatalog.GetPersonaPromptSection(scope, compact: true)));
        }
    }

    [Fact]
    public void None_Scope_Has_No_Tools_And_No_Persona()
    {
        Assert.Empty(AiAgentScopeCatalog.GetToolNames(AssistantAgentScope.None));
        Assert.Empty(AiAgentScopeCatalog.GetCpuToolNames(AssistantAgentScope.None));
        Assert.Equal(string.Empty, AiAgentScopeCatalog.GetPersonaPromptSection(AssistantAgentScope.None, compact: false));
    }

    /// <summary>
    /// Les 4 suggestions affichées par chaque expert (frontend :
    /// src/Frontend/factutrust-web/src/app/features/ai-assistant/config/agent-scopes.config.ts)
    /// doivent être répondables sur CPU : leurs outils « ancres » DOIVENT figurer dans la variante CPU
    /// du scope (sur CPU + intent Fallback, seul ce sous-ensemble est exposé au modèle). C'est la cause
    /// racine du bug « Expert Comptabilité demande un GUID » : outil absent ⇒ mauvaise réponse.
    /// </summary>
    [Fact]
    public void Cpu_Subsets_Contain_Suggestion_Anchor_Tools()
    {
        var accounting = AiAgentScopeCatalog.GetCpuToolNames(AssistantAgentScope.Accounting);
        Assert.Contains("get_accounting_dashboard", accounting);   // « tableau de bord comptable »
        Assert.Contains("get_client_balances", accounting);        // « soldes clients et fournisseurs »
        Assert.Contains("get_supplier_balances", accounting);
        Assert.Contains("compliance_check_invoice", accounting);   // « conformité de ma dernière facture »
        Assert.Contains("get_client_aging", accounting);           // « créances de plus de 90 jours »
        Assert.Contains("search_invoices", accounting);

        var stock = AiAgentScopeCatalog.GetCpuToolNames(AssistantAgentScope.Stock);
        Assert.Contains("get_stock_snapshot", stock);              // « état du stock » / « ruptures »
        Assert.Contains("get_products_never_sold", stock);         // « jamais vendus »
        Assert.Contains("get_abc_xyz_classification", stock);      // « classification ABC/XYZ »

        var treasury = AiAgentScopeCatalog.GetCpuToolNames(AssistantAgentScope.Treasury);
        Assert.Contains("get_client_payments", treasury);          // « encaissements ce mois-ci »
        Assert.Contains("get_client_aging", treasury);             // « créances +90 j » / « relancer »
        Assert.Contains("search_supplier_invoices", treasury);     // « factures fournisseurs à échéance »

        var purchases = AiAgentScopeCatalog.GetCpuToolNames(AssistantAgentScope.Purchases);
        Assert.Contains("search_supplier_invoices", purchases);    // « factures fournisseurs payées »
        Assert.Contains("search_purchase_orders", purchases);      // « bons de commande en attente »
        Assert.Contains("get_replenishment_recommendations", purchases);

        var sales = AiAgentScopeCatalog.GetCpuToolNames(AssistantAgentScope.Sales);
        Assert.Contains("get_sales_revenue", sales);               // « CA » / « meilleurs clients »
        Assert.Contains("search_invoices", sales);                 // « factures en retard »
        Assert.Contains("get_product_performance", sales);         // « produits qui se vendent le mieux »

        var crm = AiAgentScopeCatalog.GetCpuToolNames(AssistantAgentScope.Crm);
        Assert.Contains("search_crm_opportunities", crm);          // « opportunités ouvertes » / « pipeline »
        Assert.Contains("search_crm_activities", crm);             // « activités cette semaine »
        Assert.Contains("propose_client_actions", crm);            // « clients à relancer »
    }

    /// <summary>
    /// Les personas ne citent aucun identifiant snake_case : la garde anti-fuite substitue les noms
    /// d'outils dans les réponses, pas dans le prompt — un nom d'outil dans la persona inciterait le
    /// modèle à le recopier tel quel.
    /// </summary>
    [Fact]
    public void Persona_Sections_Contain_No_Snake_Case_Tool_Names()
    {
        foreach (var scope in AiAgentScopeCatalog.AllScopes)
        {
            foreach (var compact in new[] { false, true })
            {
                var persona = AiAgentScopeCatalog.GetPersonaPromptSection(scope, compact);
                Assert.False(persona.Contains('_'), $"Scope {scope} (compact={compact}) : persona contient un underscore");
            }
        }
    }
}
