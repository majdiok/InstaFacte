using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.Studio.Ai;
using FactuTrust.Application.Features.Studio.Common.SqlReport;
using FactuTrust.Domain.Enums;
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

    // ---- Bouclage : le JSON émis par le raccourci doit être celui qu'attend le parseur ----

    [Theory]
    [InlineData("créer un rapport détaillé de ventes d'articles")]
    [InlineData("rapport du chiffre d'affaires par client ce trimestre")]
    [InlineData("analyse des achats par fournisseur le mois dernier")]
    [InlineData("créer moi un rapport très détaillé de chiffres d'affaires par année")]
    public void The_shortcut_payload_round_trips_through_the_spec_parser(string message)
    {
        // Reproduit ce que SendChatMessageHandler sérialise pour studio_run_report (le branchement
        // réel est couvert par SendChatMessageHandlerStudioReportShortcutTests). Une divergence ici
        // casserait le raccourci en silence, sans qu'aucun autre test ne le voie.
        var detection = StudioReportIntentRouter.TryInfer(message);
        Assert.NotNull(detection);

        var period = ReportingPeriodResolver.Resolve(
            StudioReportIntentRouter.ResolvePeriodPreset(detection!),
            TimeProvider.System);
        var preset = SqlReportPresetCatalog.Find(detection.PresetKey);
        Assert.NotNull(preset);

        var specJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            title = preset!.DisplayName,
            preset = detection.PresetKey,
            from = period.FromDate.ToString("yyyy-MM-dd"),
            to = period.ToDate.ToString("yyyy-MM-dd")
        });

        Assert.True(StudioAiReportSpec.TryParse(specJson, out var parsed, out var error), error);
        Assert.Equal(detection.PresetKey, parsed!.PresetKey);
        Assert.Equal(period.FromDate, parsed.From);
        Assert.Equal(period.ToDate, parsed.To);
        Assert.Empty(parsed.Warnings);

        // Et la matérialisation retombe bien sur la table de faits du préréglage.
        var (factTable, definition) = StudioAiReportSpec.Materialize(parsed);
        Assert.Equal(preset.FactTable, factTable);
        Assert.Contains(definition.Filters, f => f.Op == "between");
    }

    // ---- La phrase exacte de la capture d'écran, et ses variantes sûres ----

    /// <summary>
    /// Mot pour mot, la demande qui produisait « Indiquez soit preset, soit source » : le pluriel
    /// « chiffres d'affaires » n'était couvert par aucun mot-clé du domaine Ventes.
    /// </summary>
    [Fact]
    public void The_exact_screen_prompt_routes_to_the_annual_sales_preset()
    {
        var detection = StudioReportIntentRouter.TryInfer(
            "créer moi un rapport très détaillé de chiffres d'affaires par année");

        Assert.NotNull(detection);
        Assert.Equal("ventes_par_annee", detection!.PresetKey);
        // « créer » veut dire « montre-le moi » : aucun artefact ne doit être créé.
        Assert.False(detection.Save);
        // Aucune période n'est exprimée : c'est le préréglage annuel qui décide (voir ci-dessous).
        Assert.Null(detection.PeriodPreset);
    }

    [Theory]
    // Singulier / pluriel des deux mots.
    [InlineData("créer moi un rapport très détaillé de chiffre d'affaires par année")]
    [InlineData("créer moi un rapport très détaillé de chiffre d'affaire par année")]
    [InlineData("créer moi un rapport très détaillé de chiffres d'affaire par année")]
    // Apostrophe typographique (clavier mobile, traitement de texte), apostrophe absente.
    [InlineData("créer moi un rapport très détaillé de chiffres d\u2019affaires par année")]
    [InlineData("créer moi un rapport très détaillé de chiffres d\u2018affaires par année")]
    [InlineData("créer moi un rapport très détaillé de chiffres d affaires par année")]
    // Casse et accents.
    [InlineData("CRÉER MOI UN RAPPORT TRÈS DÉTAILLÉ DE CHIFFRES D'AFFAIRES PAR ANNÉE")]
    [InlineData("creer moi un rapport tres detaille de chiffres d'affaires par annee")]
    // Faute de frappe déjà tolérée par le routeur.
    [InlineData("créer moi un rapport très détaillé de chiffres d'affaires par annnée")]
    // Espaces insécables / fines (copier-coller) et espaces multiples, retour à la ligne.
    [InlineData("créer moi un rapport très détaillé de chiffres\u00A0d'affaires par\u00A0année")]
    [InlineData("créer moi un rapport très détaillé de chiffres\u202Fd'affaires par année")]
    [InlineData("créer  moi un rapport   très détaillé de chiffres d'affaires par   année")]
    [InlineData("créer moi un rapport très détaillé\nde chiffres d'affaires par année")]
    // Autres tournures naturelles.
    [InlineData("rapport du CA par année")]
    [InlineData("chiffres d'affaires par an")]
    [InlineData("rapport des chiffres d'affaires annuels")]
    public void Safe_variants_of_the_screen_prompt_route_identically(string message)
    {
        var detection = StudioReportIntentRouter.TryInfer(message);

        Assert.NotNull(detection);
        Assert.Equal("ventes_par_annee", detection!.PresetKey);
        Assert.False(detection.Save);
        Assert.Null(detection.PeriodPreset);
    }

    [Theory]
    [InlineData("crée une table Chiffres d'affaires avec les champs année et montant")]
    [InlineData("crée un formulaire de saisie des chiffres d'affaires par année")]
    [InlineData("ajoute un champ chiffres d'affaires sur la table Ventes par année")]
    [InlineData("nouvelle table chiffres d'affaires par année")]
    public void The_plural_wording_does_not_weaken_the_structure_veto(string message)
    {
        Assert.Null(StudioReportIntentRouter.TryInfer(message));
        Assert.False(StudioReportIntentRouter.LooksLikeReportRequest(message));
    }

    [Fact]
    public void The_plural_wording_with_a_period_but_no_axis_still_asks_for_a_breakdown()
    {
        // « de l'année en cours » est une PÉRIODE, pas une ventilation : même règle qu'au singulier.
        var message = "créer moi un rapport très détaillé de chiffres d'affaires de l'année en cours";

        Assert.Null(StudioReportIntentRouter.TryInfer(message));
        Assert.True(StudioReportIntentRouter.LooksLikeReportRequest(message));

        // Le domaine est reconnu : les ventilations proposées sont celles des ventes, pas un repli
        // générique.
        var suggestions = StudioReportIntentRouter.SuggestForDomain(message);
        Assert.NotEmpty(suggestions);
        Assert.All(suggestions, key => Assert.Equal(ReportDomain.Ventes, SqlReportPresetCatalog.Find(key)!.Domain));
    }

    [Fact]
    public void The_plural_wording_routes_like_the_singular_on_every_axis()
    {
        foreach (var axis in new[] { "par produit", "par client", "par mois", "par année" })
        {
            var singular = StudioReportIntentRouter.TryInfer($"rapport de chiffre d'affaires {axis}");
            var plural = StudioReportIntentRouter.TryInfer($"rapport de chiffres d'affaires {axis}");

            Assert.NotNull(singular);
            Assert.NotNull(plural);
            Assert.Equal(singular!.PresetKey, plural!.PresetKey);
        }
    }

    // ---- Priorité de période : explicite > préréglage > défaut global ----

    [Fact]
    public void An_annual_preset_without_a_period_uses_its_own_multi_year_default()
    {
        var detection = StudioReportIntentRouter.TryInfer(
            "créer moi un rapport très détaillé de chiffres d'affaires par année");

        Assert.NotNull(detection);
        Assert.Null(detection!.PeriodPreset);
        // Pas l'année en cours : un regroupement PAR ANNÉE sur l'année en cours ne rendrait qu'une ligne.
        Assert.Equal(ReportingPeriodResolver.PresetLastFiveYears, StudioReportIntentRouter.ResolvePeriodPreset(detection));
        Assert.Equal(SqlReportPresetCatalog.Find("ventes_par_annee")!.DefaultPeriodPreset,
            StudioReportIntentRouter.ResolvePeriodPreset(detection));
    }

    [Theory]
    [InlineData("rapport des achats par année", "achats_par_annee")]
    [InlineData("rapport des encaissements par année", "encaissements_par_annee")]
    public void Every_annual_preset_resolves_to_its_declared_default_period(string message, string expectedPreset)
    {
        var detection = StudioReportIntentRouter.TryInfer(message);

        Assert.NotNull(detection);
        Assert.Equal(expectedPreset, detection!.PresetKey);
        Assert.Equal(
            SqlReportPresetCatalog.Find(expectedPreset)!.DefaultPeriodPreset,
            StudioReportIntentRouter.ResolvePeriodPreset(detection));
        Assert.NotEqual(ReportingPeriodResolver.PresetYearToDate, StudioReportIntentRouter.ResolvePeriodPreset(detection));
    }

    [Fact]
    public void An_explicit_period_wins_over_the_preset_default()
    {
        // L'utilisateur a demandé « cette année » : il l'obtient, même si l'état est annuel.
        var detection = StudioReportIntentRouter.TryInfer("rapport des ventes par année cette année");

        Assert.NotNull(detection);
        Assert.Equal("ventes_par_annee", detection!.PresetKey);
        Assert.Equal(ReportingPeriodResolver.PresetYearToDate, detection.PeriodPreset);
        Assert.Equal(ReportingPeriodResolver.PresetYearToDate, StudioReportIntentRouter.ResolvePeriodPreset(detection));
    }

    [Fact]
    public void A_preset_without_its_own_default_keeps_the_global_default()
    {
        // Comportement historique préservé pour les états non annuels.
        var detection = StudioReportIntentRouter.TryInfer("rapport des ventes par produit");

        Assert.NotNull(detection);
        Assert.Null(SqlReportPresetCatalog.Find(detection!.PresetKey)!.DefaultPeriodPreset);
        Assert.Equal(StudioReportIntentRouter.DefaultPeriodPreset, StudioReportIntentRouter.ResolvePeriodPreset(detection));
        Assert.Equal(ReportingPeriodResolver.PresetYearToDate, StudioReportIntentRouter.ResolvePeriodPreset(detection));
    }

    [Fact]
    public void ResolvePeriodPreset_always_yields_a_preset_the_resolver_accepts()
    {
        // Garde : un DefaultPeriodPreset mal orthographié dans le catalogue ferait lever
        // ReportingPeriodResolver.Resolve au moment de servir l'utilisateur.
        foreach (var preset in SqlReportPresetCatalog.All)
        {
            var resolved = StudioReportIntentRouter.ResolvePeriodPreset(new StudioReportDetection(preset.Key, null, false));
            Assert.Contains(resolved, ReportingPeriodResolver.ValidPresets);
        }

        // Clé inconnue : pas d'exception, défaut global.
        Assert.Equal(
            StudioReportIntentRouter.DefaultPeriodPreset,
            StudioReportIntentRouter.ResolvePeriodPreset(new StudioReportDetection("inexistant", null, false)));
    }

    [Fact]
    public void The_annual_shortcut_payload_spans_the_current_year_and_the_four_before()
    {
        // Horloge figée : la fenêtre ne dépend pas du jour d'exécution du test.
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 6, 10, 0, 0, TimeSpan.Zero));
        var detection = StudioReportIntentRouter.TryInfer(
            "créer moi un rapport très détaillé de chiffres d'affaires par année");

        var period = ReportingPeriodResolver.Resolve(StudioReportIntentRouter.ResolvePeriodPreset(detection!), clock);

        // 1er janvier de N−4 → aujourd'hui (année N partielle), pas cinq années pleines.
        Assert.Equal(new DateOnly(2022, 1, 1), period.FromDate);
        Assert.Equal(new DateOnly(2026, 9, 6), period.ToDate);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    // ---- Normalisation ----

    [Theory]
    [InlineData("chiffres d\u2019affaires", "chiffres d'affaires")]
    [InlineData("chiffres d\u2018affaires", "chiffres d'affaires")]
    [InlineData("chiffres d\u02BCaffaires", "chiffres d'affaires")]
    [InlineData("chiffres d\u00B4affaires", "chiffres d'affaires")]
    [InlineData("aujourd\u2019hui", "aujourd'hui")]
    [InlineData("par\u00A0année", "par annee")]
    [InlineData("par\u202Fannée", "par annee")]
    [InlineData("par\tannée", "par annee")]
    [InlineData("  par    année  ", "par annee")]
    [InlineData("Créé", "cree")]
    [InlineData("annnée", "annee")]
    public void Normalize_folds_apostrophes_whitespace_case_and_accents(string input, string expected)
    {
        Assert.Equal(expected, StudioReportIntentRouter.Normalize(input));
    }

    [Fact]
    public void A_typographic_apostrophe_no_longer_hides_the_period()
    {
        Assert.Equal(
            ReportingPeriodResolver.PresetToday,
            StudioReportIntentRouter.TryInfer("rapport des ventes par produit aujourd\u2019hui")?.PeriodPreset);
    }

    // ---- Demandes annuelles et pondération ----

    [Theory]
    [InlineData("créer moi un rapport détaillé de chiffre d'affaires par année", "ventes_par_annee")]
    [InlineData("rapport détaillé de chiffre d'affaires par annnée", "ventes_par_annee")]
    [InlineData("rapport du chiffre d'affaires annuel", "ventes_par_annee")]
    [InlineData("rapport des achats par an", "achats_par_annee")]
    [InlineData("rapport des encaissements par année", "encaissements_par_annee")]
    public void An_annual_request_routes_to_the_annual_preset(string message, string expected)
    {
        var detection = StudioReportIntentRouter.TryInfer(message);
        Assert.NotNull(detection);
        Assert.Equal(expected, detection!.PresetKey);
    }

    [Theory]
    [InlineData("rapport de chiffre d'affaires par mois", "ventes_par_mois")]
    [InlineData("rapport des ventes par produit", "ventes_par_produit")]
    [InlineData("rapport des achats par fournisseur", "achats_par_fournisseur")]
    public void Existing_routings_are_unchanged_by_the_new_weighting(string message, string expected)
    {
        var detection = StudioReportIntentRouter.TryInfer(message);
        Assert.NotNull(detection);
        Assert.Equal(expected, detection!.PresetKey);
    }

    [Fact]
    public void A_period_expressed_as_a_year_is_not_mistaken_for_an_annual_breakdown()
    {
        // « de l'année en cours » demande une PÉRIODE, pas une ventilation par année : router vers
        // un regroupement annuel rendrait un état d'UNE seule ligne.
        var detection = StudioReportIntentRouter.TryInfer(
            "créer moi un rapport détaillé de chiffre d'affaires de l'année en cours");

        Assert.Null(detection);
    }

    [Fact]
    public void A_plural_keyword_no_longer_counts_twice()
    {
        // « ventes » déclenchait « vente » ET « ventes » : le domaine valait le double de
        // « chiffre d'affaires ». Les deux formulations doivent désormais router à l'identique.
        var plural = StudioReportIntentRouter.TryInfer("rapport des ventes par mois");
        var revenue = StudioReportIntentRouter.TryInfer("rapport de chiffre d'affaires par mois");

        Assert.NotNull(plural);
        Assert.NotNull(revenue);
        Assert.Equal(plural!.PresetKey, revenue!.PresetKey);
    }

    // ---- Domaine reconnu, axe absent ----

    [Fact]
    public void A_domain_without_an_axis_suggests_that_domain_only()
    {
        var message = "créer moi un rapport détaillé de chiffre d'affaires de l'année en cours";

        Assert.Null(StudioReportIntentRouter.TryInfer(message));

        var suggestions = StudioReportIntentRouter.SuggestForDomain(message);
        Assert.NotEmpty(suggestions);
        Assert.All(suggestions, key =>
        {
            var preset = SqlReportPresetCatalog.Find(key);
            Assert.NotNull(preset);
            Assert.Equal(ReportDomain.Ventes, preset!.Domain);
        });
    }

    [Fact]
    public void An_unrelated_question_suggests_nothing_at_all()
    {
        // Contrairement à SuggestPresets, aucun repli générique : proposer des états de vente sur
        // une question hors sujet serait du bruit.
        Assert.Empty(StudioReportIntentRouter.SuggestForDomain("quelle est la météo à Tunis"));
        Assert.Empty(StudioReportIntentRouter.SuggestForDomain(null));
    }

    [Fact]
    public void Prompt_suggestions_are_sentences_a_user_can_send_back()
    {
        var prompts = StudioReportIntentRouter.ToPromptSuggestions(new[] { "ventes_par_produit", "ventes_par_annee" });

        Assert.Equal(2, prompts.Count);
        Assert.All(prompts, p => Assert.False(string.IsNullOrWhiteSpace(p)));
        // Un état annuel porte sa propre période : ne pas lui accoler « ce mois ».
        Assert.DoesNotContain(prompts, p => p.Contains("Ventes par année ce mois", StringComparison.Ordinal));
    }

    [Fact]
    public void LooksLikeReportRequest_separates_analysis_from_construction()
    {
        Assert.True(StudioReportIntentRouter.LooksLikeReportRequest("fais-moi un rapport de licornes"));
        Assert.False(StudioReportIntentRouter.LooksLikeReportRequest("crée une table avec les champs nom et date"));
        Assert.False(StudioReportIntentRouter.LooksLikeReportRequest(null));
    }
}
