using System.Text.Json;
using FactuTrust.Application.Features.AI.Json;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Le diagnostic doit pointer le champ fautif. En production, l'aperçu tronqué à 500 caractères
/// s'arrêtait systématiquement avant lui, rendant les journaux inutilisables pour cette panne.
/// </summary>
public sealed class LlmJsonDiagnosticsTests
{
    /// <summary>Reproduit la forme réelle : la faute est loin après les 500 premiers caractères.</summary>
    private static string BuildPayloadWithLateFault(string preamble)
    {
        var padding = string.Join("\n", Enumerable.Range(0, 30)
            .Select(i => $"  \"remplissage{i}\": \"{preamble} valeur de bourrage {i}\","));

        return "{\n" + padding + "\n  \"vatRatePercent\": 19.0\n}";
    }

    private static JsonException CaptureError(string json)
    {
        // PropertyNameCaseInsensitive est indispensable : sans lui « vatRatePercent » ne serait pas
        // rapproché de VatRatePercent, la propriété serait ignorée et rien ne lèverait.
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<Probe>(json, options));
    }

    private sealed class Probe
    {
        public int? VatRatePercent { get; set; }
    }

    [Fact]
    public void Describe_PointsAtTheOffendingField()
    {
        var json = BuildPayloadWithLateFault("Societe Generale");
        var error = CaptureError(json);

        var description = LlmJsonDiagnostics.Describe(json, error);

        Assert.Contains("path=$.vatRatePercent", description);
        Assert.Contains("vatRatePercent", description);
        Assert.Contains("19.0", description);
    }

    /// <summary>
    /// LineNumber et BytePositionInLine sont en OCTETS. Avec des accents avant la faute, un
    /// découpage par index de chaîne viserait à côté — ce test verrouille le traitement en UTF-8.
    /// </summary>
    [Fact]
    public void Describe_WithAccentedContentBefore_StillReachesTheField()
    {
        var json = BuildPayloadWithLateFault("Société Générale n° 68 — éàüç");
        var error = CaptureError(json);

        var description = LlmJsonDiagnostics.Describe(json, error);

        Assert.Contains("vatRatePercent", description);
        Assert.Contains("19.0", description);
    }

    [Fact]
    public void Describe_WithoutException_ReportsTruncatedResponse()
    {
        var description = LlmJsonDiagnostics.Describe("{\"a\":1", error: null);

        Assert.Contains("aucun objet JSON équilibré", description);
    }

    [Fact]
    public void Excerpt_WithoutPosition_ReturnsNull()
    {
        Assert.Null(LlmJsonDiagnostics.Excerpt("{\"a\":1}", lineNumber: null, bytePositionInLine: 5, window: 240));
        Assert.Null(LlmJsonDiagnostics.Excerpt(null, lineNumber: 1, bytePositionInLine: 5, window: 240));
        Assert.Null(LlmJsonDiagnostics.Excerpt("", lineNumber: 1, bytePositionInLine: 5, window: 240));
    }

    [Fact]
    public void Excerpt_IsBounded_EvenWithAbsurdWindow()
    {
        var json = new string('x', 10_000);

        var excerpt = LlmJsonDiagnostics.Excerpt(json, lineNumber: 0, bytePositionInLine: 5_000, window: 1_000_000);

        Assert.NotNull(excerpt);
        Assert.True(excerpt!.Length <= 2_000, $"L'extrait doit rester borné, mesuré : {excerpt.Length}");
    }

    [Fact]
    public void Excerpt_CollapsesNewlines_ForSingleLineLogs()
    {
        var excerpt = LlmJsonDiagnostics.Excerpt("{\n  \"a\": 1\n}", lineNumber: 1, bytePositionInLine: 2, window: 240);

        Assert.NotNull(excerpt);
        Assert.DoesNotContain('\n', excerpt!);
    }
}
