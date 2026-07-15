using FactuTrust.Application.Common.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactuTrust.Infrastructure.Services.Templates;

/// <summary>
/// Maquette 1 — « Classique TVA / Synthèse » : titre bleu, bandeau de tableau orange, blocs
/// « Détails de la TVA » et « Synthèse » en bas, extrait des conditions générales.
/// </summary>
public sealed class ClassicTvaSyntheseTemplate : DocumentTemplateBase
{
    public override string Key => "classic-tva-synthese";

    protected override DocumentTemplateTheme Theme { get; } = new()
    {
        Primary = Color.FromHex("#1A3D82"),
        Accent = Color.FromHex("#1A3D82"),
        TableHeaderBg = Color.FromHex("#F0A500"),
        TableHeaderText = Colors.White,
        TotalBandBg = Color.FromHex("#1A3D82"),
        TotalBandText = Colors.White,
        RowAltBg = Colors.Grey.Lighten4,
        BorderColor = Colors.Grey.Lighten1
    };

    protected override void ComposePage(PageDescriptor page, DocumentRenderModel model)
    {
        page.Header().Element(c => ComposeHeader(c, model));
        page.Content().Element(c => ComposeContent(c, model));
        page.Footer().Element(c => ComposePageNumberFooter(c, model));
    }

    private void ComposeHeader(IContainer container, DocumentRenderModel model)
    {
        container.BorderBottom(2).BorderColor(Theme.Primary).PaddingBottom(8).Row(row =>
        {
            row.RelativeItem().Row(leftRow =>
            {
                if (model.LogoBytes is { Length: > 0 })
                {
                    leftRow.ConstantItem(56).Height(56).Image(model.LogoBytes).FitArea();
                    leftRow.ConstantItem(8);
                }
                leftRow.RelativeItem().Column(col => ComposeIssuerColumn(col, model.Issuer));
            });
            row.ConstantItem(16);
            row.RelativeItem().AlignRight().AlignTop()
                .Text(model.TitleLabel).FontSize(24).Bold()
                .FontColor(model.IsCreditNote ? Colors.Red.Darken2 : Theme.Primary);
        });
    }

    private void ComposeContent(IContainer container, DocumentRenderModel model)
    {
        container.PaddingVertical(12).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(left => ComposePartyColumn(left, model));
                row.RelativeItem().AlignRight().Column(right => ComposeMetaColumn(right, model));
            });

            column.Item().PaddingTop(14).Element(c => ComposeLinesTable(c, model));

            column.Item().PaddingTop(14).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("DÉTAILS DE LA TVA").Bold().FontSize(9).FontColor(Theme.Primary);
                    c.Item().PaddingTop(4).Element(x => ComposeVatTable(x, model));
                });
                row.ConstantItem(16);
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("SYNTHÈSE").Bold().FontSize(9).FontColor(Theme.Primary);
                    c.Item().PaddingTop(4).Element(x => ComposeTotalsColumn(x, model));
                });
            });

            column.Item().PaddingTop(8).Element(c => ComposeGrandTotalBand(c, model));

            if (!string.IsNullOrWhiteSpace(model.AmountInWords))
                column.Item().PaddingTop(10).Element(c => ComposeAmountInWords(c, model));

            if (model.Payments.Count > 0)
                column.Item().PaddingTop(12).Element(c => ComposePaymentsTable(c, model));

            ComposeNotes(column, model);
        });
    }
}
