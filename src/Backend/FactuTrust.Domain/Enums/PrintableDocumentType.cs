namespace FactuTrust.Domain.Enums;

/// <summary>
/// Types de documents pouvant être imprimés/exportés en PDF et configurés avec un modèle visuel.
/// Volontairement distinct de <see cref="NumberingDocumentType"/> pour ne pas coupler le système
/// de modèles d'impression au moteur de numérotation.
/// </summary>
public enum PrintableDocumentType
{
    SalesInvoice = 0,
    CreditNote = 1,
    Quote = 2,
    PurchaseOrder = 3,
    DeliveryNote = 4,
    SupplierInvoice = 5
}

public static class PrintableDocumentTypeExtensions
{
    public static string ToDisplayString(this PrintableDocumentType type) => type switch
    {
        PrintableDocumentType.SalesInvoice => "Facture de vente",
        PrintableDocumentType.CreditNote => "Avoir",
        PrintableDocumentType.Quote => "Devis",
        PrintableDocumentType.PurchaseOrder => "Bon de commande",
        PrintableDocumentType.DeliveryNote => "Bon de livraison",
        PrintableDocumentType.SupplierInvoice => "Facture d'achat",
        _ => type.ToString()
    };
}
