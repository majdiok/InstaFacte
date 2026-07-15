using FactuTrust.Application.Common.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactuTrust.Infrastructure.Services.Templates;

/// <summary>
/// Maquette 4 — « Moderne Encadré » : logo en haut à gauche, bandeaux d'information encadrés
/// (informations du document / informations du client), tableau au style moderne, totaux à droite.
/// </summary>
public class ModernBoxedTemplate : DocumentTemplateBase
{
    public override string Key => "modern-boxed";

    /// <summary>La variante « + Règlements » affiche la ventilation TVA et le tableau des règlements.</summary>
    protected virtual bool ShowVatAndPayments => false;

    protected override DocumentTemplateTheme Theme { get; } = new()
    {
        Primary = Color.FromHex("#3F8DA0"),
        Accent = Color.FromHex("#3F8DA0"),
        TableHeaderBg = Color.FromHex("#4A9CB0"),
        TableHeaderText = Colors.White,
        TotalBandBg = Color.FromHex("#3F8DA0"),
        TotalBandText = Colors.White,
        RowAltBg = Color.FromHex("#EAF4F7"),
        BorderColor = Color.FromHex("#4A9CB0")
    };

    protected override void ComposePage(PageDescriptor page, DocumentRenderModel model)
    {
        page.Header().Element(c => ComposeHeader(c, model));
        page.Content().Element(c => ComposeContent(c, model));
        page.Footer().Element(c => ComposePageNumberFooter(c, model));
    }

    private void ComposeHeader(IContainer container, DocumentRenderModel model)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(left =>
            {
                if (model.LogoBytes is { Length: > 0 })
                    left.Item().Width(120).Height(56).Image(model.LogoBytes).FitArea();
                else
                    left.Item().Text(model.Issuer.Name).FontSize(16).Bold();

                left.Item().PaddingTop(8).Text($"{model.TitleLabel} N° {model.DocumentNumber}")
                    .FontSize(15).Bold().FontColor(model.IsCreditNote ? Colors.Red.Darken2 : Theme.Primary);
            });

            row.ConstantItem(16);
            row.RelativeItem().Column(right =>
            {
                right.Item().Element(c => BoxedBlock(c, $"Informations de {(model.IsCreditNote ? "l'avoir" : "facturation")}", inner =>
                {
                    foreach (var item in model.MetaItems)
                        inner.Item().Text($"{item.Label} : {item.Value}").FontSize(8);
                }));
                right.Item().PaddingTop(6).Element(c => BoxedBlock(c, $"Informations {model.PartyLabel.ToLowerInvariant()}", inner =>
                {
                    inner.Item().Text(model.Party.Name).Bold().FontSize(9);
                    if (!string.IsNullOrWhiteSpace(model.Party.TaxId))
                        inner.Item().Text($"N° Fisc. : {model.Party.TaxId}").FontSize(8);
                    foreach (var line in model.Party.AddressLines)
                        inner.Item().Text(line).FontSize(8);
                    if (!string.IsNullOrWhiteSpace(model.Party.Email))
                        inner.Item().Text(model.Party.Email!).FontSize(8);
                }));
            });
        });
    }

    private void BoxedBlock(IContainer container, string title, Action<ColumnDescriptor> content)
    {
        container.Border(1).BorderColor(Theme.BorderColor).Column(col =>
        {
            col.Item().Background(Theme.TableHeaderBg).Padding(4).AlignCenter()
                .Text(title).FontColor(Theme.TableHeaderText).Bold().FontSize(9);
            col.Item().Padding(6).Column(content);
        });
    }

    private void ComposeContent(IContainer container, DocumentRenderModel model)
    {
        container.PaddingVertical(12).Column(column =>
        {
            column.Item().Element(c => ComposeLinesTable(c, model, showRef: true));

            column.Item().PaddingTop(12).Row(totalsRow =>
            {
                if (ShowVatAndPayments && model.VatBreakdown.Count > 0)
                {
                    totalsRow.RelativeItem().Element(c => ComposeVatTable(c, model));
                    totalsRow.ConstantItem(16);
                }
                else
                {
                    totalsRow.RelativeItem();
                }
                totalsRow.RelativeItem().AlignRight().Width(240).Element(c => ComposeTotalsColumn(c, model));
            });

            column.Item().PaddingTop(8).Element(c => ComposeGrandTotalBand(c, model));

            if (!string.IsNullOrWhiteSpace(model.AmountInWords))
                column.Item().PaddingTop(10).Element(c => ComposeAmountInWords(c, model));

            if (ShowVatAndPayments && model.Payments.Count > 0)
            {
                column.Item().PaddingTop(12).Text("Règlements").Bold().FontSize(9).FontColor(Theme.Primary);
                column.Item().PaddingTop(4).Element(c => ComposePaymentsTable(c, model));
            }

            ComposeNotes(column, model);
        });
    }
}

/// <summary>Maquette 5 — variante moderne avec ventilation TVA et tableau des règlements.</summary>
public sealed class ModernBoxedPaymentsTemplate : ModernBoxedTemplate
{
    public override string Key => "modern-boxed-payments";
    protected override bool ShowVatAndPayments => true;
}
