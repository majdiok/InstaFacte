using FactuTrust.Application.Common.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactuTrust.Infrastructure.Services.Templates;

/// <summary>
/// Maquette 2 — « Tableau Orange » : nom de la société en haut à gauche, titre/numéro à droite,
/// panneau destinataire encadré, tableau orange (Désignation / Quantité / P.U.HT / T.TVA / R.HT / P.T.HT),
/// totaux à droite, pied à trois colonnes (raison sociale / contact / détails bancaires).
/// </summary>
public class OrangeTableTemplate : DocumentTemplateBase
{
    public override string Key => "orange-table";

    /// <summary>La variante compacte masque la ventilation TVA détaillée.</summary>
    protected virtual bool ShowVatTable => true;

    protected override DocumentTemplateTheme Theme { get; } = new()
    {
        Primary = Color.FromHex("#E59400"),
        Accent = Color.FromHex("#E59400"),
        TableHeaderBg = Color.FromHex("#F0A500"),
        TableHeaderText = Colors.White,
        TotalBandBg = Color.FromHex("#E59400"),
        TotalBandText = Colors.White,
        RowAltBg = Color.FromHex("#FDF3E0"),
        BorderColor = Color.FromHex("#E59400")
    };

    protected override void ComposePage(PageDescriptor page, DocumentRenderModel model)
    {
        page.Header().Element(c => ComposeHeader(c, model));
        page.Content().Element(c => ComposeContent(c, model));
        page.Footer().Element(c => ComposeBusinessFooter(c, model));
    }

    private void ComposeHeader(IContainer container, DocumentRenderModel model)
    {
        container.Row(row =>
        {
            row.RelativeItem().Row(leftRow =>
            {
                if (model.LogoBytes is { Length: > 0 })
                {
                    leftRow.ConstantItem(48).Height(48).Image(model.LogoBytes).FitArea();
                    leftRow.ConstantItem(8);
                }
                leftRow.RelativeItem().Column(col =>
                {
                    col.Item().Text(model.Issuer.Name).FontSize(16).Bold();
                    foreach (var line in model.Issuer.AddressLines)
                        col.Item().Text(line).FontSize(8).FontColor(Theme.MutedText);
                });
            });
            row.ConstantItem(16);
            row.RelativeItem().AlignRight().AlignTop()
                .Text($"{model.TitleLabel} N° {model.DocumentNumber}").FontSize(16).Bold()
                .FontColor(model.IsCreditNote ? Colors.Red.Darken2 : Theme.Primary);
        });
    }

    private void ComposeContent(IContainer container, DocumentRenderModel model)
    {
        container.PaddingVertical(12).Column(column =>
        {
            // Panneau destinataire encadré, aligné à droite.
            column.Item().Row(row =>
            {
                row.RelativeItem();
                row.ConstantItem(250).Border(1).BorderColor(Theme.BorderColor).Padding(8).Column(box =>
                {
                    foreach (var item in model.MetaItems.Take(2))
                        box.Item().Text($"{item.Label} : {item.Value}").FontSize(9);
                    box.Item().PaddingTop(6).Text($"{model.PartyLabel} :").Bold().FontSize(9);
                    box.Item().Text(model.Party.Name).Bold().FontSize(9);
                    if (!string.IsNullOrWhiteSpace(model.Party.TaxId))
                        box.Item().Text($"N° Fisc. : {model.Party.TaxId}").FontSize(8);
                    foreach (var line in model.Party.AddressLines)
                        box.Item().Text(line).FontSize(8);
                });
            });

            column.Item().PaddingTop(14).Element(c =>
                ComposeLinesTable(c, model, showLineNumber: false, showRef: false, showUnit: false));

            column.Item().PaddingTop(12).Row(totalsRow =>
            {
                if (ShowVatTable && model.VatBreakdown.Count > 0)
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

            if (model.Payments.Count > 0)
                column.Item().PaddingTop(12).Element(c => ComposePaymentsTable(c, model));

            ComposeNotes(column, model);
        });
    }

    private void ComposeBusinessFooter(IContainer container, DocumentRenderModel model)
    {
        var issuer = model.Issuer;
        container.Column(column =>
        {
            column.Item().BorderTop(1).BorderColor(Theme.Accent).PaddingTop(6);
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Raison sociale").Bold().FontSize(8).FontColor(Theme.Primary);
                    c.Item().Text(issuer.Name).FontSize(8);
                    if (!string.IsNullOrWhiteSpace(issuer.TaxId)) c.Item().Text($"M.F : {issuer.TaxId}").FontSize(8);
                    foreach (var line in issuer.AddressLines) c.Item().Text(line).FontSize(8);
                });
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Contact").Bold().FontSize(8).FontColor(Theme.Primary);
                    if (!string.IsNullOrWhiteSpace(issuer.Phone)) c.Item().Text($"Tél : {issuer.Phone}").FontSize(8);
                    if (!string.IsNullOrWhiteSpace(issuer.Email)) c.Item().Text($"Email : {issuer.Email}").FontSize(8);
                });
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Détails bancaires").Bold().FontSize(8).FontColor(Theme.Primary);
                    if (!string.IsNullOrWhiteSpace(issuer.BankName)) c.Item().Text($"Banque : {issuer.BankName}").FontSize(8);
                    if (!string.IsNullOrWhiteSpace(issuer.Rib)) c.Item().Text($"RIB : {issuer.Rib}").FontSize(8);
                });
            });
            column.Item().PaddingTop(4).AlignCenter().Text(x =>
            {
                x.DefaultTextStyle(BaseTextStyle.FontSize(8));
                x.Span("Page ");
                x.CurrentPageNumber();
                x.Span(" / ");
                x.TotalPages();
            });
        });
    }
}

/// <summary>Maquette 3 — variante compacte du tableau orange (sans ventilation TVA détaillée).</summary>
public sealed class OrangeTableCompactTemplate : OrangeTableTemplate
{
    public override string Key => "orange-table-compact";
    protected override bool ShowVatTable => false;
}
