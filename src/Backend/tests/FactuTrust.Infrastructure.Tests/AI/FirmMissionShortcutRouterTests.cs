using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.Tools;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Lot 1.2 — complétude sémantique du raccourci déterministe FirmMission. Pour chacune des 19
/// questions suggérées de l'accueil Chef de mission (<c>agent-scopes.config.ts</c>), on vérifie les
/// OUTILS attendus ET leurs ARGUMENTS (table 1.2 du plan v3) — pas seulement « ≥ 1 outil ». Un
/// routage naïf par mot-clé serait sémantiquement faux (ex. « pour quel montant, chez combien de
/// clients ? » exige les agrégats de l'overview, pas la liste plafonnée à top_n).
/// </summary>
public sealed class FirmMissionShortcutRouterTests
{
    // ── overview : agrégats du portefeuille ──────────────────────────────────────────────────────

    [Fact]
    public void Portfolio_status_resolves_to_overview_without_args()
        => AssertSingleOverview("Où en est le portefeuille du cabinet aujourd'hui ?");

    [Fact]
    public void Overdue_amount_and_clients_count_resolves_to_overview_not_deadline_list()
    {
        // « pour quel montant, chez combien de clients ? » ⇒ agrégats (echeancesEnRetard, montantEnRetard,
        // dossiersAvecRetard), PAS la liste plafonnée.
        var calls = FirmMissionShortcutRouter.Resolve(
            "Combien d'échéances sont en retard, pour quel montant, et chez combien de clients ?");
        AssertOverviewOnly(calls);
    }

    [Fact]
    public void Dossiers_with_upcoming_within_7_days_count_resolves_to_overview()
        => AssertSingleOverview("Combien de dossiers ont une échéance dans les 7 prochains jours ?");

    [Fact]
    public void Vat_drafts_count_resolves_to_overview()
        => AssertSingleOverview("Combien de déclarations TVA sont encore en brouillon ?");

    // ── deadlines : listes d'échéances filtrées ──────────────────────────────────────────────────

    [Fact]
    public void Overdue_list_resolves_to_deadlines_only_overdue_true()
    {
        var calls = FirmMissionShortcutRouter.Resolve(
            "Quelles échéances sont en retard et chez quels clients ?");
        var (tool, args) = Assert.Single(calls);
        Assert.Equal(FirmAgentTools.FiscalDeadlines, tool);
        Assert.True((bool)args["only_overdue"]!);
        Assert.DoesNotContain("within_days", args.Keys);
    }

    [Fact]
    public void This_week_resolves_to_deadlines_within_days_7()
        => AssertDeadlines(FirmMissionShortcutRouter.Resolve("Quelles échéances tombent cette semaine ?"), withinDays: 7);

    [Fact]
    public void Next_30_days_resolves_to_deadlines_within_days_30()
        => AssertDeadlines(FirmMissionShortcutRouter.Resolve("Quelles échéances arrivent dans les 30 prochains jours ?"), withinDays: 30);

    [Fact]
    public void Quarterly_vat_within_15_days_resolves_to_deadlines_with_obligation_type()
    {
        var calls = FirmMissionShortcutRouter.Resolve(
            "Quelles obligations de TVA trimestrielle arrivent dans les 15 prochains jours ?");
        var (tool, args) = Assert.Single(calls);
        Assert.Equal(FirmAgentTools.FiscalDeadlines, tool);
        Assert.Equal(15, (int)args["within_days"]!);
        Assert.Equal("QuarterlyVat", (string)args["obligation_type"]!);
    }

    // ── risk / dossier health ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Quels dossiers sont les plus à risque cette semaine ?")]
    [InlineData("Quels dossiers n'ont pas eu d'écriture depuis un mois ?")]
    [InlineData("Quels dossiers ont des déclarations TVA en brouillon ?")]
    [InlineData("Classe les dossiers par niveau de risque et explique pourquoi.")]
    public void Risk_questions_resolve_to_dossier_health(string question)
    {
        var calls = FirmMissionShortcutRouter.Resolve(question);
        var (tool, args) = Assert.Single(calls);
        Assert.Equal(FirmAgentTools.DossierHealth, tool);
        Assert.Empty(args);
    }

    // ── workload : charge des collaborateurs ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("Comment se répartit la charge entre mes collaborateurs ?")]
    [InlineData("Qui est le plus chargé en échéances en retard ?")]
    [InlineData("Combien d'échéances n'ont pas de responsable désigné ?")]
    [InlineData("Qui suit le plus de dossiers actuellement ?")]
    public void Workload_questions_resolve_to_collaborator_workload(string question)
    {
        var calls = FirmMissionShortcutRouter.Resolve(question);
        var (tool, args) = Assert.Single(calls);
        Assert.Equal(FirmAgentTools.CollaboratorWorkload, tool);
        Assert.Empty(args);
    }

    // ── review : combinaisons de 2 outils ─────────────────────────────────────────────────────────

    [Fact]
    public void Weekly_review_resolves_to_overview_then_dossier_health()
        => AssertOverviewThenDossierHealth("Prépare-moi la revue hebdomadaire du cabinet.");

    [Fact]
    public void Priority_actions_resolves_to_overview_then_dossier_health()
        => AssertOverviewThenDossierHealth("Quelles sont les 3 actions prioritaires cette semaine ?");

    [Fact]
    public void Inactive_or_unassigned_resolves_to_dossier_health_then_workload()
    {
        var calls = FirmMissionShortcutRouter.Resolve(
            "Y a-t-il des dossiers inactifs ou des échéances sans responsable à traiter ?");
        Assert.Equal(2, calls.Count);
        Assert.Equal(FirmAgentTools.DossierHealth, calls[0].ToolName);
        Assert.Equal(FirmAgentTools.CollaboratorWorkload, calls[1].ToolName);
    }

    // ── cas vides / ambigus : jamais plus de 2 outils, liste vide hors domaine ───────────────────

    [Fact]
    public void Empty_or_null_message_returns_no_calls()
    {
        Assert.Empty(FirmMissionShortcutRouter.Resolve(null));
        Assert.Empty(FirmMissionShortcutRouter.Resolve(""));
        Assert.Empty(FirmMissionShortcutRouter.Resolve("   "));
    }

    [Fact]
    public void Out_of_domain_message_returns_no_calls()
        => Assert.Empty(FirmMissionShortcutRouter.Resolve("Merci, c'est très clair."));

    [Fact]
    public void No_question_resolves_to_more_than_two_tools()
    {
        // Toutes les questions suggérées + quelques variantes : le routeur ne dépasse jamais 2 outils.
        var samples = new[]
        {
            "Où en est le portefeuille du cabinet aujourd'hui ?",
            "Combien d'échéances sont en retard, pour quel montant, et chez combien de clients ?",
            "Combien de dossiers ont une échéance dans les 7 prochains jours ?",
            "Combien de déclarations TVA sont encore en brouillon ?",
            "Quelles échéances sont en retard et chez quels clients ?",
            "Quelles échéances tombent cette semaine ?",
            "Quelles échéances arrivent dans les 30 prochains jours ?",
            "Quelles obligations de TVA trimestrielle arrivent dans les 15 prochains jours ?",
            "Quels dossiers sont les plus à risque cette semaine ?",
            "Quels dossiers n'ont pas eu d'écriture depuis un mois ?",
            "Quels dossiers ont des déclarations TVA en brouillon ?",
            "Classe les dossiers par niveau de risque et explique pourquoi.",
            "Comment se répartit la charge entre mes collaborateurs ?",
            "Qui est le plus chargé en échéances en retard ?",
            "Combien d'échéances n'ont pas de responsable désigné ?",
            "Qui suit le plus de dossiers actuellement ?",
            "Prépare-moi la revue hebdomadaire du cabinet.",
            "Quelles sont les 3 actions prioritaires cette semaine ?",
            "Y a-t-il des dossiers inactifs ou des échéances sans responsable à traiter ?"
        };
        foreach (var sample in samples)
        {
            var calls = FirmMissionShortcutRouter.Resolve(sample);
            Assert.True(calls.Count <= 2, $"'{sample}' resolved to {calls.Count} tools (max 2).");
        }
    }

    // ── Normalisation : diacritiques et casse ne changent pas le routage ──────────────────────────

    [Fact]
    public void Diacritics_and_case_do_not_change_routing()
    {
        AssertSingleOverview("OU EN EST LE PORTEFEUILLE DU CABINET AUJOURD'HUI ?");
        AssertSingleOverview("Où en est le portefeuille du cabinet aujourd'hui ?");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────────────────

    private static void AssertSingleOverview(string message)
    {
        var calls = FirmMissionShortcutRouter.Resolve(message);
        var (tool, args) = Assert.Single(calls);
        Assert.Equal(FirmAgentTools.PortfolioOverview, tool);
        Assert.Empty(args);
    }

    private static void AssertOverviewOnly(System.Collections.Generic.IReadOnlyList<FirmMissionShortcutRouter.PlannedToolCall> calls)
    {
        var (tool, args) = Assert.Single(calls);
        Assert.Equal(FirmAgentTools.PortfolioOverview, tool);
        Assert.Empty(args);
    }

    private static void AssertDeadlines(
        System.Collections.Generic.IReadOnlyList<FirmMissionShortcutRouter.PlannedToolCall> calls,
        int withinDays)
    {
        var (tool, args) = Assert.Single(calls);
        Assert.Equal(FirmAgentTools.FiscalDeadlines, tool);
        Assert.Equal(withinDays, (int)args["within_days"]!);
        Assert.DoesNotContain("only_overdue", args.Keys);
        Assert.DoesNotContain("obligation_type", args.Keys);
    }

    private static void AssertOverviewThenDossierHealth(string message)
    {
        var calls = FirmMissionShortcutRouter.Resolve(message);
        Assert.Equal(2, calls.Count);
        Assert.Equal(FirmAgentTools.PortfolioOverview, calls[0].ToolName);
        Assert.Empty(calls[0].Arguments);
        Assert.Equal(FirmAgentTools.DossierHealth, calls[1].ToolName);
        Assert.Empty(calls[1].Arguments);
    }

}

/// <summary>
/// Helper d'extension pour déconstruire un <see cref="FirmMissionShortcutRouter.PlannedToolCall"/>
/// en (toolName, arguments) dans les assertions de tests.
/// </summary>
file static class PlannedToolCallDeconstruction
{
    public static void Deconstruct(
        this FirmMissionShortcutRouter.PlannedToolCall call,
        out string toolName,
        out System.Collections.Generic.Dictionary<string, object?> arguments)
    {
        toolName = call.ToolName;
        arguments = call.Arguments;
    }
}
