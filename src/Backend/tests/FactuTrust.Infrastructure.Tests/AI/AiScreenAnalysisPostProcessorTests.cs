using System.Text.Json;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Dashboard KPI déterministe construit depuis le snapshot : libellés FRANÇAIS (les clés camelCase
/// brutes s'affichaient en MAJUSCULES), unités correctes (% pour *Pct — plus jamais « -17,43 TND »),
/// pas de doublons, titre d'écran humanisé.
/// </summary>
public sealed class AiScreenAnalysisPostProcessorTests
{
    private static readonly ScreenAnalysisOptions Options = new()
    {
        ForceDashboardForScreens = ["accounting-income-statement", "invoice-list"]
    };

    private const string IncomeStatementSnapshot = """
        {
          "payload": {
            "screenId": "accounting-income-statement",
            "summary": {
              "totalRevenue": 29087,
              "totalExpenses": 34157,
              "costOfGoodsSold": 34157,
              "grossMargin": -5070,
              "grossMarginPct": -17.43,
              "netResult": -5070
            }
          }
        }
        """;

    [Fact]
    public void IncomeStatement_Kpis_AreFrench_WithCorrectUnits_NoDuplicates()
    {
        var json = AiScreenAnalysisPostProcessor.TryBuildDashboardFromSnapshot(
            IncomeStatementSnapshot, "accounting-income-statement", Options);

        Assert.NotNull(json);
        using var doc = JsonDocument.Parse(json!);
        var root = doc.RootElement;

        Assert.Equal("Analyse — Compte de résultat", root.GetProperty("title").GetString());

        var sections = root.GetProperty("sections").EnumerateArray()
            .Select(s => (
                Title: s.GetProperty("title").GetString()!,
                Unit: s.GetProperty("data").GetProperty("unit").GetString()!))
            .ToList();

        // Libellés FR — plus aucune clé camelCase brute.
        var titles = sections.Select(s => s.Title).ToList();
        Assert.Contains("Revenus totaux", titles);
        Assert.Contains("Charges totales", titles);
        Assert.Contains("Coût des marchandises vendues", titles);
        Assert.Contains("Marge brute", titles);
        Assert.Contains("Marge brute (%)", titles);
        Assert.Contains("Résultat net", titles);
        Assert.DoesNotContain(titles, t => t.Contains("totalRevenue", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(titles, t => t.Contains("grossMarginPct", StringComparison.OrdinalIgnoreCase));

        // Dédup : les ajouts spécifiques écran ne doublent plus les entrées du summary.
        Assert.Equal(titles.Count, titles.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        // Unités : % pour le pourcentage, TND pour les montants.
        Assert.Equal("%", sections.Single(s => s.Title == "Marge brute (%)").Unit);
        Assert.Equal("TND", sections.Single(s => s.Title == "Résultat net").Unit);
    }

    [Fact]
    public void UnknownMetricKey_IsPrettified_NeverRaw()
    {
        const string snapshot = """
            {
              "payload": {
                "summary": { "averageBasketValue": 123.5 }
              }
            }
            """;

        var json = AiScreenAnalysisPostProcessor.TryBuildDashboardFromSnapshot(
            snapshot, "accounting-income-statement", Options);

        Assert.NotNull(json);
        Assert.Contains("Average basket value", json!, StringComparison.Ordinal);
        Assert.DoesNotContain("averageBasketValue", json, StringComparison.Ordinal);
    }

    [Fact]
    public void CountMetric_HasNoUnit_And_NonNumericValue_HasNoUnit()
    {
        const string snapshot = """
            {
              "payload": {
                "summary": {
                  "criticalInvoiceCount": 2,
                  "topClientName": "ste bouzgarou"
                }
              }
            }
            """;

        var json = AiScreenAnalysisPostProcessor.TryBuildDashboardFromSnapshot(
            snapshot, "invoice-list", Options);

        Assert.NotNull(json);
        using var doc = JsonDocument.Parse(json!);
        var sections = doc.RootElement.GetProperty("sections").EnumerateArray().ToList();

        var count = sections.Single(s => s.GetProperty("title").GetString() == "Factures critiques");
        Assert.Equal("", count.GetProperty("data").GetProperty("unit").GetString());

        var name = sections.Single(s => s.GetProperty("title").GetString() == "Client le plus exposé");
        Assert.Equal("", name.GetProperty("data").GetProperty("unit").GetString());
    }

    [Fact]
    public void ScreenNotInForceList_ReturnsNull()
    {
        Assert.Null(AiScreenAnalysisPostProcessor.TryBuildDashboardFromSnapshot(
            IncomeStatementSnapshot, "cash-desk", Options));
    }

    // ── Filet TEXTE déterministe (TryBuildAnalysisTextFromSnapshot) ──

    [Fact]
    public void AnalysisText_IncomeStatement_BuildsFrenchSections()
    {
        var text = AiScreenAnalysisPostProcessor.TryBuildAnalysisTextFromSnapshot(
            IncomeStatementSnapshot, "accounting-income-statement", Options);

        Assert.NotNull(text);
        Assert.Contains("## Synthèse", text!);
        Assert.Contains("## Indicateurs clés", text);
        Assert.Contains("## Points à vérifier", text);
        Assert.Contains("Résumé factuel", text);
        Assert.Contains("Revenus totaux", text);
        Assert.Contains("TND", text);
        Assert.DoesNotContain("totalRevenue", text, StringComparison.Ordinal);
        // Assez de prose visible pour que le frontend n'affiche jamais le bouchon.
        Assert.True(AssistantVisibleContentFormatter.HasMeaningfulAssistantText(text, minChars: 80));
    }

    [Fact]
    public void AnalysisText_BalanceSheetLegacySummary_UsesFrenchLabels_NoTndOnYear()
    {
        const string snapshot = """
            {
              "payload": {
                "summary": {
                  "fiscalYear": 2026,
                  "totalAssets": 120000,
                  "totalLiabilities": 90000,
                  "netResult": 30000
                }
              }
            }
            """;

        var text = AiScreenAnalysisPostProcessor.TryBuildAnalysisTextFromSnapshot(
            snapshot, "accounting-balance-sheet", Options);

        Assert.NotNull(text);
        Assert.Contains("Total actif", text!);
        Assert.Contains("Total passif", text);
        // La puce indicateur de l'exercice affiche l'année brute, jamais « 2026 TND » (année ≠ montant).
        var fiscalBullet = text.Split('\n').Single(l => l.StartsWith("- **Exercice fiscal**", StringComparison.Ordinal));
        Assert.DoesNotContain("TND", fiscalBullet);
        Assert.Contains("2026", fiscalBullet);
        // Le texte s'applique même hors ForceDashboardForScreens (pas de gate dashboard).
        Assert.NotNull(AiScreenAnalysisPostProcessor.TryBuildAnalysisTextFromSnapshot(snapshot, "cash-desk", Options));
    }

    [Fact]
    public void AnalysisText_IncludesHighlights()
    {
        const string snapshot = """
            {
              "payload": {
                "summary": { "grossMargin": -5070 },
                "highlights": [
                  { "type": "risk", "label": "Marge brute négative", "context": "Les charges dépassent les revenus." }
                ],
                "dataQuality": { "warnings": ["Comparatif N-1 indisponible"] }
              }
            }
            """;

        var text = AiScreenAnalysisPostProcessor.TryBuildAnalysisTextFromSnapshot(
            snapshot, "accounting-income-statement", Options);

        Assert.NotNull(text);
        Assert.Contains("## Points d'attention", text!);
        Assert.Contains("Marge brute négative", text);
        Assert.Contains("Les charges dépassent les revenus.", text);
        Assert.Contains("Comparatif N-1 indisponible", text);
    }

    [Fact]
    public void AnalysisText_ReturnsNull_OnTruncatedOrEmptySnapshot()
    {
        // Troncature côté client : suffixe non-JSON → parse impossible → null (jamais d'exception).
        const string truncated = """{"payload":{"summary":{"totalRevenue": 29""" + "\n... [tronqué côté client]";
        Assert.Null(AiScreenAnalysisPostProcessor.TryBuildAnalysisTextFromSnapshot(
            truncated, "accounting-income-statement", Options));
        Assert.Null(AiScreenAnalysisPostProcessor.TryBuildAnalysisTextFromSnapshot(
            null, "accounting-income-statement", Options));
        Assert.Null(AiScreenAnalysisPostProcessor.TryBuildAnalysisTextFromSnapshot(
            "{}", "accounting-income-statement", Options));
        Assert.Null(AiScreenAnalysisPostProcessor.TryBuildAnalysisTextFromSnapshot(
            """{"payload":{}}""", "accounting-income-statement", Options));
    }
}
