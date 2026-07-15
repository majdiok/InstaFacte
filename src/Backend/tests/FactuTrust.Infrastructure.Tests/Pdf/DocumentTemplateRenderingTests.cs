using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Common.Models;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.Templates;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Pdf;

/// <summary>
/// Smoke-tests de rendu : chaque modèle visuel doit produire un PDF valide pour des données
/// représentatives (lignes avec remise, ventilation TVA, timbre, règlements, retenue, BL).
/// Génère aussi, en best-effort, des artefacts visuels dans artifacts/qa-pdf-templates pour revue.
/// </summary>
public sealed class DocumentTemplateRenderingTests
{
    private static IReadOnlyList<IDocumentTemplate> AllTemplates() => new IDocumentTemplate[]
    {
        new StandardDocumentTemplate(),
        new ClassicTvaSyntheseTemplate(),
        new OrangeTableTemplate(),
        new OrangeTableCompactTemplate(),
        new ModernBoxedTemplate(),
        new ModernBoxedPaymentsTemplate()
    };

    public static IEnumerable<object[]> TemplateKeys() =>
        AllTemplates().Select(t => new object[] { t.Key });

    [Theory]
    [MemberData(nameof(TemplateKeys))]
    public void Render_produces_valid_pdf_for_each_template(string templateKey)
    {
        var template = AllTemplates().Single(t => t.Key == templateKey);

        foreach (var sample in SampleModels())
        {
            var bytes = template.Render(sample.Model);

            Assert.NotNull(bytes);
            Assert.True(bytes.Length > 800, $"PDF trop court pour {templateKey}/{sample.Name}");
            // En-tête PDF "%PDF"
            Assert.Equal(0x25, bytes[0]);
            Assert.Equal(0x50, bytes[1]);
            Assert.Equal(0x44, bytes[2]);
            Assert.Equal(0x46, bytes[3]);

            TryWriteArtifact(templateKey, sample.Name, bytes);
        }
    }

    private static IEnumerable<(string Name, DocumentRenderModel Model)> SampleModels()
    {
        yield return ("facture", BuildSample(PrintableDocumentType.SalesInvoice, "FACTURE"));
        yield return ("avoir", BuildSample(PrintableDocumentType.CreditNote, "FACTURE D'AVOIR", isCredit: true));
        yield return ("bon-livraison", BuildSample(PrintableDocumentType.DeliveryNote, "BON DE LIVRAISON", delivery: true));
        yield return ("facture-achat", BuildSample(PrintableDocumentType.SupplierInvoice, "FACTURE D'ACHAT", withholding: true));
        yield return ("reglements", BuildSample(PrintableDocumentType.SalesInvoice, "FACTURE", payments: true));
    }

    private static DocumentRenderModel BuildSample(
        PrintableDocumentType type, string title,
        bool isCredit = false, bool delivery = false, bool withholding = false, bool payments = false)
    {
        var lines = new List<DocumentLineModel>
        {
            new()
            {
                LineNumber = 1, Reference = "REF001", Name = "Produit Alpha", Description = "Description détaillée",
                Quantity = 10, Unit = "U", UnitPriceHt = 100.000m, VatLabel = "TVA 19%", VatRatePercent = 19,
                DiscountPercent = 5m, LineTotalHt = 950.000m, OrderedQuantity = 10, DeliveredQuantity = 8
            },
            new()
            {
                LineNumber = 2, Reference = "REF002", Name = "Produit Bêta", Description = null,
                Quantity = 3, Unit = "Kg", UnitPriceHt = 60.000m, VatLabel = "TVA 7%", VatRatePercent = 7,
                DiscountPercent = null, LineTotalHt = 180.000m, OrderedQuantity = 3, DeliveredQuantity = 3
            }
        };

        return new DocumentRenderModel
        {
            DocumentType = type,
            TitleLabel = title,
            DocumentNumber = "FAC-2026-0001",
            IsCreditNote = isCredit,
            Issuer = new PartyRenderInfo
            {
                Name = "Société Exemple SARL", TradeName = "Exemple", TaxId = "1234567/A/M/000",
                CommerceRegistry = "B0123456", AddressLines = new[] { "12 rue de la République", "1000 Tunis" },
                Phone = "+216 71 000 000", Email = "contact@exemple.tn", Rib = "12 345 6789012345678 90", BankName = "Banque X"
            },
            PartyLabel = type == PrintableDocumentType.SupplierInvoice || type == PrintableDocumentType.PurchaseOrder ? "Fournisseur" : "Facturé à",
            Party = new PartyRenderInfo
            {
                Name = "Client Test", TaxId = "7654321/B/M/000",
                AddressLines = new[] { "5 avenue Habib Bourguiba", "2000 Ariana" }, Email = "client@test.tn"
            },
            MetaItems = new List<DocumentMetaItem>
            {
                new("Numéro", "FAC-2026-0001"),
                new("Date", "18/06/2026"),
                new("Échéance", "18/07/2026")
            },
            Lines = lines,
            ShowDiscountColumn = true,
            ShowDeliveryQuantities = delivery,
            SubTotal = 1130.000m,
            VatBreakdown = new List<VatBreakdownLine>
            {
                new("TVA 7%", 7, 180.000m, 12.600m),
                new("TVA 19%", 19, 950.000m, 180.500m)
            },
            FiscalStamp = 1.000m,
            WithholdingAmount = withholding ? 15.000m : null,
            NetAfterWithholding = withholding ? 1309.100m : null,
            Total = 1324.100m,
            Currency = "TND",
            AmountInWords = "mille trois cent vingt-quatre dinars et cent millimes",
            Payments = payments
                ? new List<PaymentRenderLine> { new("CHQ-001", "18/06/2026", "Chèque", 1324.100m) }
                : Array.Empty<PaymentRenderLine>(),
            EInvoiceItems = Array.Empty<DocumentMetaItem>(),
            Notes = "Merci de régler sous 30 jours.",
            Terms = "Paiement à 30 jours fin de mois.",
            ClosingNote = "Merci de votre confiance.",
            LegalMentions = new[] { "TVA acquittée sur les débits." }
        };
    }

    private static void TryWriteArtifact(string templateKey, string sampleName, byte[] bytes)
    {
        try
        {
            var dir = FindArtifactsDir();
            if (dir is null) return;
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, $"{templateKey}_{sampleName}.pdf"), bytes);
        }
        catch
        {
            // best-effort : ne jamais faire échouer le test pour un souci d'écriture.
        }
    }

    private static string? FindArtifactsDir()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "src", "Backend", "FactuTrust.sln")))
                return Path.Combine(current.FullName, "artifacts", "qa-pdf-templates");
            current = current.Parent;
        }
        return null;
    }
}
