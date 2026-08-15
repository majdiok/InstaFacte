using System.Text.Json;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.Json;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Features.Accounting.Audit.Narrative;

/// <summary>Note assemblée, prête à persister.</summary>
public sealed record RevisionDossierAssembly(
    string ExecutiveSummary,
    IReadOnlyList<FirmRevisionNoteItemDto> Items,
    decimal TotalImpactAmount,
    int AnomalyCount,
    int BlockingCount,
    bool AiGenerated,
    string? FallbackReason);

/// <summary>
/// Assemble le dossier de révision en fusionnant les <b>faits déterministes</b> et la
/// <b>rédaction</b> du modèle — la seconde ne pouvant jamais altérer les premiers.
///
/// <para>Les six garde-fous, chacun couvert par un test :</para>
/// <list type="number">
///   <item>Une référence d'anomalie inconnue fait rejeter l'entrée : le modèle ne peut pas ajouter
///         d'anomalie au dossier.</item>
///   <item>Une action hors de l'ensemble fermé fait retomber sur l'action déterministe de la règle.</item>
///   <item>Sévérité, montant, compte et pièce sont réinjectés depuis les faits, jamais lus de la
///         réponse.</item>
///   <item>L'impact chiffré est calculé via <see cref="AuditAmountSemantics"/> : les règles dont le
///         montant n'est pas un enjeu financier restent « non chiffrables ».</item>
///   <item>Une anomalie que le modèle n'a pas couverte conserve sa description déterministe — le
///         dossier est complet même partiellement rédigé.</item>
///   <item>Sans réponse exploitable, l'assemblage est intégralement déterministe et
///         <c>AiGenerated</c> vaut faux. <b>Un dossier n'échoue jamais à cause du modèle.</b></item>
/// </list>
/// </summary>
public static class RevisionDossierGuard
{
    /// <summary>Longueur maximale d'un texte rédigé. Au-delà, il est tronqué proprement.</summary>
    private const int MaxNoteLength = 1500;

    private const int MaxSummaryLength = 3000;

    /// <summary>Assemblage purement déterministe : aucun appel de modèle n'a eu lieu ou il a échoué.</summary>
    public static RevisionDossierAssembly BuildDeterministic(
        IReadOnlyList<RevisionAnomalyFacts> anomalies,
        string? fallbackReason = null) =>
        Assemble(anomalies, response: null, fallbackReason);

    /// <summary>
    /// Assemblage augmenté de la rédaction du modèle. Une réponse nulle, vide ou intégralement
    /// rejetée retombe sur le déterministe.
    /// </summary>
    public static RevisionDossierAssembly Assemble(
        IReadOnlyList<RevisionAnomalyFacts> anomalies,
        RevisionDossierLlmResponse? response,
        string? fallbackReason = null)
    {
        var byRef = anomalies.ToDictionary(a => a.Id.ToString("N"), StringComparer.OrdinalIgnoreCase);
        var written = new Dictionary<Guid, RevisionDossierLlmItem>();

        // ── Garde-fou 1 : toute référence inconnue est écartée ──────────────────────────────
        foreach (var item in response?.Items ?? [])
        {
            if (string.IsNullOrWhiteSpace(item.AnomalyRef)) continue;
            if (!byRef.TryGetValue(item.AnomalyRef.Trim(), out var facts)) continue;

            // Première rédaction retenue : un doublon n'écrase pas la précédente.
            written.TryAdd(facts.Id, item);
        }

        var items = new List<FirmRevisionNoteItemDto>(anomalies.Count);

        foreach (var facts in anomalies)
        {
            written.TryGetValue(facts.Id, out var narrative);

            // ── Garde-fou 5 : sans rédaction, la description déterministe fait office de note ──
            var workingNote = Sanitize(narrative?.WorkingNote, MaxNoteLength);
            if (string.IsNullOrWhiteSpace(workingNote))
                workingNote = Sanitize(facts.Description, MaxNoteLength) ?? facts.Title;

            // ── Garde-fou 2 : action hors ensemble fermé ⇒ action déterministe de la règle ────
            var action = RevisionAction.IsValid(narrative?.Action)
                ? narrative!.Action!
                : RevisionAction.DefaultFor(facts.RuleCode);

            items.Add(new FirmRevisionNoteItemDto
            {
                AnomalyId = facts.Id,
                RuleCode = facts.RuleCode,
                ModuleCode = facts.ModuleCode,

                // ── Garde-fou 3 : les faits priment, toujours ─────────────────────────────────
                Severity = facts.Severity,
                Title = facts.Title,
                AccountRef = facts.AccountRef,
                PieceRef = facts.PieceRef,

                // ── Garde-fou 4 : impact chiffré seulement quand il a un sens ─────────────────
                ImpactAmount = AuditAmountSemantics.ImpactOf(facts.RuleCode, facts.Amount),

                WorkingNote = workingNote!,
                ClientQuestion = Sanitize(narrative?.ClientQuestion, MaxNoteLength),
                Action = action,
                ActionLabel = RevisionAction.LabelFor(action)
            });
        }

        var summary = Sanitize(response?.ExecutiveSummary, MaxSummaryLength)
                      ?? BuildDeterministicSummary(anomalies);

        // ── Garde-fou 6 : « rédigé par le modèle » n'est vrai que si quelque chose a survécu ──
        var aiGenerated = written.Count > 0
                          || !string.IsNullOrWhiteSpace(Sanitize(response?.ExecutiveSummary, MaxSummaryLength));

        var reason = fallbackReason;
        if (reason is null && response is not null && !aiGenerated)
            reason = "Réponse du modèle inexploitable : dossier rédigé à partir des seuls constats.";

        return new RevisionDossierAssembly(
            summary,
            items,
            MillimeRounding.Round(AuditAmountSemantics.SumImpacts(
                anomalies.Select(a => (a.RuleCode, a.Amount)))),
            anomalies.Count,
            anomalies.Count(a => a.Severity == 2),
            aiGenerated,
            reason);
    }

    /// <summary>
    /// Lit la réponse brute du modèle. Tolérante par construction : prose autour du JSON, virgules
    /// traînantes, types approximatifs. Rend <c>null</c> si rien d'exploitable — l'appelant retombe
    /// alors sur le déterministe.
    /// </summary>
    public static RevisionDossierLlmResponse? TryParse(string? rawContent, out string? failureReason)
    {
        failureReason = null;

        if (string.IsNullOrWhiteSpace(rawContent))
        {
            failureReason = "Le modèle n'a rien renvoyé.";
            return null;
        }

        var json = InvoiceImportParsing.ExtractFirstJsonObject(rawContent);
        if (string.IsNullOrWhiteSpace(json))
        {
            failureReason = "Aucun objet JSON trouvé dans la réponse du modèle.";
            return null;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<RevisionDossierLlmResponse>(json, LlmJsonOptions.Tolerant);
            if (parsed is null)
            {
                failureReason = "Réponse du modèle vide après lecture.";
                return null;
            }
            return parsed;
        }
        catch (JsonException ex)
        {
            failureReason = $"Réponse du modèle illisible : {LlmJsonDiagnostics.Describe(json, ex)}";
            return null;
        }
    }

    /// <summary>Synthèse de repli, construite des seuls compteurs. Toujours exacte, jamais élégante.</summary>
    private static string BuildDeterministicSummary(IReadOnlyList<RevisionAnomalyFacts> anomalies)
    {
        if (anomalies.Count == 0)
            return "Aucune anomalie ouverte sur la période contrôlée.";

        var blocking = anomalies.Count(a => a.Severity == 2);
        var warning = anomalies.Count(a => a.Severity == 1);
        var info = anomalies.Count - blocking - warning;

        var parts = new List<string>();
        if (blocking > 0) parts.Add($"{blocking} point(s) bloquant(s)");
        if (warning > 0) parts.Add($"{warning} avertissement(s)");
        if (info > 0) parts.Add($"{info} information(s)");

        var domains = anomalies
            .GroupBy(a => a.ModuleCode, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => AccountingAuditModuleLabels.LabelFor(g.Key));

        return $"Le contrôle relève {string.Join(", ", parts)}. " +
               $"Domaines les plus concernés : {string.Join(", ", domains)}. " +
               "Les points bloquants sont à traiter avant toute clôture.";
    }

    /// <summary>
    /// Normalise un texte rédigé : espaces réduits, longueur bornée, chaîne vide ramenée à null.
    /// La troncature se fait sur une frontière de mot — couper au milieu d'un mot dans une note de
    /// travail donne une impression de bug.
    /// </summary>
    private static string? Sanitize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var cleaned = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        while (cleaned.Contains("  ", StringComparison.Ordinal))
            cleaned = cleaned.Replace("  ", " ", StringComparison.Ordinal);

        if (cleaned.Length <= maxLength) return cleaned;

        var cut = cleaned.LastIndexOf(' ', maxLength - 1);
        if (cut < maxLength / 2) cut = maxLength - 1;
        return cleaned[..cut].TrimEnd() + "…";
    }
}
