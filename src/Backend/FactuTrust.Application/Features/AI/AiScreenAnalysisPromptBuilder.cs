using System.Text;
using FactuTrust.Application.Configuration;

namespace FactuTrust.Application.Features.AI;

/// <summary>Builds screen-analysis-specific system prompt sections.</summary>
public static class AiScreenAnalysisPromptBuilder
{
    /// <summary>
    /// Consigne de la passe de synthèse forcée en analyse d'écran (le modèle a fini ses rounds
    /// d'outils sans rédiger la prose) : rédiger MAINTENANT les sections, sans appeler d'outil.
    /// </summary>
    public const string ForcedSynthesisSection =
        "SYNTHÈSE FINALE (analyse d'écran) : le tableau de bord et les résultats d'outils sont déjà dans "
        + "l'historique. N'appelle aucun outil. Rédige MAINTENANT l'analyse complète en sections markdown "
        + "(## Synthèse exécutive, ## Indicateurs clés, ## Analyse détaillée, ## Anomalies et risques, "
        + "## Opportunités, ## Actions recommandées, ## Points à vérifier), en français, sans traduction, avec les montants "
        + "TND du snapshot.";

    private const string StandardOutputTemplate = """
        FORMAT DE RÉPONSE OBLIGATOIRE (markdown, en français) :
        ## Synthèse exécutive
        (3 à 5 lignes avec les chiffres clés du snapshot, période incluse)

        ## Indicateurs clés
        (liste à puces ou dashboard JSON via generate_dashboard_config — NE PAS utiliser de tableaux markdown pour les KPI)

        ## Analyse détaillée
        (interprétation métier structurée, cite les comptes/clients/produits du snapshot)

        ## Anomalies et risques
        Classer chaque point : **OK** | **Attention** | **Critique**

        ## Opportunités
        (leviers concrets tirés des données)

        ## Actions recommandées
        (priorisées 1..n, liées aux écrans FactuTrust : relances, achats, corrections, contrôles)

        ## Points à vérifier / limites des données
        (pagination, échantillon, N-1 manquant, etc.)
        """;

    public static string BuildScreenAnalysisSystemSection(string? screenId, ScreenAnalysisOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine("MODE ANALYSE ÉCRAN (prioritaire — cette requête uniquement) :");
        sb.AppendLine("- L'utilisateur a cliqué « Analyser avec l'assistant IA » depuis un écran métier.");
        sb.AppendLine("- Le JSON du snapshot est la source PRIMAIRE et AUTHORISÉE pour cette analyse.");
        sb.AppendLine("- Analyse directement ces données : ne demande PAS de copier/coller.");
        sb.AppendLine("- Utilise les montants du snapshot tels quels (TND, 3 décimales, dates JJ/MM/AAAA).");
        sb.AppendLine("- N'appelle des outils QUE pour compléter un comparatif absent ou vérifier un point explicite.");
        sb.AppendLine("- Ne fabrique aucun chiffre absent du snapshot ni des outils.");
        sb.AppendLine();

        if (options.EnhancedPromptsEnabled && !string.IsNullOrWhiteSpace(screenId))
        {
            sb.AppendLine(BuildFamilySpecificGuidance(screenId));
            sb.AppendLine();
        }

        var toolHints = AiScreenAnalysisToolHints.GetHintsForScreen(screenId);
        if (toolHints.Count > 0)
        {
            sb.AppendLine("OUTILS COMPLÉMENTAIRES (optionnels, max 1-2 si pertinent) :");
            foreach (var hint in toolHints)
                sb.AppendLine($"- {hint}");
            sb.AppendLine();
        }

        if (ShouldForceDashboard(screenId, options))
        {
            sb.AppendLine("TABLEAU DE BORD : appelle generate_dashboard_config UNE SEULE FOIS avec au moins 3 KPI cards");
            sb.AppendLine("pertinentes et une section table ou chart basée UNIQUEMENT sur le snapshot.");
            sb.AppendLine("Après cet appel unique, rédige OBLIGATOIREMENT l'analyse complète en sections markdown");
            sb.AppendLine("ci-dessous — ne termine JAMAIS ta réponse par le seul tableau de bord.");
            sb.AppendLine();
        }

        sb.AppendLine(StandardOutputTemplate);
        return sb.ToString();
    }

    public static bool IsEnhancedForScreen(string? screenId, ScreenAnalysisOptions options)
    {
        if (!options.Enabled)
            return false;

        if (string.IsNullOrWhiteSpace(screenId))
            return options.EnhancedPromptsEnabled;

        if (options.PerScreenOverrides.TryGetValue(screenId, out var o) && o.EnhancedPrompt.HasValue)
            return o.EnhancedPrompt.Value;

        return options.EnhancedPromptsEnabled;
    }

    public static bool ShouldForceDashboard(string? screenId, ScreenAnalysisOptions options)
    {
        if (string.IsNullOrWhiteSpace(screenId))
            return false;

        return options.ForceDashboardForScreens.Contains(screenId, StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildFamilySpecificGuidance(string screenId)
    {
        return screenId switch
        {
            "accounting-income-statement" or "accounting-balance-sheet" or "accounting-balance" or "accounting-vat-declaration" =>
                """
                FOCUS COMPTABILITÉ — ÉTATS FINANCIERS :
                - Calcule et commente les ratios clés (marge brute, marge nette, variation N/N-1).
                - Identifie les postes à plus forte variation vs N-1.
                - Signale les incohérences matérielles (charges > produits, soldes anormaux).
                - Mentionne la conformité NCT 01 / SCE tunisien lorsque pertinent.
                """,
            "accounting-ledger" or "accounting-journal" or "accounting-sub-journals" or "accounting-manual-entry" =>
                """
                FOCUS COMPTABILITÉ — MOUVEMENTS :
                - Vérifie l'équilibre débit/crédit et le solde final.
                - Repère écritures atypiques, montants isolés élevés, doublons potentiels.
                - Analyse la répartition par journal, compte et période.
                """,
            "cash-desk" or "accounting-aging" or "accounting-lettering" =>
                """
                FOCUS TRÉSORERIE :
                - Analyse flux entrées/sorties et solde net par mode de paiement.
                - Créances > 90 jours, concentration client, retards de paiement.
                - Lettrage : écritures non lettrées, écarts de rapprochement.
                """,
            "invoice-list" or "credit-note-list" or "dashboard" =>
                """
                FOCUS COMMERCIAL :
                - Chiffre d'affaires, impayés, concentration clients/produits.
                - Factures en retard, devis en attente, alertes stock/livraison du dashboard.
                """,
            "stock-simple" or "forecasting-replenishment" or "forecasting-revenue" or "forecasting-abc-xyz" or "forecasting-promotions" =>
                """
                FOCUS STOCK & PRÉVISIONS :
                - Ruptures, surstock, rotation, classification ABC/XYZ.
                - Recommandations réappro et promotions avec niveau de confiance.
                """,
            "treasury-cash-forecast" =>
                """
                FOCUS TRÉSORERIE PRÉVISIONNELLE :
                - Mois de tension, franchissement de seuil, rupture de trésorerie prévue.
                - Concentration d'échéances, poids des encaissements incertains, marge de manœuvre.
                - Les montants et les dates affichés font foi : ne jamais les recalculer ni les extrapoler.
                """,
            "accounting-closing" =>
                """
                FOCUS CLÔTURE :
                - État d'avancement, écritures de clôture manquantes, contrôles pré-clôture.
                """,
            "accounting-chart" =>
                """
                FOCUS PLAN COMPTABLE :
                - Couverture des comptes, comptes inutilisés, structure NCT 01 / SCE.
                """,
            _ => "Analyse approfondie des données du snapshot avec recommandations actionnables."
        };
    }
}
