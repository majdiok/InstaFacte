using System.Globalization;
using System.Text;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.Studio.Common.SqlReport;

namespace FactuTrust.Application.Features.Studio.Ai;

/// <summary>Demande d'état reconnue dans le message de l'utilisateur.</summary>
/// <param name="PresetKey">Clé d'un <see cref="SqlReportPreset"/> — jamais inventée.</param>
/// <param name="PeriodPreset">Clé <see cref="ReportingPeriodResolver"/>, ou null pour la période par défaut.</param>
/// <param name="Save">Vrai quand l'utilisateur demande explicitement de CONSERVER l'état.</param>
public sealed record StudioReportDetection(string PresetKey, string? PeriodPreset, bool Save);

/// <summary>
/// Reconnaît une demande d'ÉTAT dans le langage de l'utilisateur, et la traduit en préréglage +
/// période. Pur, sans dépendance, entièrement testable.
///
/// Raison d'être : en mode StudioBuilder, l'assistant n'a aucun raccourci déterministe (les trois
/// existants sont réservés aux modes Default/Compliance). Le résultat dépendait donc entièrement de
/// la capacité du modèle configuré à émettre un appel d'outil — ce qu'un modèle de code ne fait pas
/// de façon fiable. Ce routeur permet de PRÉ-EXÉCUTER l'état, comme le fait déjà
/// <see cref="AiToolIntentRouter.TryInferComplianceCheckInvoice"/> pour le contrôle de conformité.
///
/// Doctrine : en cas de doute, on ne devine pas. Un score insuffisant renvoie <c>null</c> et le flux
/// normal (modèle + catalogue d'outils) reprend la main. Mieux vaut ne rien pré-exécuter qu'exécuter
/// le mauvais état.
/// </summary>
public static class StudioReportIntentRouter
{
    /// <summary>Score minimal pour armer le raccourci : un domaine reconnu ET un axe d'analyse.</summary>
    private const int MinimumScore = 5;

    private const int DomainWeight = 2;
    private const int AxisWeight = 3;

    /// <summary>Période retenue quand l'utilisateur n'en donne aucune. Toujours annoncée en clair.</summary>
    public const string DefaultPeriodPreset = ReportingPeriodResolver.PresetYearToDate;

    // ---- Portes d'entrée -------------------------------------------------------------------

    /// <summary>Marqueurs d'une intention d'ANALYSE (par opposition à une intention de construction).</summary>
    private static readonly string[] AnalysisMarkers =
    {
        "rapport", "etat des", "un etat", "etats des", "analyse", "analytique", "statistique",
        "tableau de bord", "palmares", "classement", "repartition", "evolution", "recapitulatif",
        "chiffre d'affaires", "chiffre d affaires", "combien", "total des", "totaux",
        "top ", "meilleurs", "meilleures"
    };

    /// <summary>
    /// Marqueurs d'une demande de STRUCTURE à créer. Ils opposent un veto : « crée une table Rapports
    /// avec titre et date » contient « rapport » mais n'est pas une demande d'analyse.
    /// </summary>
    private static readonly string[] StructureVetoMarkers =
    {
        "avec les champs", "avec des champs", "avec le champ", "avec un champ",
        "systeme de gestion", "formulaire", "relation entre", "cle etrangere",
        "table avec", "tables liees", "ajoute un champ", "ajoute une colonne",
        "nouvelle table", "nouvelles tables"
    };

    private static readonly string[] SaveMarkers =
    {
        "enregistre", "enregistrer", "sauvegarde", "sauvegarder", "conserve", "conserver",
        "garde cet", "garder cet", "ajoute aux rapports", "ajouter aux rapports"
    };

    // ---- Correspondance préréglage ↔ langage ------------------------------------------------
    // Volontairement porté ICI et non dans SqlReportPresetCatalog : le catalogue décrit des états,
    // ce tableau décrit la manière dont on les DEMANDE. Les deux évoluent séparément.

    private sealed record PresetMatcher(string PresetKey, string[] Domain, string[] Axis);

    // Les tableaux de domaine sont déclarés AVANT Matchers : les initialiseurs de champs statiques
    // s'exécutent dans l'ordre de déclaration, l'inverse laisserait des références nulles.
    private static readonly string[] VentesDomain =
    {
        "vente", "ventes", "vendu", "vendus", "chiffre d'affaires", "chiffre d affaires", "facturation", " ca ",
        // Expression complète, volontairement : une « remise en banque » est de la trésorerie,
        // une « remise accordée » est commerciale. Le mot « remise » seul serait ambigu.
        "remise accordee", "remises accordees"
    };
    private static readonly string[] AchatsDomain =
        { "achat", "achats", "approvisionnement", "fournisseur", "fournisseurs" };
    private static readonly string[] StockDomain =
        { "stock", "stocks", "inventaire", "entrepot", "depot" };
    private static readonly string[] TresorerieDomain =
        { "encaissement", "encaissements", "reglement", "reglements", "paiement", "paiements", "tresorerie", "caisse" };
    private static readonly string[] ComptaDomain =
        { "ecriture", "ecritures", "comptable", "comptabilite", "grand livre", "balance" };
    private static readonly string[] PaieDomain =
        { "paie", "salaire", "salaires", "salarie", "salaries", "masse salariale", "effectif" };

    private static readonly PresetMatcher[] Matchers =
    {
        // --- Ventes ---
        new("ventes_par_produit", VentesDomain, new[] { "produit", "article", "reference", "item" }),
        new("ventes_par_client", VentesDomain, new[] { "client", "acheteur", "compte client" }),
        new("ventes_par_mois", VentesDomain, new[] { "mois", "mensuel", "mensuelle", "periode", "evolution" }),
        new("ventes_par_produit_et_mois", VentesDomain, new[] { "produit et mois", "saisonnalite", "produit par mois" }),
        new("remises_accordees", VentesDomain, new[] { "remise", "rabais", "escompte", "discount" }),
        new("detail_lignes_ventes", VentesDomain, new[] { "ligne", "lignes", "detail des ventes", "detaillee ligne" }),
        new("factures_par_statut", new[] { "facture", "factures" }, new[] { "statut", "etat de paiement", "impayee", "impayees" }),

        // --- Achats ---
        new("achats_par_fournisseur", AchatsDomain, new[] { "fournisseur", "fournisseurs" }),
        new("achats_par_mois", AchatsDomain, new[] { "mois", "mensuel", "mensuelle", "evolution" }),
        new("achats_par_produit", AchatsDomain, new[] { "produit", "article", "reference" }),

        // --- Stock ---
        new("mouvements_de_stock", StockDomain, new[] { "mouvement", "mouvements", "entree", "sortie", "flux" }),
        new("stock_par_entrepot", StockDomain, new[] { "entrepot", "depot", "magasin", "disponible", "quantite" }),

        // --- Trésorerie ---
        new("encaissements_par_mois", TresorerieDomain, new[] { "mois", "mensuel", "mensuelle", "evolution" }),
        new("encaissements_par_mode", TresorerieDomain, new[] { "mode", "moyen", "especes", "cheque", "virement" }),
        new("encaissements_par_client", TresorerieDomain, new[] { "client", "clients" }),

        // --- Comptabilité ---
        new("ecritures_par_compte", ComptaDomain, new[] { "compte", "comptes", "balance", "grand livre" }),
        new("ecritures_par_journal", ComptaDomain, new[] { "journal", "journaux" }),

        // --- Paie ---
        new("masse_salariale_par_mois", PaieDomain, new[] { "masse salariale", "mois", "mensuel", "brut", "net" }),
        new("effectif_par_contrat", PaieDomain, new[] { "effectif", "contrat", "cdi", "cdd", "type de contrat" })
    };

    // ---- API -------------------------------------------------------------------------------

    /// <summary>
    /// Reconnaît une demande d'état non ambiguë. Renvoie <c>null</c> dès qu'un doute subsiste :
    /// message vide, intention de construction, domaine absent, ou score insuffisant.
    /// </summary>
    public static StudioReportDetection? TryInfer(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        var normalized = Pad(message);

        if (!HasAnalysisIntent(normalized) || HasStructureVeto(normalized))
            return null;

        var best = RankPresets(normalized).FirstOrDefault();
        if (best is null || best.Score < MinimumScore)
            return null;

        return new StudioReportDetection(best.PresetKey, InferPeriodPreset(normalized), HasSaveIntent(normalized));
    }

    /// <summary>
    /// Préréglages les plus proches de la demande, même quand le score reste sous le seuil. Sert à
    /// proposer des formulations qui fonctionnent quand rien n'a pu être exécuté.
    /// </summary>
    public static IReadOnlyList<string> SuggestPresets(string? message, int max = 3)
    {
        if (string.IsNullOrWhiteSpace(message) || max < 1)
            return Array.Empty<string>();

        var ranked = RankPresets(Pad(message))
            .Where(r => r.Score > 0)
            .Take(max)
            .Select(r => r.PresetKey)
            .ToList();

        // Aucun signal : proposer les états les plus universels plutôt qu'une liste vide.
        return ranked.Count > 0
            ? ranked
            : new[] { "ventes_par_produit", "ventes_par_client", "ventes_par_mois" }
                .Where(k => SqlReportPresetCatalog.Find(k) is not null)
                .Take(max)
                .ToList();
    }

    /// <summary>Vrai si le message ressemble à une demande d'analyse (sans exiger un préréglage identifiable).</summary>
    public static bool LooksLikeReportRequest(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;
        var normalized = Pad(message);
        return HasAnalysisIntent(normalized) && !HasStructureVeto(normalized);
    }

    /// <summary>
    /// Normalise puis entoure d'espaces : les marqueurs courts (« ca », « top ») doivent pouvoir
    /// matcher en début et en fin de message sans capturer « facture » ou « stock ».
    /// </summary>
    private static string Pad(string message) => " " + Normalize(message) + " ";

    // ---- Interne ---------------------------------------------------------------------------

    private sealed record RankedPreset(string PresetKey, int Score);

    private static IEnumerable<RankedPreset> RankPresets(string normalized) =>
        Matchers
            .Where(m => SqlReportPresetCatalog.Find(m.PresetKey) is not null)
            .Select(m => new RankedPreset(m.PresetKey, ScoreOf(m, normalized)))
            .Where(r => r.Score > 0)
            .OrderByDescending(r => r.Score)
            // Départage stable : l'ordre de déclaration prime, les états les plus courants d'abord.
            .ThenBy(r => Array.FindIndex(Matchers, m => m.PresetKey == r.PresetKey));

    private static int ScoreOf(PresetMatcher matcher, string normalized)
    {
        var domainHits = matcher.Domain.Count(k => normalized.Contains(k, StringComparison.Ordinal));
        if (domainHits == 0)
            return 0; // sans domaine reconnu, aucun état n'est proposable

        var axisHits = matcher.Axis.Count(k => normalized.Contains(k, StringComparison.Ordinal));
        return domainHits * DomainWeight + axisHits * AxisWeight;
    }

    private static bool HasAnalysisIntent(string normalized) =>
        AnalysisMarkers.Any(m => normalized.Contains(m, StringComparison.Ordinal));

    private static bool HasStructureVeto(string normalized) =>
        StructureVetoMarkers.Any(m => normalized.Contains(m, StringComparison.Ordinal));

    private static bool HasSaveIntent(string normalized) =>
        SaveMarkers.Any(m => normalized.Contains(m, StringComparison.Ordinal));

    /// <summary>
    /// Traduit une expression de période en préréglage <see cref="ReportingPeriodResolver"/>.
    /// Null = aucune période exprimée ; l'appelant applique <see cref="DefaultPeriodPreset"/>.
    /// </summary>
    public static string? InferPeriodPreset(string normalizedMessage)
    {
        if (string.IsNullOrWhiteSpace(normalizedMessage))
            return null;
        var n = normalizedMessage;

        if (Contains(n, "aujourd'hui", "aujourdhui", "du jour", "ce jour"))
            return ReportingPeriodResolver.PresetToday;
        if (Contains(n, "hier"))
            return ReportingPeriodResolver.PresetYesterday;
        if (Contains(n, "7 derniers jours", "sept derniers jours", "cette semaine"))
            return ReportingPeriodResolver.PresetLast7Days;
        if (Contains(n, "30 derniers jours", "trente derniers jours", "dernier mois glissant"))
            return ReportingPeriodResolver.PresetLast30Days;
        // « mois dernier » avant « ce mois » : le second est contenu dans des tournures du premier.
        if (Contains(n, "mois dernier", "dernier mois", "mois precedent", "mois passe"))
            return ReportingPeriodResolver.PresetLastMonth;
        if (Contains(n, "ce mois", "mois en cours", "mois-ci", "du mois"))
            return ReportingPeriodResolver.PresetCurrentMonth;
        if (Contains(n, "trimestre dernier", "dernier trimestre", "trimestre precedent", "trimestre passe"))
            return ReportingPeriodResolver.PresetLastCompletedQuarter;
        if (Contains(n, "ce trimestre", "trimestre en cours", "du trimestre"))
            return ReportingPeriodResolver.PresetCurrentQuarter;
        if (Contains(n, "cette annee", "annee en cours", "depuis janvier", "de l'annee", "de l annee"))
            return ReportingPeriodResolver.PresetYearToDate;

        return null;
    }

    private static bool Contains(string haystack, params string[] needles) =>
        needles.Any(n => haystack.Contains(n, StringComparison.Ordinal));

    /// <summary>Minuscules sans diacritiques : « Créé », « CRÉÉ » et « cree » se comparent à l'identique.</summary>
    public static string Normalize(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
