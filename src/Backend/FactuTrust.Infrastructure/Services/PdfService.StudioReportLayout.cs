using System.Globalization;
using FactuTrust.Application.Features.Studio.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Rendu PDF GÉNÉRIQUE des états Studio (low-code). Contrairement aux autres rendus, ce layout ne
/// connaît aucun modèle métier : il est entièrement piloté par <see cref="ReportResultDto"/>
/// (colonnes dimension/mesure + lignes). Tout état créé dans le Studio — designer ou assistant IA —
/// est donc imprimable sans écrire de rendu dédié.
///
/// La largeur des colonnes s'adapte au nombre de colonnes, et le format paysage s'enclenche au-delà
/// de 6 colonnes pour éviter l'écrasement. Les mesures numériques sont alignées à droite au format
/// tunisien (voir <c>TnAmountFormat</c> défini avec les états comptables).
/// </summary>
public partial class PdfService
{
    /// <summary>Au-delà, on bascule en paysage : 7 colonnes ne tiennent pas lisiblement en portrait A4.</summary>
    private const int StudioLandscapeColumnThreshold = 6;

    /// <summary>Garde-fou de pagination : un état Studio n'est pas un export de masse (CSV/XLSX pour cela).</summary>
    private const int StudioMaxPrintedRows = 2000;

    public Task<byte[]> GenerateStudioReportPdfAsync(
        StudioReportPdfContext context, CancellationToken cancellationToken = default)
    {
        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                var landscape = context.Result.Columns.Count > StudioLandscapeColumnThreshold;
                page.Size(landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(ReportTextStyle);
                page.Header().Element(c => ComposeStudioHeader(c, context));
                page.Content().PaddingVertical(8).Element(c => ComposeStudioBody(c, context));
                page.Footer().Element(ComposeReportFooter);
            });
        }).GeneratePdf();

        return Task.FromResult(pdf);
    }

    private static void ComposeStudioHeader(IContainer container, StudioReportPdfContext ctx)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(string.IsNullOrWhiteSpace(ctx.CompanyName) ? "Société" : ctx.CompanyName)
                        .FontSize(14).Bold();
                    if (!string.IsNullOrWhiteSpace(ctx.MatriculeFiscal))
                        c.Item().Text($"Matricule fiscal : {ctx.MatriculeFiscal}").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(160).AlignRight().Text($"{ctx.Result.TotalRows} ligne(s)")
                    .FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
            });

            col.Item().PaddingTop(6).Text(ctx.Title).FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
            if (!string.IsNullOrWhiteSpace(ctx.SourceLabel))
                col.Item().Text($"Source : {ctx.SourceLabel}").FontSize(9).FontColor(Colors.Grey.Darken2);
            foreach (var criterion in ctx.Criteria)
                col.Item().Text(criterion).FontSize(8).FontColor(Colors.Grey.Darken2);

            col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Medium);
        });
    }

    private static void ComposeStudioBody(IContainer container, StudioReportPdfContext ctx)
    {
        var columns = ctx.Result.Columns;
        if (columns.Count == 0)
        {
            container.AlignCenter().PaddingTop(40)
                .Text("Cet état ne comporte aucune colonne.").FontSize(10).Italic().FontColor(Colors.Grey.Darken1);
            return;
        }

        var rows = ctx.Result.Rows.Take(StudioMaxPrintedRows).ToList();
        if (rows.Count == 0)
        {
            container.AlignCenter().PaddingTop(40)
                .Text("Aucune donnée pour cet état.").FontSize(10).Italic().FontColor(Colors.Grey.Darken1);
            return;
        }

        container.Column(col =>
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(def =>
                {
                    foreach (var column in columns)
                    {
                        // Une mesure tient sur une largeur fixe ; une dimension prend le reste.
                        if (IsMeasure(column)) def.ConstantColumn(90);
                        else def.RelativeColumn();
                    }
                });

                table.Header(header =>
                {
                    foreach (var column in columns)
                    {
                        IContainer cell = header.Cell().Background(Colors.Blue.Darken2).Padding(4);
                        if (IsMeasure(column)) cell = cell.AlignRight();
                        cell.Text(column.Label).FontSize(8).Bold().FontColor(Colors.White);
                    }
                });

                var alternate = false;
                foreach (var row in rows)
                {
                    foreach (var column in columns)
                    {
                        var numeric = IsMeasure(column);
                        IContainer cell = table.Cell();
                        if (alternate) cell = cell.Background(Colors.Grey.Lighten4);
                        cell = cell.Padding(3);
                        if (numeric) cell = cell.AlignRight();
                        cell.Text(FormatStudioCell(row.GetValueOrDefault(column.Key), numeric)).FontSize(8);
                    }
                    alternate = !alternate;
                }
            });

            var totals = BuildStudioTotals(ctx.Result, rows);
            if (totals.Count > 0)
            {
                col.Item().PaddingTop(8).Row(row =>
                {
                    row.RelativeItem();
                    row.ConstantItem(260).Background(Colors.Blue.Darken2).Padding(6).Column(c =>
                    {
                        foreach (var (label, value) in totals)
                        {
                            c.Item().Row(r =>
                            {
                                r.RelativeItem().Text(label).FontSize(8).FontColor(Colors.White);
                                r.ConstantItem(110).AlignRight().Text(value).FontSize(8).Bold().FontColor(Colors.White);
                            });
                        }
                    });
                });
            }

            if (ctx.Result.Rows.Count > rows.Count)
            {
                col.Item().PaddingTop(6)
                    .Text($"Seules les {StudioMaxPrintedRows} premières lignes sont imprimées "
                        + $"({ctx.Result.Rows.Count} au total). Utilisez l'export Excel pour la totalité.")
                    .FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
            }

            // Avertissement de troncature de la SOURCE (distinct de la pagination d'impression) :
            // les chiffres ci-dessus ne portent pas sur la totalité des données.
            if (ctx.Result.Truncated)
            {
                col.Item().PaddingTop(6)
                    .Text($"Attention : résultat tronqué à {ctx.Result.Rows.Count} ligne(s) sur {ctx.Result.TotalRows}. "
                        + "Les totaux ne portent pas sur la totalité des données — affinez la période ou les filtres.")
                    .FontSize(8).Bold().FontColor(Colors.Orange.Darken2);
            }
        });
    }

    /// <summary>Total par colonne de mesure — hors moyennes/min/max, dont la somme n'aurait pas de sens.</summary>
    private static List<(string Label, string Value)> BuildStudioTotals(
        ReportResultDto result, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        var totals = new List<(string, string)>();
        foreach (var column in result.Columns.Where(IsMeasure))
        {
            if (!IsSummableMeasure(column.Key)) continue;

            decimal sum = 0m;
            var any = false;
            foreach (var row in rows)
            {
                if (!TryReadDecimal(row.GetValueOrDefault(column.Key), out var value)) continue;
                sum += value;
                any = true;
            }
            if (any) totals.Add(($"Total {column.Label}", Amount(sum)));
        }
        return totals;
    }

    private static bool IsMeasure(ReportColumn column) =>
        string.Equals(column.Kind, "measure", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// La clé d'agrégat est <c>count</c> ou <c>{fn}_{champ}</c> (cf. <c>CustomReportRunner.AggKey</c>) :
    /// seuls un décompte et une somme s'additionnent verticalement.
    /// </summary>
    private static bool IsSummableMeasure(string key) =>
        string.Equals(key, "count", StringComparison.OrdinalIgnoreCase)
        || key.StartsWith("sum_", StringComparison.OrdinalIgnoreCase);

    private static string FormatStudioCell(object? value, bool numeric)
    {
        if (value is null) return string.Empty;
        if (numeric && TryReadDecimal(value, out var d)) return Amount(d);

        return value switch
        {
            bool b => b ? "Oui" : "Non",
            DateTime dt => dt.TimeOfDay == TimeSpan.Zero
                ? dt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : dt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            decimal or double or int or long => Amount(Convert.ToDecimal(value, CultureInfo.InvariantCulture)),
            // Les dates des enregistrements Studio sont stockées en ISO : on les rend au format local.
            string s when DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                && s.Length >= 10 && s.Contains('-')
                => parsed.TimeOfDay == TimeSpan.Zero
                    ? parsed.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                    : parsed.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static bool TryReadDecimal(object? value, out decimal result)
    {
        switch (value)
        {
            case decimal d: result = d; return true;
            case double db: result = (decimal)db; return true;
            case int i: result = i; return true;
            case long l: result = l; return true;
            case string s when decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed):
                result = parsed; return true;
            default: result = 0m; return false;
        }
    }
}
