using System.Text.Json;
using FactuTrust.Application.Configuration;

namespace FactuTrust.Application.Features.AI;

/// <summary>Deterministic post-processing for screen analysis responses.</summary>
public static class AiScreenAnalysisPostProcessor
{
    public static string BuildDefaultFollowUpPromptsJson(string? screenId)
    {
        var prompts = screenId switch
        {
            "accounting-income-statement" => new[]
            {
                "Quels postes de charges expliquent le plus la marge négative ?",
                "Compare la performance N vs N-1 par grande catégorie.",
                "Quelles écritures contrôler en priorité ?"
            },
            "accounting-ledger" => new[]
            {
                "Quelles écritures sont les plus atypiques sur la période ?",
                "Le solde final est-il cohérent avec la balance ?",
                "Y a-t-il des doublons ou montants isolés élevés ?"
            },
            "cash-desk" => new[]
            {
                "Quels modes de paiement concentrent le plus de flux ?",
                "Y a-t-il des opérations en attente ou annulées à surveiller ?",
                "Compare ce mois au mois précédent."
            },
            "dashboard" => new[]
            {
                "Quels clients représentent le plus de risque d'impayé ?",
                "Quels produits tirent le CA ce mois-ci ?",
                "Quelles alertes stock ou livraison traiter en priorité ?"
            },
            _ => new[]
            {
                "Quels sont les 3 principaux risques à traiter cette semaine ?",
                "Quelles actions concrètes recommandes-tu en priorité ?",
                "Quelles données manquent pour affiner l'analyse ?"
            }
        };

        return JsonSerializer.Serialize(prompts);
    }

    public static string? TryBuildDashboardFromSnapshot(string? analysisSummary, string? screenId, ScreenAnalysisOptions options)
    {
        if (string.IsNullOrWhiteSpace(analysisSummary) || string.IsNullOrWhiteSpace(screenId))
            return null;

        if (!AiScreenAnalysisPromptBuilder.ShouldForceDashboard(screenId, options))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(analysisSummary);
            if (!doc.RootElement.TryGetProperty("payload", out var payload))
                return null;

            var kpis = ExtractKpis(payload, screenId);
            if (kpis.Count == 0)
                return null;

            var sections = kpis.Select(k => new
            {
                type = "kpi_card",
                title = k.Label,
                data = new { value = k.Value, unit = k.Unit }
            }).ToList();

            var dashboard = new
            {
                // Réutilise le formateur de titres existant (« Analyse — Compte de résultat »
                // au lieu du screenId brut affiché en MAJUSCULES par la carte).
                title = AiScreenTitleFormatter.BuildScreenAnalysisConversationTitle(screenId),
                sections
            };

            return JsonSerializer.Serialize(dashboard);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Filet TEXTE symétrique de <see cref="TryBuildDashboardFromSnapshot"/> : sections FR factuelles
    /// construites déterministiquement depuis le snapshot quand ni le modèle ni la synthèse forcée
    /// n'ont livré de prose. Zéro appel LLM. Null si le snapshot est absent/imparsable/vide —
    /// s'applique à TOUS les écrans (pas de gate ShouldForceDashboard : le texte vaut partout).
    /// </summary>
    public static string? TryBuildAnalysisTextFromSnapshot(string? analysisSummary, string? screenId, ScreenAnalysisOptions options)
    {
        if (string.IsNullOrWhiteSpace(analysisSummary))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(analysisSummary);
            if (!doc.RootElement.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
                return null;

            var kpis = ExtractKpis(payload, screenId ?? "");
            var highlights = ExtractHighlights(payload);
            if (kpis.Count == 0 && highlights.Count == 0)
                return null;

            var title = string.IsNullOrWhiteSpace(screenId)
                ? "l'écran analysé"
                : AiScreenTitleFormatter.BuildScreenAnalysisConversationTitle(screenId);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("## Synthèse");
            sb.Append("Résumé factuel généré à partir des données affichées (").Append(title).Append("). ");
            var leadKpis = kpis.Take(3)
                .Select(k => $"{k.Label} : {FormatKpiValue(k)}")
                .ToList();
            if (leadKpis.Count > 0)
                sb.Append(string.Join(" ; ", leadKpis)).Append('.');
            sb.AppendLine();

            if (kpis.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Indicateurs clés");
                foreach (var kpi in kpis)
                    sb.AppendLine($"- **{kpi.Label}** : {FormatKpiValue(kpi)}");
            }

            if (highlights.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Points d'attention");
                foreach (var (label, detail) in highlights)
                    sb.AppendLine(string.IsNullOrWhiteSpace(detail) ? $"- **{label}**" : $"- **{label}** : {detail}");
            }

            sb.AppendLine();
            sb.AppendLine("## Points à vérifier");
            sb.AppendLine("- Résumé automatique généré sans interprétation par l'IA — relancez l'analyse pour une lecture d'expert.");
            sb.AppendLine("- Vérifiez les montants dans les écrans officiels avant toute décision.");
            foreach (var warning in ExtractDataQualityWarnings(payload))
                sb.AppendLine($"- {warning}");

            return sb.ToString().TrimEnd();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Montants TND en N3 fr-FR (« 93 340,000 TND ») ; % avec virgule ; sinon valeur brute.</summary>
    private static string FormatKpiValue(KpiEntry kpi)
    {
        if (kpi.Unit == "TND"
            && decimal.TryParse(kpi.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var amount))
        {
            return amount.ToString("N3", System.Globalization.CultureInfo.GetCultureInfo("fr-FR")) + " TND";
        }
        if (kpi.Unit == "%")
            return kpi.Value.Replace('.', ',') + " %";
        return kpi.Value;
    }

    /// <summary>Points d'attention du snapshot (payload.highlights : { label, context, value }), cap 5.</summary>
    private static List<(string Label, string? Detail)> ExtractHighlights(JsonElement payload)
    {
        var result = new List<(string, string?)>();
        if (!payload.TryGetProperty("highlights", out var highlights) || highlights.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var item in highlights.EnumerateArray())
        {
            if (result.Count >= 5)
                break;
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            var label = item.TryGetProperty("label", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() : null;
            if (string.IsNullOrWhiteSpace(label))
                continue;
            var detail = item.TryGetProperty("context", out var c) && c.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(c.GetString())
                ? c.GetString()
                : item.TryGetProperty("value", out var v) && v.ValueKind is JsonValueKind.String or JsonValueKind.Number
                    ? v.ToString()
                    : null;
            result.Add((label!, detail));
        }
        return result;
    }

    private static IEnumerable<string> ExtractDataQualityWarnings(JsonElement payload)
    {
        if (!payload.TryGetProperty("dataQuality", out var quality) || quality.ValueKind != JsonValueKind.Object)
            yield break;
        if (!quality.TryGetProperty("warnings", out var warnings) || warnings.ValueKind != JsonValueKind.Array)
            yield break;
        foreach (var w in warnings.EnumerateArray())
        {
            if (w.ValueKind != JsonValueKind.String)
                continue;
            var text = w.GetString();
            if (!string.IsNullOrWhiteSpace(text))
                yield return text!;
        }
    }

    private sealed record KpiEntry(string Label, string Value, string Unit);

    /// <summary>
    /// Libellés FR des clés métriques des snapshots (les clés camelCase brutes s'affichaient telles
    /// quelles en MAJUSCULES dans les KPI cards). Clé inconnue → camelCase éclaté capitalisé,
    /// jamais la clé brute. Couvre les summaries des goldens (docs/ai-screen-analysis/golden).
    /// </summary>
    private static readonly Dictionary<string, string> MetricFrenchLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["totalRevenue"] = "Revenus totaux",
        ["totalExpenses"] = "Charges totales",
        ["costOfGoodsSold"] = "Coût des marchandises vendues",
        ["grossMargin"] = "Marge brute",
        ["grossMarginPct"] = "Marge brute (%)",
        ["netResult"] = "Résultat net",
        ["totalRows"] = "Nombre de lignes",
        ["totalDebit"] = "Total débit",
        ["totalCredit"] = "Total crédit",
        ["finalBalance"] = "Solde final",
        ["debitCreditGap"] = "Écart débit/crédit",
        ["primaryTotal"] = "Solde caisse primaire",
        ["primaryCreditsTotal"] = "Encaissements caisse primaire",
        ["primaryDebitsTotal"] = "Décaissements caisse primaire",
        ["netPrimary"] = "Net caisse primaire",
        ["totalOutstanding"] = "Total créances clients",
        ["totalOverdue"] = "Créances en retard",
        ["criticalInvoiceCount"] = "Factures critiques",
        ["topClientExposure"] = "Exposition du client principal",
        ["topClientName"] = "Client le plus exposé",
        // Bilan (payload legacy wrapLegacyAnalyzePayload) — sans ces libellés : « Total assets », « Fiscal year ».
        ["totalAssets"] = "Total actif",
        ["totalLiabilities"] = "Total passif",
        ["fiscalYear"] = "Exercice fiscal"
    };

    private static string DescribeMetric(string key) =>
        MetricFrenchLabels.TryGetValue(key, out var label) ? label : PrettifyMetricKey(key);

    /// <summary>« averageBasketValue » → « Average basket value » — jamais la clé brute en MAJUSCULES.</summary>
    private static string PrettifyMetricKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return key;
        var sb = new System.Text.StringBuilder(key.Length + 8);
        sb.Append(char.ToUpperInvariant(key[0]));
        for (var i = 1; i < key.Length; i++)
        {
            var c = key[i];
            if (char.IsUpper(c))
            {
                sb.Append(' ');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c == '_' ? ' ' : c);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Unité d'un KPI déduite de sa CLÉ d'origine (l'ancien code codait « TND » en dur, affichant
    /// « -17,43 TND » pour un pourcentage) : *Pct/Percent/Rate → % ; compteurs/valeurs non numériques →
    /// aucune unité ; sinon TND.
    /// </summary>
    private static string InferUnit(string key, string value)
    {
        if (key.EndsWith("Pct", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith("Percent", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith("Rate", StringComparison.OrdinalIgnoreCase))
            return "%";
        if (key.EndsWith("Count", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith("Rows", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith("Name", StringComparison.OrdinalIgnoreCase)
            // Années (fiscalYear…) : « Exercice fiscal : 2026 TND » n'a aucun sens.
            || key.EndsWith("Year", StringComparison.OrdinalIgnoreCase))
            return "";
        return decimal.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _)
            ? "TND"
            : "";
    }

    private static List<KpiEntry> ExtractKpis(JsonElement payload, string screenId)
    {
        var entries = new List<KpiEntry>();
        var seenLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string key, JsonElement value, string? explicitLabel = null)
        {
            if (value.ValueKind is not (JsonValueKind.Number or JsonValueKind.String))
                return;
            var label = explicitLabel ?? DescribeMetric(key);
            if (!seenLabels.Add(label))
                return; // dédup : le summary générique et les ajouts spécifiques produisent le même libellé
            var raw = value.ToString() ?? "";
            entries.Add(new KpiEntry(label, raw, InferUnit(key, raw)));
        }

        if (payload.TryGetProperty("summary", out var summary) && summary.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in summary.EnumerateObject())
                Add(prop.Name, prop.Value);
        }

        if (screenId == "accounting-income-statement")
        {
            AddIfPresent(payload, "totalRevenue", Add);
            AddIfPresent(payload, "totalExpenses", Add);
            AddIfPresent(payload, "netResult", Add);
        }

        if (screenId == "invoice-list")
        {
            AddIfPresent(payload, "totalOutstanding", Add);
            AddIfPresent(payload, "totalOverdue", Add);
            AddIfPresent(payload, "topClientExposure", Add);
            AddIfPresent(payload, "criticalInvoiceCount", Add);
        }

        return entries;
    }

    private static void AddIfPresent(JsonElement payload, string field, Action<string, JsonElement, string?> add)
    {
        if (payload.TryGetProperty(field, out var val))
            add(field, val, null);
    }
}
