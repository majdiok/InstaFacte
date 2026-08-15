using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.Treasury;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Treasury;

/// <summary>
/// Lecture de la réponse du modèle. Les petits modèles encadrent volontiers leur JSON de prose ou
/// de blocs de code, et écrivent des nombres là où l'on attend des entiers — le produit a déjà été
/// mordu par un <c>19.0</c> sur l'import de facture. Le parsing doit encaisser tout cela sans
/// jamais lever, et retourner <c>null</c> quand rien d'exploitable ne subsiste.
/// </summary>
public sealed class CashFlowAiAdvisorParsingTests
{
    private const string ValidPayload = """
        {
          "scenarioProbabilities": { "optimistic": 25, "realistic": 55, "pessimistic": 20 },
          "scenarioRationale": { "optimistic": "Carnet ferme", "realistic": "Tendance stable", "pessimistic": "Risque client" },
          "alerts": [ { "severity": "critical", "title": "Rupture en août", "detail": "Solde négatif", "periodStart": "2026-08-01" } ],
          "drivers": [ { "label": "Saisonnalité", "detail": "Ramadan", "impact": "high", "direction": "down" } ],
          "recommendations": [ { "title": "Relancer les impayés", "detail": "Trois factures à plus de 90 jours" } ]
        }
        """;

    [Fact]
    public void Parse_ReadsAValidPayload()
    {
        var analysis = CashFlowAiAdvisor.Parse(ValidPayload);

        Assert.NotNull(analysis);
        Assert.Equal(25m, analysis!.ScenarioProbabilities[CashFlowScenarioKind.Optimistic]);
        Assert.Equal(55m, analysis.ScenarioProbabilities[CashFlowScenarioKind.Realistic]);
        Assert.Equal(20m, analysis.ScenarioProbabilities[CashFlowScenarioKind.Pessimistic]);
        Assert.Equal("Carnet ferme", analysis.ScenarioRationales[CashFlowScenarioKind.Optimistic]);
    }

    [Fact]
    public void Parse_MapsAlertsDriversAndRecommendations()
    {
        var analysis = CashFlowAiAdvisor.Parse(ValidPayload)!;

        var alert = Assert.Single(analysis.Alerts);
        Assert.Equal(CashFlowInsightSeverity.Critical, alert.Severity);
        Assert.Equal(new DateTime(2026, 8, 1), alert.PeriodStart);

        var driver = Assert.Single(analysis.Drivers);
        Assert.Equal(CashFlowImpactLevel.High, driver.Impact);
        Assert.Equal(CashFlowDirection.Outflow, driver.ImpactDirection);

        Assert.Single(analysis.Recommendations);
    }

    [Fact]
    public void Parse_TolerAtesProseAroundTheJson()
    {
        var raw = "Voici mon analyse :\n```json\n" + ValidPayload + "\n```\nJ'espère que cela aide.";

        var analysis = CashFlowAiAdvisor.Parse(raw);

        Assert.NotNull(analysis);
        Assert.Equal(3, analysis!.ScenarioProbabilities.Count);
    }

    [Fact]
    public void Parse_TolerAtesDecimalsWhereIntegersWereExpected()
    {
        var raw = """
            { "scenarioProbabilities": { "optimistic": 25.0, "realistic": 55.5, "pessimistic": 19.5 } }
            """;

        var analysis = CashFlowAiAdvisor.Parse(raw);

        Assert.NotNull(analysis);
        Assert.Equal(55.5m, analysis!.ScenarioProbabilities[CashFlowScenarioKind.Realistic]);
    }

    [Fact]
    public void Parse_AcceptsAPartialSetOfScenarios()
    {
        // Le parsing reste permissif ; c'est l'applicateur de garde-fous qui rejette ensuite une
        // répartition incomplète. Séparer les deux évite qu'une réponse tronquée fasse échouer
        // toute l'analyse, alertes comprises.
        var raw = """{ "scenarioProbabilities": { "realistic": 60 } }""";

        var analysis = CashFlowAiAdvisor.Parse(raw);

        Assert.NotNull(analysis);
        Assert.Single(analysis!.ScenarioProbabilities);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Je ne peux pas répondre.")]
    [InlineData("{ }")]
    [InlineData("{ \"scenarioProbabilities\": null }")]
    public void Parse_ReturnsNullWhenNothingUsable(string raw)
    {
        Assert.Null(CashFlowAiAdvisor.Parse(raw));
    }

    [Fact]
    public void Parse_MalformedJson_ReturnsNullInsteadOfThrowing()
    {
        var raw = """{ "scenarioProbabilities": { "optimistic": 25, """;

        var exception = Record.Exception(() => CashFlowAiAdvisor.Parse(raw));

        Assert.Null(exception);
    }

    [Fact]
    public void Parse_DropsInsightsWithoutATitle()
    {
        var raw = """
            {
              "scenarioProbabilities": { "optimistic": 25, "realistic": 55, "pessimistic": 20 },
              "alerts": [ { "severity": "warning", "detail": "sans titre" }, { "title": "Vraie alerte" } ]
            }
            """;

        var analysis = CashFlowAiAdvisor.Parse(raw)!;

        var alert = Assert.Single(analysis.Alerts);
        Assert.Equal("Vraie alerte", alert.Title);
    }

    [Fact]
    public void Parse_UnknownSeverityFallsBackToInfo()
    {
        var raw = """
            {
              "scenarioProbabilities": { "optimistic": 25, "realistic": 55, "pessimistic": 20 },
              "alerts": [ { "severity": "catastrophique", "title": "Alerte" } ]
            }
            """;

        var analysis = CashFlowAiAdvisor.Parse(raw)!;

        Assert.Equal(CashFlowInsightSeverity.Info, analysis.Alerts[0].Severity);
    }

    [Fact]
    public void Parse_UnparseableDateIsDropped()
    {
        var raw = """
            {
              "scenarioProbabilities": { "optimistic": 25, "realistic": 55, "pessimistic": 20 },
              "alerts": [ { "title": "Alerte", "periodStart": "bientôt" } ]
            }
            """;

        var analysis = CashFlowAiAdvisor.Parse(raw)!;

        Assert.Null(analysis.Alerts[0].PeriodStart);
    }

    [Theory]
    [InlineData("texte { \"a\": 1 } suite", "{ \"a\": 1 }")]
    [InlineData("{\"a\":1}", "{\"a\":1}")]
    [InlineData("aucun objet", null)]
    [InlineData("", null)]
    public void ExtractJsonObject_IsolatesTheFirstObject(string raw, string? expected)
    {
        Assert.Equal(expected, CashFlowAiAdvisor.ExtractJsonObject(raw));
    }
}
