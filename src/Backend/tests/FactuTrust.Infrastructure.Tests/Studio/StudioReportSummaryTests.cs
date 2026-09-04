using FactuTrust.Application.Features.AI;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Un état qui a RÉUSSI ne doit jamais produire une phrase d'excuse.
///
/// Le défaut corrigé : le raccourci calculait l'état, le tableau partait au navigateur, puis le
/// modèle (un modèle de code, qui ne rend presque rien) restait sous le seuil de 80 caractères.
/// Le repli déterministe prenait alors la main, tentait d'humaniser le dernier message d'outil —
/// et échouait, parce que <c>rows</c> est imbriqué sous <c>result</c> et qu'aucune clé racine ne
/// correspondait aux formes connues. L'utilisateur lisait « Je n'ai pas pu formuler une réponse
/// complète à partir des données récupérées » juste au-dessus de son tableau.
/// </summary>
public sealed class StudioReportSummaryTests
{
    private const string ApologyWithTools =
        "Je n'ai pas pu formuler une réponse complète à partir des données récupérées.";

    private static string Payload(string rows, int totalRows, bool truncated) => $$"""
        {
          "success": true,
          "title": "Ventes par mois",
          "source": "InvoiceLines",
          "sourceLabel": "Lignes de facture de vente",
          "preset": "ventes_par_mois",
          "period": { "from": "2026-01-01", "to": "2026-09-03" },
          "result": {
            "columns": [
              { "key": "Invoices_IssueDate__month", "label": "Issue Date (mois)", "kind": "dimension" },
              { "key": "sum_InvoiceLines_Total", "label": "Somme de Total", "kind": "measure" }
            ],
            "rows": [{{rows}}],
            "totalRows": {{totalRows}},
            "truncated": {{(truncated ? "true" : "false")}}
          },
          "warnings": [],
          "message": "{{totalRows}} ligne(s) de résultat."
        }
        """;

    private const string TwoRows =
        """{ "Invoices_IssueDate__month": "2026-07", "sum_InvoiceLines_Total": 1000.5 },""" +
        """{ "Invoices_IssueDate__month": "2026-08", "sum_InvoiceLines_Total": 2000.25 }""";

    private static string Build(string payload)
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "t");
        conversation.AddMessage(MessageRole.Tool, payload, "studio_run_report", "call-1");
        return AssistantDeterministicFallback.TryBuild(
            conversation,
            "créer moi un rapport détaillé de chiffre d'affaires par mois",
            minMeaningfulChars: 80,
            enabled: true,
            toolsExecutedThisRequest: true,
            toolIntent: AiToolIntentRouter.AiToolIntent.Fallback);
    }

    [Fact]
    public void A_successful_report_produces_a_real_summary_never_an_apology()
    {
        var summary = Build(Payload(TwoRows, 2, truncated: false));

        Assert.DoesNotContain(ApologyWithTools, summary, StringComparison.Ordinal);
        Assert.Contains("Ventes par mois", summary, StringComparison.Ordinal);
        Assert.Contains("Lignes de facture de vente", summary, StringComparison.Ordinal);
        // Le total d'une mesure est nommé par son libellé, pas par sa clé technique.
        Assert.Contains("Somme de Total", summary, StringComparison.Ordinal);
        // La première ligne est citée : c'est l'enseignement le plus immédiat.
        Assert.Contains("2026-07", summary, StringComparison.Ordinal);
        // Doit franchir le seuil de « réponse complète », sinon le repli reprendrait la main.
        Assert.True(summary.Length >= 80, $"Résumé trop court ({summary.Length}) : {summary}");
    }

    [Fact]
    public void The_period_is_stated_so_the_figures_are_not_floating()
    {
        var summary = Build(Payload(TwoRows, 2, truncated: false));

        Assert.Contains("01/01/2026", summary, StringComparison.Ordinal);
        Assert.Contains("03/09/2026", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void A_truncated_result_carries_no_total()
    {
        var summary = Build(Payload(TwoRows, 5000, truncated: true));

        // Un total calculé sur un extrait serait FAUX tout en ayant l'air juste.
        Assert.DoesNotContain("Somme de Total", summary, StringComparison.Ordinal);
        Assert.Contains("tronqué", summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ApologyWithTools, summary, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_result_says_so_and_is_long_enough_to_be_delivered()
    {
        var summary = Build(Payload(string.Empty, 0, truncated: false));

        Assert.Contains("aucune donnée", summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ApologyWithTools, summary, StringComparison.Ordinal);
        Assert.True(summary.Length >= 80, $"Résumé trop court ({summary.Length}) : {summary}");
    }

    [Fact]
    public void A_payload_without_a_report_envelope_is_left_to_the_other_forms()
    {
        // Non-régression : les autres outils continuent d'être humanisés par les branches existantes.
        var conversation = Conversation.Create(Guid.NewGuid(), "t");
        conversation.AddMessage(
            MessageRole.Tool,
            """{ "totalRevenue": 1500.0, "rowCount": 2, "rows": [ { "name": "Ciment", "amount": 900.0 }, { "name": "Sable", "amount": 600.0 } ] }""",
            "get_sales_revenue",
            "call-2");

        var summary = AssistantDeterministicFallback.TryBuild(
            conversation, "chiffre d'affaires", 80, true, true, AiToolIntentRouter.AiToolIntent.Fallback);

        Assert.Contains("Ciment", summary, StringComparison.Ordinal);
        Assert.DoesNotContain(ApologyWithTools, summary, StringComparison.Ordinal);
    }
}
