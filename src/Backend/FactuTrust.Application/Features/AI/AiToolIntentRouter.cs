using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Réduit le catalogue d'outils exposé au LLM selon l'intention détectée dans la requête.
/// Retombe sur <see cref="AiToolIntent.Fallback"/> (catalogue complet) en cas de doute.
/// </summary>
public static class AiToolIntentRouter
{
    public enum AiToolIntent
    {
        Fallback,
        Greeting,
        Sales,
        Stock,
        Accounting,
        Forecasting,
        Chart,
        Synthesis
    }

    private static readonly string[] GreetingKeywords =
    [
        "bonjour", "bonsoir", "salut", "hello", "coucou", "merci", "aide", "help",
        "comment ca marche", "comment utiliser", "qui es-tu", "qui es tu", "presente-toi", "presente toi",
        "presentez-vous", "presentez vous", "vous etes qui", "vous êtes qui", "c'est quoi", "c est quoi",
        "instafact"
    ];

    /// <summary>
    /// Marqueur interne consommé par <see cref="MatchesKeyword"/> : force la détection en mot isolé
    /// du fragment « ca » (au lieu des anciens fragments bruts « ca »/" ca" qui matchaient "cabinet",
    /// "cadeau", "caisse", "occasion"…). Ne JAMAIS matcher via <see cref="string.Contains(string)"/> direct.
    /// </summary>
    private const string WholeWordCaMarker = "\u0000ca-whole-word\u0000";

    private static readonly string[] SalesKeywords =
    [
        "chiffre d'affaires", "chiffre d affaires", WholeWordCaMarker, "mon ca", "ca du", "ca ce", "ca mois",
        "revenu", "vente", "ventes", "facture", "factures", "client", "clients", "encaisse", "encaissement",
        "paiement", "paiements", "marge", "panier", "commercial", "meilleur client", "top client",
        "impaye", "creance", "gagne", "gagné", "gagnes", "combien", "aujourd'hui", "aujourdhui", "du jour", "ce jour"
    ];

    private static readonly HashSet<string> CpuCoreToolNames = new(StringComparer.Ordinal)
    {
        "get_sales_revenue",
        "get_client_payments",
        "get_client_balances",
        "get_commercial_profit",
        "get_stock_snapshot",
        "get_accounting_dashboard",
        "generate_dashboard_config",
        "propose_follow_up_prompts"
    };

    /// <summary>
    /// Catalogue de l'intent Synthesis (suivi conversationnel après une analyse) : cœur LECTURE +
    /// dashboard. EXCLUT volontairement les outils paramétriques/spécialisés qui déraillent un suivi
    /// (analyze_seasonal_impact → product_id requis, get_replenishment_recommendations, forecast_revenue,
    /// get_promotion_recommendations, get_abc_xyz_classification, CRM/mutations). Le modèle répond ainsi
    /// depuis l'analyse déjà présente dans l'historique ; les données usuelles restent accessibles.
    /// </summary>
    private static readonly HashSet<string> SynthesisToolNames = new(StringComparer.Ordinal)
    {
        "resolve_reporting_period",
        "get_sales_revenue",
        "get_client_payments",
        "get_client_balances",
        "get_commercial_profit",
        "get_stock_snapshot",
        "get_accounting_dashboard",
        "generate_dashboard_config",
        "propose_follow_up_prompts"
    };

    /// <summary>Outils ventes exposés en CPU (intent Sales) — schémas réduits pour tenir dans num_ctx fixe.</summary>
    private static readonly HashSet<string> CpuSalesToolNames = new(StringComparer.Ordinal)
    {
        "get_sales_revenue",
        "get_client_payments",
        "get_client_balances",
        "get_commercial_profit",
        "generate_dashboard_config",
        "propose_follow_up_prompts"
    };

    private static readonly HashSet<string> CpuStockToolNames = new(StringComparer.Ordinal)
    {
        "get_stock_snapshot",
        "get_replenishment_recommendations",
        "generate_dashboard_config",
        "propose_follow_up_prompts"
    };

    private static readonly HashSet<string> CpuAccountingToolNames = new(StringComparer.Ordinal)
    {
        "get_accounting_dashboard",
        "get_client_balances",
        "get_client_aging",
        "generate_dashboard_config",
        "propose_follow_up_prompts"
    };

    private static readonly string[] StockKeywords =
    [
        "stock", "inventaire", "entrepot", "rupture", "reappro", "replenishment",
        "mouvement de stock", "quantite", "article", "produit dormant", "abc", "xyz"
    ];

    private static readonly string[] AccountingKeywords =
    [
        "compta", "comptable", "bilan", "balance", "tva", "ecriture", "journal", "grand livre",
        "resultat", "exercice", "fournisseur", "dette", "aging", "balance agee"
    ];

    private static readonly string[] ForecastingKeywords =
    [
        "prevision", "forecast", "tendance", "projection", "promotion", "recommandation"
    ];

    /// <summary>
    /// Termes de visualisation NON ambigus déclenchant l'intent <see cref="AiToolIntent.Chart"/>.
    /// Restreint le catalogue à la lecture + generate_dashboard_config (zéro outil de mutation/CRM),
    /// pour que « génère un graphique à partir de ces chiffres » ne dérive jamais vers une écriture.
    /// </summary>
    private static readonly string[] ChartKeywords =
    [
        "graphique", "graphiques", "chart", "diagramme", "courbe", "camembert", "histogramme",
        "generate_dashboard_config", "visualis"
    ];

    private static readonly HashSet<string> SalesToolNames = new(StringComparer.Ordinal)
    {
        "resolve_reporting_period",
        "get_tunisian_commercial_calendar",
        "get_sales_revenue",
        "get_client_payments",
        "get_client_balances",
        "get_commercial_profit",
        "get_product_performance",
        "get_basket_metrics",
        "get_client_aging",
        "generate_dashboard_config",
        "propose_client_actions",
        "propose_follow_up_prompts"
    };

    private static readonly HashSet<string> StockToolNames = new(StringComparer.Ordinal)
    {
        "resolve_reporting_period",
        "get_tunisian_commercial_calendar",
        "get_stock_snapshot",
        "get_stock_movements",
        "get_products_never_sold",
        "get_replenishment_recommendations",
        "get_abc_xyz_classification",
        "generate_dashboard_config",
        "propose_follow_up_prompts"
    };

    private static readonly HashSet<string> AccountingToolNames = new(StringComparer.Ordinal)
    {
        "resolve_reporting_period",
        "get_tunisian_commercial_calendar",
        "get_accounting_dashboard",
        "get_client_aging",
        "get_client_balances",
        "get_supplier_balances",
        "compliance_check_invoice",
        "generate_dashboard_config",
        "propose_client_actions",
        "propose_follow_up_prompts"
    };

    private static readonly HashSet<string> ForecastingToolNames = new(StringComparer.Ordinal)
    {
        "resolve_reporting_period",
        "get_tunisian_commercial_calendar",
        "forecast_revenue",
        "get_promotion_recommendations",
        "get_replenishment_recommendations",
        "get_sales_revenue",
        "generate_dashboard_config",
        "propose_follow_up_prompts"
    };

    /// <summary>
    /// Catalogue de l'intent Chart : cœur LECTURE SEULE + generate_dashboard_config. AUCUN outil de
    /// mutation/CRM — garantit qu'une demande de graphique ne peut pas déclencher une écriture, même
    /// si EnableMutationTools est actif. Couvre « graphique depuis le contexte » et « lis puis trace ».
    /// </summary>
    private static readonly HashSet<string> ChartToolNames = new(StringComparer.Ordinal)
    {
        "resolve_reporting_period",
        "get_sales_revenue",
        "get_client_payments",
        "get_client_balances",
        "get_commercial_profit",
        "get_stock_snapshot",
        "get_accounting_dashboard",
        "generate_dashboard_config",
        "propose_follow_up_prompts"
    };

    public static AiToolIntent Resolve(string? message, AssistantMode assistantMode)
    {
        // Compliance / ScreenAnalysis / StudioBuilder already expose a focused catalogue via
        // GetDefinitionsForMode — keep the full mode set (Fallback = no extra keyword filtering).
        if (assistantMode is AssistantMode.ScreenAnalysis or AssistantMode.Compliance or AssistantMode.StudioBuilder)
            return AiToolIntent.Fallback;

        if (string.IsNullOrWhiteSpace(message))
            return AiToolIntent.Fallback;

        var normalized = RemoveDiacritics(message).ToLowerInvariant();

        if (IsGreetingOnly(normalized))
            return AiToolIntent.Greeting;

        if (IsIdentityQuestion(normalized))
            return AiToolIntent.Greeting;

        // Priorité aux demandes de graphique : catalogue restreint (lecture + dashboard, zéro mutation).
        if (IsChartRequest(normalized))
            return AiToolIntent.Chart;

        var scores = new Dictionary<AiToolIntent, int>
        {
            [AiToolIntent.Sales] = ScoreKeywords(normalized, SalesKeywords),
            [AiToolIntent.Stock] = ScoreKeywords(normalized, StockKeywords),
            [AiToolIntent.Accounting] = ScoreKeywords(normalized, AccountingKeywords),
            [AiToolIntent.Forecasting] = ScoreKeywords(normalized, ForecastingKeywords)
        };

        ApplyColloquialSalesBoost(normalized, scores);

        var best = scores.OrderByDescending(kv => kv.Value).First();
        if (best.Value <= 0)
            return AiToolIntent.Fallback;

        var second = scores.OrderByDescending(kv => kv.Value).Skip(1).First().Value;
        if (second > 0 && best.Value == second)
            return AiToolIntent.Fallback;

        return best.Key;
    }

    /// <summary>True for pure salutations and identity questions — use conversational fast-path (no tools).</summary>
    public static bool ShouldUseConversationalFastPath(AiToolIntent intent, string? message)
    {
        if (intent == AiToolIntent.Greeting)
            return true;
        return LooksLikeIdentityQuestion(message);
    }

    public static bool LooksLikeIdentityQuestion(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;
        var normalized = RemoveDiacritics(message).ToLowerInvariant();
        return IsIdentityQuestion(normalized);
    }

    private static readonly Regex ClientRankingRegex = new(
        @"\b(?:(?<n>\d{1,3})\s+)?(?:meilleurs?|top|pires?)\s+(?:(?<n2>\d{1,3})\s+)?clients?\b|\btop\s*(?<n3>\d{1,3})\s+clients?\b|\bclassement\s+(?:des\s+)?clients?\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Nombre de lignes par défaut d'un classement clients quand la question n'en précise pas.</summary>
    public const int DefaultClientRankingTopN = 5;

    /// <summary>
    /// Détecte une question de CLASSEMENT CLIENTS (« mes 5 meilleurs clients », « top 3 clients »,
    /// « pires clients »…) et en extrait le top_n demandé (borné 1–50 ; 5 par défaut).
    /// Null si la question n'est pas un classement clients. Sert au raccourci déterministe
    /// get_sales_revenue(group_by=Client) — le petit modèle choisissait un mauvais outil.
    /// </summary>
    public static int? TryInferClientRankingTopN(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;
        var normalized = RemoveDiacritics(message).ToLowerInvariant();
        var match = ClientRankingRegex.Match(normalized);
        if (!match.Success)
            return null;

        var rawN = match.Groups["n"].Success ? match.Groups["n"].Value
            : match.Groups["n2"].Success ? match.Groups["n2"].Value
            : match.Groups["n3"].Success ? match.Groups["n3"].Value
            : null;
        if (rawN is not null && int.TryParse(rawN, out var n) && n > 0)
            return Math.Min(n, 50);
        return DefaultClientRankingTopN;
    }

    /// <summary>Infer sales reporting preset from user phrasing (null if ambiguous).</summary>
    public static string? InferSalesPreset(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;
        var normalized = RemoveDiacritics(message).ToLowerInvariant();
        if (normalized.Contains("aujourd'hui", StringComparison.Ordinal)
            || normalized.Contains("aujourdhui", StringComparison.Ordinal)
            || normalized.Contains("du jour", StringComparison.Ordinal)
            || normalized.Contains("ce jour", StringComparison.Ordinal))
        {
            return "today";
        }
        if (normalized.Contains("ce mois", StringComparison.Ordinal)
            || normalized.Contains("mois en cours", StringComparison.Ordinal)
            || normalized.Contains("mois-ci", StringComparison.Ordinal))
        {
            return "current_month";
        }
        return null;
    }

    /// <summary>Résultat de détection d'une demande de contrôle de conformité de facture.</summary>
    public sealed record ComplianceCheckDetection(string? InvoiceNumber);

    // Numéro de facture (ex. FAC-2026-000123) — extrait sur le message ORIGINAL (casse préservée).
    private static readonly Regex ComplianceInvoiceNumberRegex = new(
        @"\b[A-Za-z]{2,5}-\d{4}-\d{3,}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    // Mot de conformité (sur texte normalisé sans diacritiques) : « conformite », « conforme(s) », « non conforme ».
    private static readonly Regex ComplianceWordRegex = new(
        @"\bconformite\b|\bconformes?\b|\bnon[- ]conforme\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Détecte une demande NON AMBIGUË de contrôle de conformité d'une facture (« vérifie la conformité
    /// de ma dernière facture », « la facture FAC-2026-000123 est-elle conforme ? ») et en extrait le
    /// numéro éventuel. Null sinon — « conformité TVA » (pas de facture) et « vérifie ma facture »
    /// (pas de mot conformité) ne matchent pas. Sert au raccourci déterministe du contrôle de conformité :
    /// l'outil résout lui-même la facture (numéro ou plus récente), ce qu'un petit modèle en un seul
    /// round d'outils sur CPU ne peut pas enchaîner.
    /// </summary>
    public static ComplianceCheckDetection? TryInferComplianceCheckInvoice(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;
        var normalized = RemoveDiacritics(message).ToLowerInvariant();
        if (!ComplianceWordRegex.IsMatch(normalized) || !normalized.Contains("facture", StringComparison.Ordinal))
            return null;
        var number = ComplianceInvoiceNumberRegex.Match(message);
        return new ComplianceCheckDetection(number.Success ? number.Value.ToUpperInvariant() : null);
    }

    /// <summary>
    /// Suffixe de prompt quand le raccourci conformité a pré-exécuté le contrôle : le modèle doit
    /// synthétiser le résultat déjà présent, pas rappeler d'outil. Aucun nom d'outil snake_case
    /// (la garde anti-fuite n'a jamais à intervenir).
    /// </summary>
    public const string ComplianceShortcutPromptSuffix =
        "CONTRÔLE DE CONFORMITÉ : le contrôle de la facture a DÉJÀ été exécuté — son résultat est dans la conversation. "
        + "Synthétise-le en français : numéro de facture, état global, constats classés (ok / warning / blocking), totaux. "
        + "N'appelle AUCUN autre outil sauf demande explicite.";

    // Marqueurs (sans accents, minuscules) d'une question de CONSEIL / recommandation.
    private static readonly string[] AdviceKeywords =
    [
        "conseil", "conseils", "recommande", "recommandes", "que me conseilles", "suggere", "suggestions",
        "comment je peux", "comment puis-je", "comment puis je", "comment faire pour", "comment ameliorer",
        "ameliorer", "augmenter", "optimiser", "reduire", "diminuer", "gagner plus", "vendre plus",
        "strategie", "strategies", "eviter les pertes", "limiter les pertes"
    ];

    /// <summary>
    /// True pour une question de CONSEIL (« comment gagner plus », « vos conseils pour… ») : la réponse
    /// attendue est une recommandation ANCRÉE dans les données, pas un méta-discours sur les capacités.
    /// Indépendant de l'intent (une question de conseil peut scorer Sales via « gagner » ou Fallback).
    /// </summary>
    public static bool LooksLikeAdviceQuestion(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;
        var normalized = RemoveDiacritics(message).ToLowerInvariant();
        foreach (var kw in AdviceKeywords)
        {
            if (normalized.Contains(kw, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Suffixe de prompt pour les questions de conseil — cumulable avec le suffixe d'intent.
    /// </summary>
    public const string AdvicePromptSuffix =
        "QUESTION DE CONSEIL : 1) appelle d'abord 1-2 outils de DONNÉES pertinents (CA par produit ou client, "
        + "performance produits ; recommandations de réapprovisionnement/promotions si la question concerne le stock) ; "
        + "2) réponds avec 3 à 5 recommandations CONCRÈTES et CHIFFRÉES tirées de ces données réelles "
        + "(produits/clients nommés, montants TND) ; "
        + "3) ne mentionne JAMAIS d'identifiants techniques internes (noms de fonctions ou d'outils) — "
        + "décris des actions métier réalisables dans l'application (écrans Ventes, Stock, Promotions).";

    public static string BuildIntentPromptSuffix(AiToolIntent intent)
    {
        return intent switch
        {
            AiToolIntent.Greeting =>
                "MODE CONVERSATION : réponds poliment en français (2–4 phrases). N'appelle AUCUN outil. Ne cite pas de chiffres ni de données métier.",
            AiToolIntent.Sales =>
                "INTENTION VENTES/CA : si l'utilisateur dit « gagné », « combien » ou « aujourd'hui », appelle get_sales_revenue avec preset=today. "
                + "Ne redemande pas la période si elle est explicite (aujourd'hui → today, ce mois → current_month). "
                + "Meilleurs/pires clients → get_sales_revenue avec group_by=Client et top_n=nombre demandé ; sans période précisée, preset=last_30_days. "
                + "Pour le total, cite EXCLUSIVEMENT le champ `totalRevenue` du résultat ; n'additionne ni ne recalcule JAMAIS les lignes, et ne présente JAMAIS une seule ligne comme le total. "
                + "Cite la période du champ `period` (from/to) du résultat. "
                + "« Encaissé » → get_client_payments ; « marge » → get_commercial_profit.",
            AiToolIntent.Fallback =>
                "Si la question porte sur le CA ou revenus du jour, appelle get_sales_revenue(preset=today) directement sans demander de clarification.",
            AiToolIntent.Chart =>
                "INTENTION GRAPHIQUE : utilise generate_dashboard_config pour produire UN graphique à partir des chiffres déjà obtenus. "
                + "N'appelle AUCUN outil de création ou de modification.",
            AiToolIntent.Synthesis =>
                "QUESTION DE SUIVI : appuie-toi EN PRIORITÉ sur l'analyse et les données déjà présentes dans la conversation "
                + "pour répondre directement. N'appelle un outil QUE si une donnée chiffrée nouvelle est réellement indispensable. "
                + "Si la question porte sur un total de CA déjà obtenu, base-toi sur le champ `totalRevenue` du résultat de get_sales_revenue ; "
                + "n'additionne ni ne recalcule JAMAIS les lignes, et ne présente JAMAIS une seule ligne comme le total.",
            _ => string.Empty
        };
    }

    public static bool ShouldIncludeTool(
        string toolName,
        AiToolIntent intent,
        bool enableMutationTools,
        bool isMutating,
        bool useCpuCoreSubset = false,
        bool useCpuIntentSubset = false)
    {
        if (isMutating && !enableMutationTools)
            return false;

        if (useCpuCoreSubset && intent == AiToolIntent.Fallback)
            return CpuCoreToolNames.Contains(toolName);

        if (useCpuIntentSubset && intent is AiToolIntent.Sales or AiToolIntent.Stock or AiToolIntent.Accounting)
            return GetCpuToolsForIntent(intent).Contains(toolName);

        return intent switch
        {
            AiToolIntent.Greeting => false,
            AiToolIntent.Sales => SalesToolNames.Contains(toolName),
            AiToolIntent.Stock => StockToolNames.Contains(toolName),
            AiToolIntent.Accounting => AccountingToolNames.Contains(toolName),
            AiToolIntent.Forecasting => ForecastingToolNames.Contains(toolName),
            AiToolIntent.Chart => ChartToolNames.Contains(toolName),
            AiToolIntent.Synthesis => SynthesisToolNames.Contains(toolName),
            _ => true
        };
    }

    private static HashSet<string> GetCpuToolsForIntent(AiToolIntent intent) =>
        intent switch
        {
            AiToolIntent.Sales => CpuSalesToolNames,
            AiToolIntent.Stock => CpuStockToolNames,
            AiToolIntent.Accounting => CpuAccountingToolNames,
            _ => CpuCoreToolNames
        };

    private static bool IsGreetingOnly(string normalized)
    {
        var trimmed = normalized.Trim();
        if (trimmed.Length > 80)
            return false;

        foreach (var kw in GreetingKeywords)
        {
            if (trimmed.Contains(kw, StringComparison.Ordinal))
            {
                var stripped = trimmed.Replace(kw, "", StringComparison.Ordinal).Trim(' ', '?', '!', '.', ',');
                if (stripped.Length <= 12)
                    return true;
            }
        }

        return false;
    }

    private static bool IsIdentityQuestion(string normalized)
    {
        if (normalized.Contains("vous etes qui", StringComparison.Ordinal)
            || normalized.Contains("vous êtes qui", StringComparison.Ordinal)
            || normalized.Contains("qui es tu", StringComparison.Ordinal)
            || normalized.Contains("qui es-tu", StringComparison.Ordinal)
            || normalized.Contains("tu es qui", StringComparison.Ordinal)
            || normalized.Contains("presente toi", StringComparison.Ordinal)
            || normalized.Contains("presente-toi", StringComparison.Ordinal)
            || normalized.Contains("presentez vous", StringComparison.Ordinal)
            || normalized.Contains("presentez-vous", StringComparison.Ordinal)
            || normalized.Contains("c est quoi instafact", StringComparison.Ordinal)
            || normalized.Contains("c'est quoi instafact", StringComparison.Ordinal))
        {
            return true;
        }
        return false;
    }

    private static bool IsChartRequest(string normalized)
    {
        foreach (var kw in ChartKeywords)
        {
            if (normalized.Contains(kw, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static void ApplyColloquialSalesBoost(string normalized, Dictionary<AiToolIntent, int> scores)
    {
        var hasRevenueHint = normalized.Contains("gagn", StringComparison.Ordinal)
            || ContainsWholeWord(normalized, "ca")
            || normalized.Contains("vente", StringComparison.Ordinal)
            || normalized.Contains("combien", StringComparison.Ordinal);
        var hasTodayHint = normalized.Contains("aujourd", StringComparison.Ordinal)
            || normalized.Contains("du jour", StringComparison.Ordinal)
            || normalized.Contains("ce jour", StringComparison.Ordinal);
        if (hasRevenueHint && hasTodayHint)
            scores[AiToolIntent.Sales] += 2;
    }

    private static int ScoreKeywords(string normalized, string[] keywords)
    {
        var score = 0;
        foreach (var kw in keywords)
        {
            if (MatchesKeyword(normalized, kw))
                score++;
        }
        return score;
    }

    /// <summary>
    /// Matche un mot-clé de scoring. Cas spécial <see cref="WholeWordCaMarker"/> : détection de « ca »
    /// en mot isolé (frontières non alphanumériques) pour éviter les faux positifs « cabinet », « cadeau »,
    /// « caisse », « occasion »… tout en conservant les tournures colloquiales « mon ca aujourd'hui »,
    /// « ca du mois », « CA ce mois-ci ». Les autres mots-clés gardent le simple <see cref="string.Contains(string, StringComparison)"/>
    /// existant (comportement inchangé).
    /// </summary>
    private static bool MatchesKeyword(string normalized, string keyword)
        => keyword == WholeWordCaMarker
            ? ContainsWholeWord(normalized, "ca")
            : normalized.Contains(keyword, StringComparison.Ordinal);

    /// <summary>
    /// True si <paramref name="word"/> apparaît dans <paramref name="text"/> comme mot isolé, c'est-à-dire
    /// borné par des caractères non alphanumériques (ou le début/la fin de la chaîne). Utilisé pour éviter
    /// que des fragments courts comme « ca » ne matchent à l'intérieur d'un mot plus long (« cabinet »).
    /// </summary>
    private static bool ContainsWholeWord(string text, string word)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(word))
            return false;

        var searchStart = 0;
        while (true)
        {
            var index = text.IndexOf(word, searchStart, StringComparison.Ordinal);
            if (index < 0)
                return false;

            var leftBoundaryOk = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            var rightIndex = index + word.Length;
            var rightBoundaryOk = rightIndex >= text.Length || !char.IsLetterOrDigit(text[rightIndex]);
            if (leftBoundaryOk && rightBoundaryOk)
                return true;

            searchStart = index + 1;
        }
    }

    private static string RemoveDiacritics(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}