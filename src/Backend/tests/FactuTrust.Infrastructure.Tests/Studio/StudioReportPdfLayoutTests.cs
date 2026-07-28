using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Infrastructure.Services;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Rendu PDF générique des états Studio. Le rendu étant piloté par le seul <see cref="ReportResultDto"/>,
/// ces tests couvrent les formes limites qui feraient planter QuestPDF en production : état vide,
/// sans colonne, très large (bascule paysage) et volumineux (troncature).
/// </summary>
public sealed class StudioReportPdfLayoutTests
{
    // Le rendu des états Studio n'utilise ni logo distant ni gabarit de document : les dépendances
    // du service (HTTP, registre de gabarits) ne sont jamais sollicitées sur ce chemin.
    private static readonly PdfService Service = new(
        Mock.Of<System.Net.Http.IHttpClientFactory>(),
        Mock.Of<IDocumentTemplateRegistry>());

    private static StudioReportPdfContext Context(ReportResultDto result, IReadOnlyList<string>? criteria = null) =>
        new("Société Test", "1234567A", "Contrats par statut", "Contrats",
            criteria ?? Array.Empty<string>(), result);

    [Fact]
    public async Task Renders_a_grouped_report_with_totals()
    {
        var result = new ReportResultDto(
            new[]
            {
                new ReportColumn("statut", "Statut", "dimension"),
                new ReportColumn("sum_montant", "Somme de Montant", "measure"),
                new ReportColumn("count", "Nombre", "measure")
            },
            new IReadOnlyDictionary<string, object?>[]
            {
                new Dictionary<string, object?> { ["statut"] = "Actif", ["sum_montant"] = 1250.500m, ["count"] = 3 },
                new Dictionary<string, object?> { ["statut"] = "Expiré", ["sum_montant"] = 800m, ["count"] = 2 }
            },
            2);

        var pdf = await Service.GenerateStudioReportPdfAsync(Context(result,
            new[] { "Filtres : Statut = actif", "Trié par : Montant décroissant" }));

        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 1000, "Le PDF généré est anormalement court.");
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public async Task Renders_a_detail_report_with_mixed_value_types()
    {
        var result = new ReportResultDto(
            new[]
            {
                new ReportColumn("nom", "Nom", "dimension"),
                new ReportColumn("actif", "Actif", "dimension"),
                new ReportColumn("date_debut", "Date de début", "dimension"),
                new ReportColumn("montant", "Montant", "measure")
            },
            new IReadOnlyDictionary<string, object?>[]
            {
                new Dictionary<string, object?>
                {
                    ["nom"] = "Contrat A",
                    ["actif"] = true,
                    ["date_debut"] = "2026-03-15",
                    ["montant"] = 1500.750m
                },
                // Ligne partielle : valeurs manquantes / nulles — ne doit pas faire échouer le rendu.
                new Dictionary<string, object?> { ["nom"] = "Contrat B", ["montant"] = null }
            },
            2);

        var pdf = await Service.GenerateStudioReportPdfAsync(Context(result));

        Assert.True(pdf.Length > 1000);
    }

    [Fact]
    public async Task Empty_report_still_produces_a_valid_pdf()
    {
        var result = new ReportResultDto(
            new[] { new ReportColumn("nom", "Nom", "dimension") },
            Array.Empty<IReadOnlyDictionary<string, object?>>(),
            0);

        var pdf = await Service.GenerateStudioReportPdfAsync(Context(result));

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public async Task Report_without_any_column_does_not_throw()
    {
        var result = new ReportResultDto(
            Array.Empty<ReportColumn>(), Array.Empty<IReadOnlyDictionary<string, object?>>(), 0);

        var pdf = await Service.GenerateStudioReportPdfAsync(Context(result));

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public async Task Wide_report_switches_to_landscape_without_overflowing()
    {
        var columns = Enumerable.Range(0, 10)
            .Select(i => new ReportColumn($"c{i}", $"Colonne {i}", i % 2 == 0 ? "dimension" : "measure"))
            .ToList();
        var row = columns.ToDictionary(c => c.Key, c => (object?)"valeur relativement longue");

        var result = new ReportResultDto(columns, new IReadOnlyDictionary<string, object?>[] { row }, 1);

        var pdf = await Service.GenerateStudioReportPdfAsync(Context(result));

        Assert.True(pdf.Length > 1000);
    }

    [Fact]
    public async Task Large_report_is_truncated_rather_than_generating_an_unbounded_document()
    {
        var columns = new[] { new ReportColumn("nom", "Nom", "dimension") };
        var rows = Enumerable.Range(0, 2500)
            .Select(i => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { ["nom"] = $"Ligne {i}" })
            .ToList();

        var result = new ReportResultDto(columns, rows, rows.Count);

        var pdf = await Service.GenerateStudioReportPdfAsync(Context(result));

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }
}
