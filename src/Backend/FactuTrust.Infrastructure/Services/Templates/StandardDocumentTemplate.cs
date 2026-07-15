using FactuTrust.Application.Common.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactuTrust.Infrastructure.Services.Templates;

/// <summary>
/// Modèle "Standard" unifié (clé <c>standard</c>) : reprend le style maison historique (bandeau bleu,
/// tableau des lignes, ventilation TVA, totaux, montant en lettres). Sert de modèle par défaut pour les
/// types nouvellement dotés d'un PDF (bons de livraison, factures d'achat) et reste sélectionnable
/// pour tous les types.
/// </summary>
public sealed class StandardDocumentTemplate : DocumentTemplateBase
{
    public const string TemplateKey = "standard";
    public override string Key => TemplateKey;

    protected override DocumentTemplateTheme Theme { get; } = new();

    protected override void ComposePage(PageDescriptor page, DocumentRenderModel model)
    {
        page.Header().Element(c => ComposeHeader(c, model));
        page.Content().Element(c => ComposeContent(c, model));
        page.Footer().Element(c => ComposePageNumberFooter(c, model));
    }

    private void ComposeHeader(IContainer container, DocumentRenderModel model)
    {
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(8).Row(row =>
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
                .Text(model.TitleLabel)
                .FontSize(22).Bold()
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

            if (model.QrBytes is { Length: > 0 })
            {
                column.Item().PaddingTop(12).Row(qrRow =>
                {
                    qrRow.ConstantItem(88).Height(88).Image(model.QrBytes).FitArea();
                    qrRow.ConstantItem(12);
                    qrRow.RelativeItem().Column(qc =>
                    {
                        qc.Item().Text("E-Facture").Bold().FontSize(10).FontColor(Theme.Primary);
                        foreach (var item in model.EInvoiceItems)
                            qc.Item().Text($"{item.Label} : {item.Value}").FontSize(9);
                    });
                });
            }

            column.Item().PaddingTop(14).Element(c => ComposeLinesTable(c, model));

            if (model.VatBreakdown.Count > 0)
            {
                column.Item().PaddingTop(12).Row(totalsRow =>
                {
                    totalsRow.RelativeItem().Element(c => ComposeVatTable(c, model));
                    totalsRow.ConstantItem(16);
                    totalsRow.RelativeItem().AlignRight().Width(240).Element(c => ComposeTotalsColumn(c, model));
                });
            }
            else
            {
                column.Item().PaddingTop(12).AlignRight().Width(240).Element(c => ComposeTotalsColumn(c, model));
            }

            column.Item().PaddingTop(8).Element(c => ComposeGrandTotalBand(c, model));

            if (!string.IsNullOrWhiteSpace(model.AmountInWords))
                column.Item().PaddingTop(10).Element(c => ComposeAmountInWords(c, model));

            if (model.Payments.Count > 0)
                column.Item().PaddingTop(12).Element(c => ComposePaymentsTable(c, model));

            ComposeNotes(column, model);
        });
    }
}
