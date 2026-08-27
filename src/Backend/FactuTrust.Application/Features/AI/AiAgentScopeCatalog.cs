using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Constants;

namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Source de vérité des assistants experts par module (<see cref="AssistantAgentScope"/>) :
/// sous-ensembles d'outils (complet + variante CPU), persona de prompt système et nom d'affichage.
/// Le scope ne s'applique qu'en mode Default ; None = assistant global (aucun filtrage, comportement historique).
/// Garde anti-dérive : AiAgentScopeCatalogTests vérifie que chaque nom existe dans AiToolRegistry.All.
/// </summary>
public static class AiAgentScopeCatalog
{
    /// <summary>Noyau commun à tous les experts (période, calendrier, dashboard, prompts de suivi).</summary>
    private static readonly string[] CoreToolNames =
    {
        "resolve_reporting_period",
        "propose_follow_up_prompts",
        "generate_dashboard_config",
        "get_tunisian_commercial_calendar"
    };

    private static HashSet<string> BuildSet(params string[] names)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in CoreToolNames)
            set.Add(name);
        foreach (var name in names)
            set.Add(name);
        return set;
    }

    // ── Sous-ensembles complets (lecture + mutation ; les outils IsMutating restent gérés par
    //    EnableMutationTools dans GetDefinitionsForMode, exactement comme pour le catalogue global) ──

    private static readonly HashSet<string> SalesToolNames = BuildSet(
        "get_sales_revenue",
        "get_client_payments",
        "get_client_balances",
        "get_commercial_profit",
        "get_client_aging",
        "get_product_performance",
        "get_basket_metrics",
        "get_product_sales_trend",
        "propose_client_actions",
        "search_invoices",
        "get_invoice_by_id",
        "search_quotes",
        "get_quote_by_id",
        "search_delivery_notes",
        "get_delivery_note_by_id",
        "search_clients",
        "get_client_by_id",
        // Mutations du domaine ventes (exposées uniquement si EnableMutationTools)
        "record_invoice_payment",
        "validate_invoice",
        "sign_invoice",
        "send_invoice_email",
        "accept_quote",
        "reject_quote",
        "send_quote",
        "generate_invoice");

    private static readonly HashSet<string> PurchasesToolNames = BuildSet(
        "search_suppliers",
        "get_supplier_by_id",
        "get_supplier_balances",
        "search_purchase_orders",
        "get_purchase_order_by_id",
        "search_supplier_invoices",
        "get_supplier_invoice_by_id",
        "get_replenishment_recommendations",
        "forecast_product_demand",
        // Mutations
        "confirm_purchase_order",
        "cancel_purchase_order",
        "record_supplier_payment",
        "cancel_supplier_invoice",
        "create_supplier",
        "update_supplier",
        "prepare_purchase_order_from_replenishment");

    private static readonly HashSet<string> StockToolNames = BuildSet(
        "get_stock_snapshot",
        "get_stock_movements",
        "get_warehouses",
        "get_products_never_sold",
        "get_replenishment_recommendations",
        "get_abc_xyz_classification",
        "forecast_product_demand",
        "get_product_by_id",
        "get_product_categories",
        // Mutations
        "record_stock_entry",
        "record_stock_exit",
        "adjust_stock",
        "create_warehouse");

    private static readonly HashSet<string> AccountingToolNames = BuildSet(
        "get_accounting_dashboard",
        "compliance_check_invoice",
        "get_client_aging",
        "get_client_balances",
        "get_supplier_balances",
        "get_sales_revenue",
        "search_invoices",
        "get_invoice_by_id",
        "search_supplier_invoices",
        "get_supplier_invoice_by_id",
        "propose_client_actions");

    private static readonly HashSet<string> TreasuryToolNames = BuildSet(
        "get_client_payments",
        "get_client_balances",
        "get_client_aging",
        "get_supplier_balances",
        "get_sales_revenue",
        "search_invoices",
        "search_supplier_invoices",
        "propose_client_actions",
        // Trésorerie prévisionnelle — lecture seule.
        "get_cash_flow_forecast",
        "get_cash_flow_lines",
        // Mutations
        "record_invoice_payment",
        "record_supplier_payment",
        "create_cash_expense");

    private static readonly HashSet<string> CrmToolNames = BuildSet(
        "search_crm_activities",
        "search_crm_opportunities",
        "get_client_balances",
        "get_client_aging",
        "get_sales_revenue",
        "propose_client_actions",
        "search_clients",
        "get_client_by_id",
        // Mutations
        "create_crm_activity",
        "create_crm_opportunity",
        "create_client",
        "update_client");

    /// <summary>
    /// Seul scope au périmètre du CABINET : ses outils lisent plusieurs dossiers à la fois, via la
    /// base master et un fan-out borné par l'ACL, jamais via le contexte tenant courant.
    /// </summary>
    private static readonly HashSet<string> FirmMissionToolNames = BuildSet(
        "get_firm_portfolio_overview",
        "get_firm_fiscal_deadlines",
        "get_firm_dossier_health",
        "get_firm_collaborator_workload",
        // Seule mutation du scope (exposée uniquement si EnableMutationTools)
        "send_fiscal_deadline_reminder");

    // ── Variantes CPU (≤ 8 outils, lecture seule) : catalogue minimal pour le petit modèle sur CPU,
    //    même esprit que les Cpu*ToolNames de AiToolIntentRouter ──

    private static readonly HashSet<string> CpuSalesToolNames = new(StringComparer.Ordinal)
    {
        "resolve_reporting_period",
        "get_sales_revenue",
        "get_client_balances",
        "get_client_aging",
        "get_commercial_profit",
        "get_product_performance",
        "search_invoices",
        "propose_follow_up_prompts"
    };

    private static readonly HashSet<string> CpuPurchasesToolNames = new(StringComparer.Ordinal)
    {
        "resolve_reporting_period",
        "get_supplier_balances",
        "search_purchase_orders",
        "search_supplier_invoices",
        "get_replenishment_recommendations",
        "propose_follow_up_prompts"
    };

    private static readonly HashSet<string> CpuStockToolNames = new(StringComparer.Ordinal)
    {
        "resolve_reporting_period",
        "get_stock_snapshot",
        "get_stock_movements",
        "get_replenishment_recommendations",
        "get_products_never_sold",
        "get_abc_xyz_classification",
        "propose_follow_up_prompts"
    };

    private static readonly HashSet<string> CpuAccountingToolNames = new(StringComparer.Ordinal)
    {
        "resolve_reporting_period",
        "get_accounting_dashboard",
        "get_client_balances",
        "get_supplier_balances",
        "get_client_aging",
        "search_invoices",
        "compliance_check_invoice",
        "propose_follow_up_prompts"
    };

    private static readonly HashSet<string> CpuTreasuryToolNames = new(StringComparer.Ordinal)
    {
        "resolve_reporting_period",
        "get_client_payments",
        "get_client_balances",
        "get_client_aging",
        "get_supplier_balances",
        "search_supplier_invoices",
        "get_cash_flow_forecast",
        "propose_follow_up_prompts"
    };

    private static readonly HashSet<string> CpuCrmToolNames = new(StringComparer.Ordinal)
    {
        "resolve_reporting_period",
        "search_crm_activities",
        "search_crm_opportunities",
        "get_client_aging",
        "propose_client_actions",
        "propose_follow_up_prompts"
    };

    private static readonly HashSet<string> CpuFirmMissionToolNames = new(StringComparer.Ordinal)
    {
        "resolve_reporting_period",
        "get_firm_portfolio_overview",
        "get_firm_fiscal_deadlines",
        "get_firm_dossier_health",
        "get_firm_collaborator_workload",
        "propose_follow_up_prompts"
    };

    /// <summary>Tous les scopes « expert » (sans None), pour les tests et les itérations.</summary>
    public static readonly IReadOnlyList<AssistantAgentScope> AllScopes = new[]
    {
        AssistantAgentScope.Sales,
        AssistantAgentScope.Purchases,
        AssistantAgentScope.Stock,
        AssistantAgentScope.Accounting,
        AssistantAgentScope.Treasury,
        AssistantAgentScope.Crm,
        AssistantAgentScope.FirmMission
    };

    /// <summary>Sous-ensemble complet d'outils du scope (vide pour None = aucun filtrage).</summary>
    public static IReadOnlySet<string> GetToolNames(AssistantAgentScope scope) => scope switch
    {
        AssistantAgentScope.Sales => SalesToolNames,
        AssistantAgentScope.Purchases => PurchasesToolNames,
        AssistantAgentScope.Stock => StockToolNames,
        AssistantAgentScope.Accounting => AccountingToolNames,
        AssistantAgentScope.Treasury => TreasuryToolNames,
        AssistantAgentScope.Crm => CrmToolNames,
        AssistantAgentScope.FirmMission => FirmMissionToolNames,
        _ => EmptySet
    };

    /// <summary>Variante CPU (catalogue minimal lecture seule) du scope.</summary>
    public static IReadOnlySet<string> GetCpuToolNames(AssistantAgentScope scope) => scope switch
    {
        AssistantAgentScope.Sales => CpuSalesToolNames,
        AssistantAgentScope.Purchases => CpuPurchasesToolNames,
        AssistantAgentScope.Stock => CpuStockToolNames,
        AssistantAgentScope.Accounting => CpuAccountingToolNames,
        AssistantAgentScope.Treasury => CpuTreasuryToolNames,
        AssistantAgentScope.Crm => CpuCrmToolNames,
        AssistantAgentScope.FirmMission => CpuFirmMissionToolNames,
        _ => EmptySet
    };

    private static readonly HashSet<string> EmptySet = new(StringComparer.Ordinal);

    /// <summary>Nom d'affichage FR de l'expert (pour logs et UI).</summary>
    public static string GetDisplayName(AssistantAgentScope scope) => scope switch
    {
        AssistantAgentScope.Sales => "Expert Ventes",
        AssistantAgentScope.Purchases => "Expert Achats",
        AssistantAgentScope.Stock => "Expert Stock",
        AssistantAgentScope.Accounting => "Expert Comptabilité",
        AssistantAgentScope.Treasury => "Expert Trésorerie",
        AssistantAgentScope.Crm => "Expert CRM",
        AssistantAgentScope.FirmMission => "Chef de mission",
        _ => "Assistant global"
    };

    /// <summary>
    /// Section persona appendée APRÈS le prompt système de base (jamais insérée dedans) :
    /// identité d'expert + périmètre + règle hors-scope (répondre + orienter, ne jamais deviner).
    /// Courte volontairement (petit modèle 3b sur CPU). La variante compacte est encore plus brève.
    /// </summary>
    public static string GetPersonaPromptSection(AssistantAgentScope scope, bool compact)
    {
        // Le Chef de mission a sa propre rédaction : la formule générique de repli oriente vers
        // « l'Assistant IA global », qui n'existe pas en mode cabinet natif. L'orientation correcte
        // est d'ouvrir le dossier concerné.
        if (scope == AssistantAgentScope.FirmMission)
            return BuildFirmMissionPersona(compact);

        var (identity, domain, posture, outOfScope) = scope switch
        {
            AssistantAgentScope.Sales => (
                "Expert Ventes : tu agis comme un directeur commercial expérimenté.",
                "chiffre d'affaires, clients, marges, paniers, devis, bons de livraison et factures de vente",
                "Analyse, classe, compare et conseille avec un regard de directeur commercial (tendances, relances, opportunités de vente).",
                "achats, stock, comptabilité, trésorerie"),
            AssistantAgentScope.Purchases => (
                "Expert Achats : tu agis comme un directeur des achats expérimenté.",
                "fournisseurs, bons de commande, factures fournisseurs, réapprovisionnement et coûts d'achat",
                "Analyse les dépenses fournisseurs, suis les commandes et recommande des réapprovisionnements comme un directeur des achats.",
                "ventes, comptabilité, trésorerie, CRM"),
            AssistantAgentScope.Stock => (
                "Expert Stock : tu agis comme un responsable logistique expérimenté.",
                "niveaux de stock, mouvements, entrepôts, ruptures, rotation, réapprovisionnement et classification ABC/XYZ",
                "Surveille les niveaux, anticipe les ruptures et optimise la rotation comme un responsable logistique.",
                "ventes, achats, comptabilité, trésorerie"),
            AssistantAgentScope.Accounting => (
                "Expert Comptabilité : tu agis comme un expert-comptable expérimenté.",
                "tableau de bord comptable, soldes clients et fournisseurs, créances âgées, conformité des factures",
                "Contrôle, rapproche et explique les chiffres avec la rigueur d'un expert-comptable. Rappel : le chiffre d'affaires canonique ne compte que les factures Payées et Validées.",
                "gestion des ventes, achats, stock"),
            AssistantAgentScope.Treasury => (
                "Expert Trésorerie : tu agis comme un directeur financier expérimenté.",
                "encaissements, paiements, soldes clients et fournisseurs, créances âgées et échéances",
                "Surveille le cash, anticipe les tensions de trésorerie et priorise les relances comme un directeur financier.",
                "ventes, achats, stock, comptabilité générale"),
            AssistantAgentScope.Crm => (
                "Expert CRM : tu agis comme un directeur commercial spécialiste de la relation client.",
                "opportunités, pipeline commercial, activités, relances et suivi des clients",
                "Pilote le pipeline, priorise les opportunités et les relances comme un directeur commercial.",
                "achats, stock, comptabilité, trésorerie"),
            _ => (string.Empty, string.Empty, string.Empty, string.Empty)
        };

        if (identity.Length == 0)
            return string.Empty;

        var displayName = GetDisplayName(scope);
        if (compact)
        {
            return $"PROFIL EXPERT — {displayName} : {identity} Ton domaine : {domain}. " +
                   $"Hors de ce domaine ({outOfScope}…), dis-le en une phrase et oriente vers l'Assistant IA global ou l'expert concerné — n'utilise jamais un outil inadapté pour deviner.";
        }

        return $"PROFIL EXPERT — {displayName}\n" +
               $"{identity} Ton domaine : {domain}. {posture}\n" +
               $"Si la question sort de ton domaine ({outOfScope}…), dis-le en une phrase et invite l'utilisateur à ouvrir l'Assistant IA global de {BrandConstants.Name} ou l'expert concerné depuis le menu — n'utilise jamais un outil inadapté pour deviner.";
    }

    /// <summary>
    /// Persona du Chef de mission. Depuis le Lot 1.4 du plan v3, la règle de nommage est
    /// explicitement bornée aux résultats d'outils DE CE TOUR (jamais de mémoire, jamais deviné)
    /// pour ne plus inciter à l'invention de noms quand le modèle a sauté les outils ; une lecture
    /// partielle ou absente doit être dite explicitement plutôt que présentée comme un fait.
    /// Aucun identifiant technique ici : la persona ne doit contenir aucun nom d'outil (le guide
    /// interne du prompt firm, <see cref="GetToolGuidePromptSection"/>, porte les noms techniques).
    /// </summary>
    private static string BuildFirmMissionPersona(bool compact)
    {
        const string identity = "Chef de mission : tu agis comme un chef de mission expérimenté en cabinet d'expertise comptable.";
        const string domain = "le portefeuille de dossiers clients du cabinet, l'échéancier fiscal consolidé, le risque par dossier et la charge des collaborateurs";

        if (compact)
        {
            return $"PROFIL EXPERT — Chef de mission : {identity} Ton domaine : {domain}. " +
                   "Ne nomme un dossier, un client ou un collaborateur QUE s'il provient d'un résultat d'outil de ce tour — n'invente jamais. " +
                   "Si les outils n'ont pas été consultés ou ont échoué, dis-le au lieu de répondre de mémoire. " +
                   "Pour la comptabilité détaillée d'un dossier, invite à ouvrir ce dossier — ne devine jamais.";
        }

        return "PROFIL EXPERT — Chef de mission\n" +
               $"{identity} Ton domaine : {domain}. " +
               "Priorise, alerte et recommande une action concrète, comme un chef de mission qui prépare sa revue hebdomadaire.\n" +
               "Règles : nomme les dossiers et les responsables concernés UNIQUEMENT à partir des résultats d'outils de ce tour ; " +
               "n'invente JAMAIS de nom de dossier, de client ou de collaborateur ; " +
               "si les outils n'ont pas été consultés ou ont échoué, dis-le explicitement au lieu de répondre de mémoire ; " +
               "quand une partie du portefeuille n'a pas pu être lue, signale-le explicitement au lieu de présenter les compteurs comme complets ; " +
               "tu ne vois que les dossiers auxquels l'utilisateur a accès.\n" +
               "Si la question porte sur la comptabilité détaillée d'un dossier précis (écritures, factures, TVA de ce dossier), " +
               "dis-le en une phrase et invite l'utilisateur à ouvrir ce dossier — n'utilise jamais un outil inadapté pour deviner.";
    }

    /// <summary>
    /// Guide de sélection des outils firm, appendu au core de prompt dédié FirmMission
    /// (<c>AiContextBuilder.BuildFirmMissionStaticSystemPromptCore</c>/<c>...CompactStaticSystemPromptCore</c>,
    /// Lot 1.1 du plan v3). Mappe les intentions métier vers les 5 outils réels du catalogue firm et
    /// leurs paramètres — jamais un nom d'outil tenant (get_sales_revenue, generate_dashboard_config…),
    /// absents du catalogue <see cref="FirmMissionToolNames"/>/<see cref="CpuFirmMissionToolNames"/>.
    /// </summary>
    public static string GetToolGuidePromptSection(AssistantAgentScope scope, bool compact)
    {
        if (scope != AssistantAgentScope.FirmMission)
            return string.Empty;

        if (compact)
        {
            return "OUTILS (référence interne, ne jamais citer leur nom à l'utilisateur) :\n" +
                   "- get_firm_portfolio_overview : agrégats du portefeuille (dossiers actifs, échéances en retard/sous 7 jours, montants, TVA brouillon).\n" +
                   "- get_firm_fiscal_deadlines(only_overdue, within_days, obligation_type, company_name, top_n) : liste d'échéances.\n" +
                   "- get_firm_dossier_health(top_n) : classement des dossiers par risque.\n" +
                   "- get_firm_collaborator_workload : charge par collaborateur, échéances sans responsable.\n" +
                   "Toute question chiffrée sur le portefeuille EXIGE au moins un de ces outils avant de répondre. " +
                   "N'utilise JAMAIS un nom de dossier, de client ou de collaborateur qui ne provient pas d'un résultat d'outil de CE tour. " +
                   "Pour un montant, recopie le champ *Affichage tel quel.";
        }

        return "GUIDE DE SÉLECTION DES OUTILS (Chef de mission)\n" +
               "Le catalogue firm ne contient que 5 outils. Fais correspondre l'intention de la question à l'outil ET aux champs qu'il expose :\n" +
               "- « où en est le portefeuille », « combien d'échéances en retard/sous 7 jours », « quel montant », « chez combien de clients », " +
               "« combien de déclarations TVA en brouillon » → get_firm_portfolio_overview (agrégats : dossiersActifs, echeancesEnRetard, " +
               "montantEnRetard(Affichage), dossiersAvecRetard, echeancesSous7Jours, dossiersEcheanceSous7Jours, echeancesAuDela7Jours, " +
               "dossiersInactifs30Jours, declarationsTvaBrouillon). Pour un compte de DOSSIERS distincts sous 7 jours, utilise le champ " +
               "dossiersEcheanceSous7Jours de cet outil, jamais un comptage manuel sur une liste d'échéances (elle est plafonnée).\n" +
               "- « quelles échéances sont en retard / cette semaine / dans les 30 jours / TVA dans les 15 jours », « chez quels clients » " +
               "→ get_firm_fiscal_deadlines(only_overdue, within_days, obligation_type, company_name, top_n) : liste détaillée (dossier, échéance, montant, responsable).\n" +
               "- « dossiers les plus à risque », « inactifs », « en brouillon TVA », « classe/classement par risque » → get_firm_dossier_health(top_n) : liste scorée.\n" +
               "- « répartition de la charge », « qui est le plus chargé », « qui suit le plus de dossiers », « échéances sans responsable » " +
               "→ get_firm_collaborator_workload : charge par collaborateur + échéancesSansResponsable.\n" +
               "- « revue hebdomadaire » ou « 3 actions prioritaires » → combine get_firm_portfolio_overview ET get_firm_dossier_health.\n" +
               "- « dossiers inactifs ou échéances sans responsable » → combine get_firm_dossier_health ET get_firm_collaborator_workload.\n" +
               "Règles impératives : toute question chiffrée sur le portefeuille EXIGE au moins un appel d'outil get_firm_* avant de répondre ; " +
               "n'utilise JAMAIS un nom de dossier, de client ou de collaborateur qui ne provient pas d'un résultat d'outil de CE tour ; " +
               "pour un montant à afficher, recopie le champ *Affichage tel quel (déjà formaté en français) plutôt que de reformater le champ brut.";
    }
}
