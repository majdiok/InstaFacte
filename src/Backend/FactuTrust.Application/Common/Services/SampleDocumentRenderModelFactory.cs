using FactuTrust.Application.Common.Models;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Services;

/// <summary>
/// Construit un <see cref="DocumentRenderModel"/> d'exemple par type de document, pour l'aperçu live
/// de l'écran de configuration des modèles (aucune donnée réelle requise).
/// </summary>
public static class SampleDocumentRenderModelFactory
{
    public static DocumentRenderModel Build(PrintableDocumentType documentType)
    {
        var isCredit = documentType == PrintableDocumentType.CreditNote;
        var isSupplier = documentType is PrintableDocumentType.SupplierInvoice or PrintableDocumentType.PurchaseOrder;
        var isDelivery = documentType == PrintableDocumentType.DeliveryNote;

        var title = documentType switch
        {
            PrintableDocumentType.SalesInvoice => "FACTURE",
            PrintableDocumentType.CreditNote => "FACTURE D'AVOIR",
            PrintableDocumentType.Quote => "DEVIS",
            PrintableDocumentType.PurchaseOrder => "BON DE COMMANDE",
            PrintableDocumentType.DeliveryNote => "BON DE LIVRAISON",
            PrintableDocumentType.SupplierInvoice => "FACTURE D'ACHAT",
            _ => "DOCUMENT"
        };

        var lines = new List<DocumentLineModel>
        {
            new()
            {
                LineNumber = 1, Reference = "REF001", Name = "Produit Alpha", Description = "Exemple de désignation",
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
            DocumentType = documentType,
            TitleLabel = title,
            DocumentNumber = "EX-2026-0001",
            IsCreditNote = isCredit,
            Issuer = new PartyRenderInfo
            {
                Name = "Société Exemple SARL", TradeName = "Exemple", TaxId = "1234567/A/M/000",
                CommerceRegistry = "B0123456", AddressLines = new[] { "12 rue de la République", "1000 Tunis" },
                Phone = "+216 71 000 000", Email = "contact@exemple.tn", Rib = "12 345 6789012345678 90", BankName = "Banque Exemple"
            },
            PartyLabel = isSupplier ? "Fournisseur" : isDelivery ? "Destinataire" : "Facturé à",
            Party = new PartyRenderInfo
            {
                Name = isSupplier ? "Fournisseur Démo" : "Client Démo", TaxId = "7654321/B/M/000",
                AddressLines = new[] { "5 avenue Habib Bourguiba", "2000 Ariana" }, Email = "demo@exemple.tn"
            },
            MetaItems = new List<DocumentMetaItem>
            {
                new("Numéro", "EX-2026-0001"),
                new("Date", "18/06/2026"),
                new(isDelivery ? "Livraison" : "Échéance", "18/07/2026")
            },
            Lines = lines,
            ShowDiscountColumn = true,
            ShowDeliveryQuantities = isDelivery,
            SubTotal = 1130.000m,
            VatBreakdown = new List<VatBreakdownLine>
            {
                new("TVA 7%", 7, 180.000m, 12.600m),
                new("TVA 19%", 19, 950.000m, 180.500m)
            },
            FiscalStamp = 1.000m,
            WithholdingAmount = documentType == PrintableDocumentType.SupplierInvoice ? 15.000m : null,
            NetAfterWithholding = documentType == PrintableDocumentType.SupplierInvoice ? 1309.100m : null,
            Total = 1324.100m,
            Currency = "TND",
            AmountInWords = "mille trois cent vingt-quatre dinars et cent millimes",
            Payments = Array.Empty<PaymentRenderLine>(),
            EInvoiceItems = Array.Empty<DocumentMetaItem>(),
            Notes = "Document d'exemple — aperçu du modèle.",
            Terms = "Conditions de paiement : 30 jours.",
            ClosingNote = "Merci de votre confiance.",
            LegalMentions = new[] { "Aperçu généré automatiquement." }
        };
    }
}
