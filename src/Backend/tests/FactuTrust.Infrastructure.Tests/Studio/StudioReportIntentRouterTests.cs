using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.Studio.Ai;
using FactuTrust.Application.Features.Studio.Common.SqlReport;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Le raccourci déterministe est ce qui rend la fonctionnalité indépendante du modèle. Ces tests
/// figent les deux exigences opposées : il DOIT s'armer sur les formulations réelles des
/// utilisateurs, et il ne DOIT PAS s'armer sur une demande de création de structure.
/// </summary>
public sealed class StudioReportIntentRouterTests
{
    // ---- Les formulations exactes qui échouaient à l'écran ----

    [Theory]
    [InlineData("créer un rapport détaillé de ventes d'articles")]
    [InlineData("créer un rapport détaillé de ventes de produits à partir de tables du système")]
    [InlineData("créer un rapport avancés pour le vente de produits")]
    public void The_failing_screen_prompts_now_arm_the_shortcut(string message)
    {
        var detection = StudioReportIntentRouter.TryInfer(message);

        Assert.NotNull(detection);
        Assert.Equal("ventes_par_produit", detection!.PresetKey);
        Assert.False(detection.Save);
    }

    [Fact]
    public void Sales_by_client_is_recognised()
    {
        var detection = StudioReportIntentRouter.TryInfer("rapport du chiffre d'affaires par client");
        Assert.Equal("ventes_par_client", detection!.PresetKey);
    }

    [Theory]
    [InlineData("analyse des achats par fournisseur", "achats_par_fournisseur")]
    [InlineData("rapport des mouvements de stock", "mouvements_de_stock")]
    [InlineData("statistiques des encaissements par mode de règlement", "encaissements_par_mode")]
    [InlineData("rapport des écritures par compte", "ecritures_par_compte")]
    [InlineData("analyse de la masse salariale par mois", "masse_salariale_par_mois")]
    [InlineData("rapport des remises accordées", "remises_accordees")]
    public void Each_domain_maps_to_its_preset(string message, string expected)
    {
        Assert.Equal(expected, StudioReportIntentRouter.TryInfer(message)?.PresetKey);
    }

    // ---- Veto : une demande de STRUCTURE ne doit jamais lancer un état ----

    [Theory]
    [InlineData("crée une table Rapports avec les champs titre et date")]
    [InlineData("Créer un système de gestion de congés avec plusieurs tables liées")]
    [InlineData("ajoute un champ Statut sur la table Ventes")]
    [InlineData("crée un formulaire de saisie des ventes")]
    public void A_structure_request_never_arms_the_shortcut(string message)
    {
        Assert.Null(StudioReportIntentRouter.TryInfer(message));
    }

    [Fact]
    public void A_message_without_analysis_intent_is_ignored()
    {
        Assert.Null(StudioReportIntentRouter.TryInfer("bonjour"));
        Assert.Null(StudioReportIntentRouter.TryInfer("liste mes produits"));
        Assert.Null(StudioReportIntentRouter.TryInfer(null));
        Assert.Null(StudioReportIntentRouter.TryInfer("   "));
    }

    [Fact]
    public void An_analysis_intent_without_a_known_domain_is_ignored()
    {
        // « rapport de licornes » : intention d'analyse, mais aucun domaine métier reconnaissable.
        // On préfère ne rien exécuter plutôt que d'exécuter le mauvais état.
        Assert.Null(StudioReportIntentRouter.TryInfer("fais-moi un rapport de licornes"));
    }

    // ---- Période ----

    [Theory]
    [InlineData("rapport des ventes par produit aujourd'hui", ReportingPeriodResolver.PresetToday)]
    [InlineData("rapport des ventes par produit ce mois", ReportingPeriodResolver.PresetCurrentMonth)]
    [InlineData("rapport des ventes par produit le mois dernier", ReportingPeriodResolver.PresetLastMonth)]
    [InlineData("rapport des ventes par produit ce trimestre", ReportingPeriodResolver.PresetCurrentQuarter)]
    [InlineData("rapport des ventes par produit du dernier trimestre", ReportingPeriodResolver.PresetLastCompletedQuarter)]
    [InlineData("rapport des ventes par produit cette année", ReportingPeriodResolver.PresetYearToDate)]
    [InlineData("rapport des ventes par produit sur les 30 derniers jours", ReportingPeriodResolver.PresetLast30Days)]
    public void The_period_is_extracted_from_plain_french(string message, string expected)
    {
        Assert.Equal(expected, StudioReportIntentRouter.TryInfer(message)?.PeriodPreset);
    }

    [Fact]
    public void No_period_expressed_leaves_the_default_to_the_caller()
    {
        Assert.Null(StudioReportIntentRouter.TryInfer("rapport des ventes par produit")?.PeriodPreset);
        // Le défaut est une constante publique, annoncée en clair à l'utilisateur.
        Assert.Equal(ReportingPeriodResolver.PresetYearToDate, StudioReportIntentRouter.DefaultPeriodPreset);
    }

    [Fact]
    public void Last_month_wins_over_current_month_despite_overlapping_wording()
    {
        // « mois dernier » contient une sous-chaîne proche de « ce mois » : l'ordre des tests compte.
        Assert.Equal(
            ReportingPeriodResolver.PresetLastMonth,
            StudioReportIntentRouter.TryInfer("rapport des ventes du mois dernier par produit")?.PeriodPreset);
    }

    // ---- Enregistrement ----

    [Theory]
    [InlineData("enregistre le rapport des ventes par produit")]
    [InlineData("sauvegarder l'analyse des ventes par client")]
    public void An_explicit_save_verb_switches_to_the_plan_path(string message)
    {
        Assert.True(StudioReportIntentRouter.TryInfer(message)!.Save);
    }

    [Fact]
    public void Creer_alone_does_not_mean_save()
    {
        // « créer un rapport » veut dire « montre-le moi », pas « enregistre-le » : c'est la
        // formulation de la capture d'écran, elle ne doit pas déclencher une création d'artefact.
        Assert.False(StudioReportIntentRouter.TryInfer("créer un rapport de ventes par produit")!.Save);
    }

    // ---- Cohérence avec le catalogue ----

    [Fact]
    public void Every_routed_preset_exists_in_the_catalogue()
    {
        // Garde contre une clé renommée d'un côté et pas de l'autre : le routeur ne doit jamais
        // pointer vers un préréglage inexistant.
        var messages = new[]
        {
            "rapport des ventes par produit", "rapport des ventes par client",
            "rapport des ventes par mois", "rapport des remises accordées",
            "analyse des achats par fournisseur", "rapport des achats par mois",
            "rapport des mouvements de stock", "analyse du stock par entrepôt",
            "rapport des encaissements par mois", "statistiques des encaissements par mode",
            "rapport des écritures par compte", "rapport des écritures par journal",
            "analyse de la masse salariale par mois", "rapport de l'effectif par contrat"
        };

        foreach (var message in messages)
        {
            var detection = StudioReportIntentRouter.TryInfer(message);
            if (detection is null) continue;
            Assert.True(
                SqlReportPresetCatalog.Find(detection.PresetKey) is not null,
                $"« {message} » route vers « {detection.PresetKey} », absent du catalogue.");
        }
    }

    // ---- Suggestions ----

    [Fact]
    public void Suggestions_are_offered_even_when_the_shortcut_stays_silent()
    {
        var suggestions = StudioReportIntentRouter.SuggestPresets("je veux voir mes ventes");
        Assert.NotEmpty(suggestions);
        Assert.All(suggestions, key => Assert.NotNull(SqlReportPresetCatalog.Find(key)));
    }

    [Fact]
    public void Suggestions_fall_back_to_the_most_common_reports_without_any_signal()
    {
        var suggestions = StudioReportIntentRouter.SuggestPresets("azerty");
        Assert.NotEmpty(suggestions);
        Assert.All(suggestions, key => Assert.NotNull(SqlReportPresetCatalog.Find(key)));
    }

    [Fact]
    public void Suggestions_are_capped()
    {
        Assert.True(StudioReportIntentRouter.SuggestPresets("rapport des ventes", max: 2).Count <= 2);
        Assert.Empty(StudioReportIntentRouter.SuggestPresets("rapport des ventes", max: 0));
    }

    // ---- LooksLikeReportRequest (utilisé par le message d'échec) ----

    [Fact]
    public void LooksLikeReportRequest_separates_analysis_from_construction()
    {
        Assert.True(StudioReportIntentRouter.LooksLikeReportRequest("fais-moi un rapport de licornes"));
        Assert.False(StudioReportIntentRouter.LooksLikeReportRequest("crée une table avec les champs nom et date"));
        Assert.False(StudioReportIntentRouter.LooksLikeReportRequest(null));
    }
}
