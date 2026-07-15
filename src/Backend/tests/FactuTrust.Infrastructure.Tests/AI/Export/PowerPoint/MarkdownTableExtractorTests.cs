using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI.Export.PowerPoint;

public sealed class MarkdownTableExtractorTests
{
    [Fact]
    public void Extract_standard_table_returns_headers_and_rows()
    {
        var markdown = """
            ## Indicateurs clés

            | Indicateur | Montant (TND) | Détails |
            | :--- | :--- | :--- |
            | Total Créances | 90 151.910 | Somme restant à payer |
            | Créances en Retard | 85 472.250 | Factures échues |
            """;

        var (remaining, tables) = MarkdownTableExtractor.Extract(markdown);

        Assert.Single(tables);
        Assert.Equal(3, tables[0].Headers.Count);
        Assert.Equal(2, tables[0].Rows.Count);
        Assert.Equal("Indicateurs clés", tables[0].PrecedingSectionTitle);
        Assert.DoesNotContain("| Total Créances |", remaining);
    }

    [Fact]
    public void Extract_malformed_table_keeps_lines()
    {
        var markdown = "| Only one row |\nSome text";
        var (_, tables) = MarkdownTableExtractor.Extract(markdown);
        Assert.Empty(tables);
    }

    [Fact]
    public void Extract_empty_input_returns_empty()
    {
        var (remaining, tables) = MarkdownTableExtractor.Extract(string.Empty);
        Assert.Empty(tables);
        Assert.Equal(string.Empty, remaining);
    }
}

public sealed class MarkdownKpiHeuristicsTests
{
    [Fact]
    public void TryExtract_promotes_indicateurs_cles_table_to_kpis()
    {
        var markdown = """
            ## Indicateurs clés

            | Indicateur | Montant (TND) | Détails |
            | :--- | :--- | :--- |
            | Total Créances Clients | 90 151.910 | Somme |
            | Créances en Retard | 85 472.250 | Arriérés |
            """;

        var (_, tables) = MarkdownTableExtractor.Extract(markdown);
        var (remaining, kpis) = MarkdownKpiHeuristics.TryExtract(markdown, tables);

        Assert.Equal(2, kpis.Count);
        Assert.Equal("Total Créances Clients", kpis[0].Label);
        Assert.Equal("90 151.910", kpis[0].Value);
        Assert.DoesNotContain("|", remaining);
    }
}

public sealed class MarkdownSectionSegmenterTests
{
    [Fact]
    public void Segment_splits_on_h2_headings()
    {
        var markdown = """
            ## Synthèse exécutive
            Texte synthèse.

            ## Analyse détaillée
            Texte analyse.
            """;

        var sections = MarkdownSectionSegmenter.Segment(markdown);
        Assert.Equal(2, sections.Count);
        Assert.Equal(MarkdownSectionSegmenter.SectionType.ExecutiveSummary, sections[0].SectionType);
        Assert.Equal(MarkdownSectionSegmenter.SectionType.DetailedAnalysis, sections[1].SectionType);
    }
}
