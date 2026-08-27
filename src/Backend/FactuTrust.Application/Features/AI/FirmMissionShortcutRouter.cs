using System.Globalization;
using System.Text;
using FactuTrust.Application.Features.AI.Tools;

namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Raccourci déterministe FirmMission (Lot 1.2 du plan v3). Classe statique pure, testable :
/// résout une question en langage naturel du Chef de mission vers ≤ 2 appels d'outils
/// <c>get_firm_*</c> réels, avec les BONS paramètres — un routage naïf par mot-clé serait
/// sémantiquement faux (ex. « pour quel montant, chez combien de clients ? » exige les AGRÉGATS de
/// l'overview, pas la liste des échéances plafonnée à <c>top_n</c>).
///
/// Couvre les 19 questions suggérées de l'accueil Chef de mission
/// (<c>agent-scopes.config.ts</c>, catégories overview/deadlines/risk/workload/review) via une
/// table de correspondance sémantique question → outils ET champs attendus (documentée dans le
/// plan v3, §2 Lot 1.2). Aucun mot-clé ne doit renvoyer plus de 2 outils.
/// </summary>
public static class FirmMissionShortcutRouter
{
    /// <summary>Un appel d'outil planifié : nom réel du catalogue firm + arguments prêts pour ExecuteAsync.</summary>
    public sealed record PlannedToolCall(string ToolName, Dictionary<string, object?> Arguments)
    {
        public PlannedToolCall(string toolName) : this(toolName, new Dictionary<string, object?>())
        {
        }
    }

    /// <summary>
    /// Résout le message utilisateur en ≤ 2 appels d'outils ordonnés. Liste vide = aucun mot-clé
    /// métier reconnu (le gate 1.3 sécurise ce cas en secours). Normalisation identique au routeur
    /// d'intention existant (diacritiques retirés, minuscules, détection en mot isolé pour les
    /// termes courts comme « ca »/« tva »).
    /// </summary>
    public static IReadOnlyList<PlannedToolCall> Resolve(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return Array.Empty<PlannedToolCall>();

        var normalized = RemoveDiacritics(message).ToLowerInvariant();

        // ── Signaux bruts, réutilisés par plusieurs règles ──
        var mentionsPortfolio = normalized.Contains("portefeuille", StringComparison.Ordinal)
            || normalized.Contains("ou en est", StringComparison.Ordinal);
        var mentionsOverdue = normalized.Contains("retard", StringComparison.Ordinal);
        var mentionsWithin7 = normalized.Contains("7 jour", StringComparison.Ordinal)
            || normalized.Contains("7 prochain", StringComparison.Ordinal)
            || normalized.Contains("sept jour", StringComparison.Ordinal)
            || normalized.Contains("cette semaine", StringComparison.Ordinal);
        var mentionsWithin30 = normalized.Contains("30 jour", StringComparison.Ordinal)
            || normalized.Contains("30 prochain", StringComparison.Ordinal);
        var mentionsWithin15 = normalized.Contains("15 jour", StringComparison.Ordinal)
            || normalized.Contains("15 prochain", StringComparison.Ordinal);
        var mentionsVat = ContainsWholeWord(normalized, "tva");
        var mentionsVatDraft = normalized.Contains("brouillon", StringComparison.Ordinal) && mentionsVat;
        var mentionsAmount = normalized.Contains("montant", StringComparison.Ordinal);
        var mentionsClientsCount = normalized.Contains("combien de client", StringComparison.Ordinal)
            || normalized.Contains("combien de clients", StringComparison.Ordinal);
        var mentionsCount = normalized.Contains("combien", StringComparison.Ordinal);
        // « quelles »/« quels » en tête de phrase interrogative = demande de LISTE plutôt que d'agrégat.
        var mentionsList = normalized.Contains("quelles", StringComparison.Ordinal)
            || normalized.Contains("quels ", StringComparison.Ordinal)
            || normalized.Contains("liste", StringComparison.Ordinal);
        var mentionsRisk = normalized.Contains("risque", StringComparison.Ordinal)
            || normalized.Contains("classe ", StringComparison.Ordinal)
            || normalized.Contains("classement", StringComparison.Ordinal)
            || normalized.Contains("classe les", StringComparison.Ordinal);
        var mentionsInactive = normalized.Contains("inactif", StringComparison.Ordinal)
            || normalized.Contains("pas eu d'ecriture", StringComparison.Ordinal)
            || normalized.Contains("pas eu decriture", StringComparison.Ordinal)
            || normalized.Contains("sans ecriture", StringComparison.Ordinal)
            || normalized.Contains("depuis un mois", StringComparison.Ordinal);
        var mentionsWorkload = normalized.Contains("charge", StringComparison.Ordinal)
            || normalized.Contains("collaborateur", StringComparison.Ordinal)
            || normalized.Contains("qui suit le plus", StringComparison.Ordinal)
            || normalized.Contains("qui est le plus", StringComparison.Ordinal);
        var mentionsUnassigned = normalized.Contains("sans responsable", StringComparison.Ordinal)
            || normalized.Contains("pas de responsable", StringComparison.Ordinal)
            || normalized.Contains("responsable designe", StringComparison.Ordinal);
        var mentionsWeeklyReview = normalized.Contains("revue hebdomadaire", StringComparison.Ordinal)
            || normalized.Contains("revue", StringComparison.Ordinal) && normalized.Contains("hebdo", StringComparison.Ordinal);
        var mentionsPriorityActions = normalized.Contains("action", StringComparison.Ordinal)
            || normalized.Contains("prioritaire", StringComparison.Ordinal)
            || normalized.Contains("priorite", StringComparison.Ordinal);

        // ── 1. Combinaisons explicites (les plus spécifiques d'abord) ──

        // « Prépare-moi la revue hebdomadaire du cabinet. »
        if (mentionsWeeklyReview)
            return new[] { Overview(), DossierHealth() };

        // « Quelles sont les 3 actions prioritaires cette semaine ? » (nouveaux mots-clés : action(s), prioritaire(s), priorité)
        if (mentionsPriorityActions)
            return new[] { Overview(), DossierHealth() };

        // « Y a-t-il des dossiers inactifs ou des échéances sans responsable à traiter ? »
        if (mentionsInactive && mentionsUnassigned)
            return new[] { DossierHealth(), Workload() };

        // ── 2. Risque / activité des dossiers → dossier_health ──

        // « Quels dossiers sont les plus à risque cette semaine ? » / « Classe les dossiers par niveau de risque… »
        if (mentionsRisk)
            return new[] { DossierHealth() };

        // « Quels dossiers n'ont pas eu d'écriture depuis un mois ? »
        if (mentionsInactive)
            return new[] { DossierHealth() };

        // « Quels dossiers ont des déclarations TVA en brouillon ? » (liste de dossiers, pas un compte)
        if (mentionsVatDraft && !mentionsCount)
            return new[] { DossierHealth() };

        // ── 3. Charge des collaborateurs → workload ──

        // « Comment se répartit la charge… », « qui est le plus chargé… », « qui suit le plus de dossiers… »
        if (mentionsWorkload)
            return new[] { Workload() };

        // « Combien d'échéances n'ont pas de responsable désigné ? »
        if (mentionsUnassigned)
            return new[] { Workload() };

        // ── 4. Agrégats de portefeuille → overview (jamais la liste plafonnée) ──

        // « Combien de dossiers ont une échéance dans les 7 prochains jours ? » : agrégat dossiersEcheanceSous7Jours,
        // dérivé de l'overview — la liste deadlines(within_days=7) est plafonnée à top_n et mélange les retards.
        if (mentionsWithin7 && mentionsCount && !mentionsOverdue)
            return new[] { Overview() };

        // « Combien de déclarations TVA sont encore en brouillon ? »
        if (mentionsVatDraft && mentionsCount)
            return new[] { Overview() };

        if (mentionsOverdue)
        {
            // « Quelles échéances sont en retard et chez quels clients ? » → liste détaillée.
            if (mentionsList)
                return new[] { Deadlines(onlyOverdue: true) };

            // « Combien d'échéances en retard, pour quel montant, et chez combien de clients ? » → agrégats
            // (echeancesEnRetard, montantEnRetard, dossiersAvecRetard), PAS la liste seule.
            if (mentionsCount || mentionsAmount || mentionsClientsCount)
                return new[] { Overview() };

            // Mention de retard sans autre précision : liste des retards, la plus actionnable.
            return new[] { Deadlines(onlyOverdue: true) };
        }

        // ── 5. Échéances à venir, avec fenêtre explicite → deadlines(within_days=…) ──

        // « Quelles échéances tombent cette semaine ? »
        if (mentionsWithin7)
            return new[] { Deadlines(withinDays: 7) };

        // « Quelles échéances arrivent dans les 30 prochains jours ? »
        if (mentionsWithin30)
            return new[] { Deadlines(withinDays: 30) };

        // « Quelles obligations de TVA trimestrielle arrivent dans les 15 prochains jours ? »
        if (mentionsVat && mentionsWithin15)
            return new[] { Deadlines(withinDays: 15, obligationType: "QuarterlyVat") };

        // ── 6. Vue d'ensemble générique ──

        // « Où en est le portefeuille du cabinet aujourd'hui ? »
        if (mentionsPortfolio)
            return new[] { Overview() };

        // Aucun mot-clé métier reconnu : liste vide, le gate 1.3 (secours + repli honnête) prend le relais.
        return Array.Empty<PlannedToolCall>();
    }

    private static PlannedToolCall Overview() => new(FirmAgentTools.PortfolioOverview);

    private static PlannedToolCall DossierHealth() => new(FirmAgentTools.DossierHealth);

    private static PlannedToolCall Workload() => new(FirmAgentTools.CollaboratorWorkload);

    private static PlannedToolCall Deadlines(bool onlyOverdue = false, int? withinDays = null, string? obligationType = null)
    {
        var args = new Dictionary<string, object?>();
        if (onlyOverdue)
            args["only_overdue"] = true;
        if (withinDays is { } days)
            args["within_days"] = days;
        if (obligationType is not null)
            args["obligation_type"] = obligationType;
        return new PlannedToolCall(FirmAgentTools.FiscalDeadlines, args);
    }

    // ── Helpers de normalisation exposés en internal (réutilisés par le grounding gate du handler,
    // SendChatMessageCommand.FirmTurnNeedsData) pour une détection cohérente des mots isolés. ──

    /// <summary>
    /// Vrai si <paramref name="word"/> apparaît dans <paramref name="text"/> comme mot isolé, c.-à-d.
    /// borné par des caractères non alphanumériques (ou le début/la fin de la chaîne). Exposé en
    /// <c>public static</c> pour réutilisation par <see cref="AiToolIntentRouter"/> (détection de « ca »)
    /// et <c>SendChatMessageHandler.FirmTurnNeedsData</c> — source unique de vérité.
    /// </summary>
    public static bool ContainsWholeWord(string text, string word)
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

    internal static string RemoveDiacritics(string text)
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
