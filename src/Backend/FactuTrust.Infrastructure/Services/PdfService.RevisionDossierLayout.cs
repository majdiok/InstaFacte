using System.Globalization;
using FactuTrust.Application.DTOs;
using FactuTrust.Infrastructure.Services.Templates;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Rendu PDF du dossier de révision — la note de travail que l'expert-comptable joint au dossier.
///
/// <para>Format portrait et non paysage, contrairement au rapport de contrôle : c'est un document
/// qui se lit et s'annote, pas un tableau qui se balaie.</para>
///
/// <para>Ce rendu ne calcule rien. Gravité, impact et action arrivent arrêtés dans
/// <see cref="RevisionDossierPdfContext"/> ; en particulier, un impact <c>null</c> s'imprime
/// « non chiffrable » et non « 0,000 » — la nuance est le sens même de la mention.</para>
/// </summary>
public partial class PdfService
{
    public Task<byte[]> GenerateRevisionDossierPdfAsync(
        RevisionDossierPdfContext context,
        CancellationToken cancellationToken = default)
    {
        var bytes = BuildReport(context.Header, landscape: false, col =>
        {
            ComposeRevisionOrigin(col, context);
            ComposeRevisionSummary(col, context.Note);

            if (context.Note.Items.Count == 0)
            {
                EmptyNotice(col, "Aucune anomalie ouverte : rien à consigner sur cet exercice.");
                return;
            }

            ComposeRevisionItems(col, context.Note);
        });

        return Task.FromResult(bytes);
    }

    // ── Provenance ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// D'où sort ce dossier. La mention du mode de rédaction est <b>obligatoire</b> : un lecteur
    /// doit savoir si la prose a été assistée, et le cas échéant pourquoi elle ne l'a pas été.
    /// </summary>
    private static void ComposeRevisionOrigin(ColumnDescriptor col, RevisionDossierPdfContext context)
    {
        var note = context.Note;

        col.Item().PaddingTop(8).Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().Text(t =>
                {
                    t.Span("Établi le ").FontSize(8).FontColor(Colors.Grey.Darken2);
                    t.Span(note.GeneratedAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture))
                        .FontSize(8).Bold();
                    if (!string.IsNullOrWhiteSpace(note.GeneratedByUserName))
                    {
                        t.Span(" par ").FontSize(8).FontColor(Colors.Grey.Darken2);
                        t.Span(note.GeneratedByUserName).FontSize(8);
                    }
                });

                if (context.ControlCompletedAt is { } controlDate)
                {
                    c.Item().Text(
                            $"Issu du contrôle du {controlDate:dd/MM/yyyy HH:mm}" +
                            (context.ComplianceRate is { } rate ? $" — conformité {rate:0.0} %" : ""))
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                }
            });

            row.ConstantItem(190).AlignRight().Column(c =>
            {
                c.Item().AlignRight().Text(note.AiGenerated ? "Rédaction assistée" : "Rédaction non assistée")
                    .FontSize(8).Italic()
                    .FontColor(note.AiGenerated ? Colors.Blue.Darken1 : Colors.Grey.Darken2);

                if (note.AiGenerated && !string.IsNullOrWhiteSpace(note.ModelRef))
                    c.Item().AlignRight().Text(note.ModelRef).FontSize(7).FontColor(Colors.Grey.Darken1);
            });
        });

        // Le motif du repli est une information de confiance : on ne le masque pas.
        if (!string.IsNullOrWhiteSpace(note.FallbackReason))
        {
            col.Item().PaddingTop(4).Background(Colors.Grey.Lighten4)
                .BorderLeft(3).BorderColor(Colors.Blue.Medium).Padding(6)
                .Text($"Constats et montants issus des contrôles — {note.FallbackReason}")
                .FontSize(8).FontColor(Colors.Grey.Darken3);
        }
    }

    // ── Synthèse ──────────────────────────────────────────────────────────────────────────

    private static void ComposeRevisionSummary(ColumnDescriptor col, FirmRevisionNoteDto note)
    {
        col.Item().PaddingTop(12).Text("Synthèse").FontSize(12).Bold().FontColor(Colors.Blue.Darken2);

        col.Item().PaddingTop(4).Background(Colors.Grey.Lighten5).Padding(8)
            .Text(PdfRenderHelpers.CleanTextForPdf(note.ExecutiveSummary))
            .FontSize(9).LineHeight(1.4f);

        col.Item().PaddingTop(8).Row(row =>
        {
            RevisionKpi(row, "Anomalies", note.AnomalyCount.ToString(), Colors.Grey.Darken3);
            RevisionKpi(row, "Bloquantes", note.BlockingCount.ToString(),
                note.BlockingCount > 0 ? Colors.Red.Darken2 : Colors.Green.Darken2);
            RevisionKpi(row, "Impact chiffré", Amount(note.TotalImpactAmount) + " TND", Colors.Blue.Darken2);
        });
    }

    private static void RevisionKpi(RowDescriptor row, string label, string value, string color)
    {
        row.RelativeItem().Border(0.5f).BorderColor(Colors.Grey.Lighten1)
            .Background(Colors.Grey.Lighten4).Padding(6).Column(c =>
            {
                c.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken2);
                c.Item().PaddingTop(2).Text(value).FontSize(13).Bold().FontColor(color);
            });
    }

    // ── Notes de travail ──────────────────────────────────────────────────────────────────

    private static void ComposeRevisionItems(ColumnDescriptor col, FirmRevisionNoteDto note)
    {
        col.Item().PaddingTop(14).Text("Notes de travail")
            .FontSize(12).Bold().FontColor(Colors.Blue.Darken2);

        var ordered = note.Items
            .OrderByDescending(i => i.Severity)
            .ThenByDescending(i => i.ImpactAmount ?? 0m)
            .ToList();

        var index = 1;
        foreach (var item in ordered)
        {
            // Une note ne doit jamais être coupée entre deux pages : elle se lit d'un bloc.
            col.Item().ShowEntire().PaddingTop(10).Column(entry =>
            {
                entry.Item().Row(row =>
                {
                    row.ConstantItem(22).Text($"{index}.").FontSize(9).Bold()
                        .FontColor(Colors.Grey.Darken2);

                    row.RelativeItem().Text(PdfRenderHelpers.CleanTextForPdf(item.Title))
                        .FontSize(10).Bold();

                    row.ConstantItem(84).AlignRight()
                        .Text(AuditSeverityLabel(item.Severity))
                        .FontSize(8).Bold().FontColor(AuditSeverityColor(item.Severity));
                });

                entry.Item().PaddingLeft(22).PaddingTop(3)
                    .Text(PdfRenderHelpers.CleanTextForPdf(item.WorkingNote))
                    .FontSize(9).LineHeight(1.35f);

                if (!string.IsNullOrWhiteSpace(item.ClientQuestion))
                {
                    entry.Item().PaddingLeft(22).PaddingTop(3).Text(t =>
                    {
                        t.Span("À demander au client : ").FontSize(8.5f).Italic()
                            .FontColor(Colors.Orange.Darken3);
                        t.Span(PdfRenderHelpers.CleanTextForPdf(item.ClientQuestion))
                            .FontSize(8.5f).FontColor(Colors.Grey.Darken3);
                    });
                }

                entry.Item().PaddingLeft(22).PaddingTop(4).Row(row =>
                {
                    row.RelativeItem().Text(t =>
                    {
                        t.Span("Action : ").FontSize(8).FontColor(Colors.Grey.Darken2);
                        t.Span(item.ActionLabel).FontSize(8).Bold().FontColor(Colors.Purple.Darken2);
                    });

                    row.ConstantItem(150).AlignRight().Text(t =>
                    {
                        t.Span("Impact : ").FontSize(8).FontColor(Colors.Grey.Darken2);
                        // Un impact absent n'est pas un impact nul : la mention le dit.
                        t.Span(item.ImpactAmount is { } amount ? $"{Amount(amount)} TND" : "non chiffrable")
                            .FontSize(8).Bold();
                    });
                });

                var references = BuildRevisionReferences(item);
                if (references.Length > 0)
                {
                    entry.Item().PaddingLeft(22).PaddingTop(2)
                        .Text(references).FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                }

                entry.Item().PaddingTop(6).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
            });

            index++;
        }
    }

    /// <summary>Références de rattachement (compte, pièce), omises quand elles n'apportent rien.</summary>
    private static string BuildRevisionReferences(FirmRevisionNoteItemDto item)
    {
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(item.AccountRef)) parts.Add($"Compte {item.AccountRef}");
        if (!string.IsNullOrWhiteSpace(item.PieceRef)) parts.Add($"Pièce {item.PieceRef}");
        return string.Join(" · ", parts);
    }
}
