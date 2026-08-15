using FactuTrust.Application.DTOs;
using FactuTrust.Infrastructure.Services.Templates;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Rendu PDF du rapport de contrôle d'intégrité comptable.
///
/// <para>Réutilise l'enveloppe tabulaire des états comptables (<c>BuildReport</c>, en-tête société,
/// pied paginé, cellules) définie dans <c>PdfService.AccountingReportsLayout.cs</c> : le rapport
/// d'audit sort ainsi avec la même identité visuelle que la balance ou le grand livre.</para>
///
/// <para>Ce rendu ne calcule rien : taux de conformité, compteurs et montants arrivent déjà arrêtés
/// dans <see cref="AccountingAuditPdfContext"/>.</para>
/// </summary>
public partial class PdfService
{
    private static readonly string[] AuditSeverityLabels = ["Information", "Avertissement", "Bloquant"];

    private static readonly string[] AuditStatusLabels =
        ["Ouverte", "En cours", "Sous surveillance", "Corrigée", "Ignorée"];

    private static string AuditSeverityLabel(int severity) =>
        severity >= 0 && severity < AuditSeverityLabels.Length ? AuditSeverityLabels[severity] : "—";

    private static string AuditStatusLabel(int status) =>
        status >= 0 && status < AuditStatusLabels.Length ? AuditStatusLabels[status] : "—";

    private static string AuditSeverityColor(int severity) => severity switch
    {
        2 => Colors.Red.Darken2,
        1 => Colors.Orange.Darken2,
        _ => Colors.Blue.Darken1
    };

    public Task<byte[]> GenerateAccountingAuditPdfAsync(
        AccountingAuditPdfContext context,
        CancellationToken cancellationToken = default)
    {
        var bytes = BuildReport(context.Header, landscape: true, col =>
        {
            ComposeAuditSummary(col, context.Dashboard);

            if (context.Modules.Count > 0)
                ComposeAuditModuleBreakdown(col, context.Modules);

            if (context.Anomalies.Count == 0)
            {
                EmptyNotice(col, "Aucune anomalie ouverte sur l'exercice — dossier conforme aux contrôles exécutés.");
                return;
            }

            ComposeAuditAnomalyTable(col, context);
        });

        return Task.FromResult(bytes);
    }

    // ── Synthèse de conformité ────────────────────────────────────────────────────────────

    private static void ComposeAuditSummary(ColumnDescriptor col, AccountingAuditDashboardDto dash)
    {
        col.Item().PaddingTop(8).Row(row =>
        {
            AuditKpi(row, "Taux de conformité", $"{dash.ComplianceRate:0.0} %", Colors.Green.Darken2);
            AuditKpi(row, "Bloquants", dash.BlockingCount.ToString(), Colors.Red.Darken2);
            AuditKpi(row, "Avertissements", dash.WarningCount.ToString(), Colors.Orange.Darken2);
            AuditKpi(row, "Informations", dash.InfoCount.ToString(), Colors.Blue.Darken1);
            AuditKpi(row, "Total anomalies", dash.AnomalyCount.ToString(), Colors.Grey.Darken3);
        });

        if (dash.ComplianceRateDeltaVsPriorYear is { } delta)
        {
            var sign = delta >= 0 ? "+" : "";
            var color = delta >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2;
            col.Item().PaddingTop(4).Text(t =>
            {
                t.Span("Évolution vs exercice précédent : ").FontSize(8).FontColor(Colors.Grey.Darken2);
                t.Span($"{sign}{delta:0.0} point(s)").FontSize(8).Bold().FontColor(color);
            });
        }

        if (dash.LastRun is { } run)
        {
            col.Item().PaddingTop(2).Text(
                    $"Dernier contrôle : {run.CompletedAt:dd/MM/yyyy HH:mm} " +
                    $"(durée {run.Duration:hh\\:mm\\:ss})" +
                    (string.IsNullOrWhiteSpace(run.TriggeredByUserName) ? "" : $" — déclenché par {run.TriggeredByUserName}"))
                .FontSize(8).FontColor(Colors.Grey.Darken1);
        }
    }

    private static void AuditKpi(RowDescriptor row, string label, string value, string color)
    {
        row.RelativeItem().Border(0.5f).BorderColor(Colors.Grey.Lighten1)
            .Background(Colors.Grey.Lighten4).Padding(6).Column(c =>
            {
                c.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken2);
                c.Item().PaddingTop(2).Text(value).FontSize(15).Bold().FontColor(color);
            });
    }

    // ── Répartition par module ────────────────────────────────────────────────────────────

    private static void ComposeAuditModuleBreakdown(
        ColumnDescriptor col,
        IReadOnlyList<AccountingControlModuleDto> modules)
    {
        var active = modules.Where(m => m.Count > 0).OrderByDescending(m => m.Count).ToList();
        if (active.Count == 0) return;

        col.Item().PaddingTop(12).Text("Répartition par domaine de contrôle")
            .FontSize(11).Bold().FontColor(Colors.Blue.Darken2);

        col.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(3);
                c.ConstantColumn(90);
            });

            table.Header(h =>
            {
                h.Cell().Element(HeadCell).Text("Domaine").Bold();
                h.Cell().Element(HeadCell).AlignRight().Text("Anomalies").Bold();
            });

            foreach (var module in active)
            {
                table.Cell().Element(BodyCell).Text(PdfRenderHelpers.CleanTextForPdf(module.Label));
                table.Cell().Element(BodyCell).AlignRight().Text(module.Count.ToString());
            }
        });
    }

    // ── Détail des anomalies ──────────────────────────────────────────────────────────────

    private static void ComposeAuditAnomalyTable(ColumnDescriptor col, AccountingAuditPdfContext context)
    {
        col.Item().PaddingTop(14).Text("Détail des anomalies")
            .FontSize(11).Bold().FontColor(Colors.Blue.Darken2);

        if (context.TotalAnomalyCount > context.Anomalies.Count)
        {
            col.Item().PaddingBottom(2).Text(
                    $"{context.Anomalies.Count} anomalie(s) détaillée(s) sur {context.TotalAnomalyCount} — " +
                    "utiliser l'export CSV pour la liste exhaustive.")
                .FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
        }

        col.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(78);   // sévérité
                c.RelativeColumn(3);    // intitulé
                c.ConstantColumn(64);   // compte
                c.ConstantColumn(96);   // période
                c.ConstantColumn(88);   // montant
                c.ConstantColumn(74);   // statut
            });

            table.Header(h =>
            {
                h.Cell().Element(HeadCell).Text("Sévérité").Bold();
                h.Cell().Element(HeadCell).Text("Anomalie").Bold();
                h.Cell().Element(HeadCell).Text("Compte").Bold();
                h.Cell().Element(HeadCell).Text("Période").Bold();
                h.Cell().Element(HeadCell).AlignRight().Text("Montant").Bold();
                h.Cell().Element(HeadCell).Text("Statut").Bold();
            });

            foreach (var anomaly in context.Anomalies
                         .OrderByDescending(a => a.Severity)
                         .ThenByDescending(a => a.Amount))
            {
                table.Cell().Element(BodyCell)
                    .Text(AuditSeverityLabel(anomaly.Severity))
                    .FontColor(AuditSeverityColor(anomaly.Severity)).Bold();

                table.Cell().Element(BodyCell).Column(c =>
                {
                    c.Item().Text(PdfRenderHelpers.CleanTextForPdf(anomaly.Title)).Bold();
                    if (!string.IsNullOrWhiteSpace(anomaly.Description))
                    {
                        c.Item().Text(PdfRenderHelpers.CleanTextForPdf(anomaly.Description))
                            .FontSize(8).FontColor(Colors.Grey.Darken2);
                    }
                });

                table.Cell().Element(BodyCell).Text(anomaly.AccountRef ?? "—");
                table.Cell().Element(BodyCell).Text(FormatAuditPeriod(anomaly.PeriodFrom, anomaly.PeriodTo));
                table.Cell().Element(BodyCell).AlignRight().Text(AmountOrBlank(anomaly.Amount));
                table.Cell().Element(BodyCell).Text(AuditStatusLabel(anomaly.Status));
            }
        });
    }

    private static string FormatAuditPeriod(DateOnly? from, DateOnly? to)
    {
        if (from is null && to is null) return "Exercice";
        if (from is not null && to is not null) return $"{from:dd/MM/yy} — {to:dd/MM/yy}";
        return (from ?? to)!.Value.ToString("dd/MM/yyyy");
    }
}
