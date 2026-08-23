namespace FactuTrust.Domain.Enums;

public enum NumberingDocumentType
{
    Invoice = 0,
    CreditNote = 1,
    Quote = 2,
    DeliveryNote = 3,
    PurchaseOrder = 4,
    StockTransfer = 5,
    PhysicalInventory = 6,
    CashReceipt = 7,
    CashExpense = 8,
    BankDeposit = 9,

    /// <summary>
    /// Commande client (vague 1). Ajouté EN FIN d'énumération : les valeurs sont persistées
    /// en entier dans <c>DocumentNumberingSchemes.DocumentType</c>, toute réindexation
    /// réaffecterait les schémas de numérotation existants.
    /// </summary>
    SalesOrder = 10,

    /// <summary>Facture honoraires cabinet (schéma séparé de <see cref="Invoice"/>).</summary>
    FeeInvoice = 11,

    /// <summary>Avoir honoraires cabinet (schéma séparé de <see cref="CreditNote"/>).</summary>
    FeeCreditNote = 12,

    /// <summary>Devis honoraires cabinet (schéma séparé de <see cref="Quote"/>).</summary>
    FeeQuote = 13,

    /// <summary>
    /// Bon de réception d'achat. Ajouté EN FIN d'énumération : les valeurs sont persistées
    /// en entier dans <c>DocumentNumberingSchemes.DocumentType</c>.
    /// </summary>
    PurchaseReceipt = 14,

    /// <summary>
    /// Facture fournisseur interne. Ajouté EN FIN d'énumération : les valeurs sont persistées
    /// en entier dans <c>DocumentNumberingSchemes.DocumentType</c>.
    /// </summary>
    SupplierInvoice = 15,

    /// <summary>
    /// Bon de retour client (articles livrés non facturés). Ajouté EN FIN d'énumération.
    /// Préfixe BRT — BR est déjà pris par <see cref="PurchaseReceipt"/>.
    /// </summary>
    SalesReturnNote = 16,

    /// <summary>
    /// Clôture Z de caisse POS. Ajouté EN FIN d'énumération : les valeurs sont persistées
    /// en entier dans <c>DocumentNumberingSchemes.DocumentType</c>.
    /// </summary>
    ZReport = 17,

    /// <summary>
    /// Bon d'entrée de stock générique. Ajouté EN FIN d'énumération.
    /// Préfixe BE.
    /// </summary>
    StockEntry = 18,

    /// <summary>
    /// Bon de sortie de stock générique. Ajouté EN FIN d'énumération.
    /// Préfixe BS.
    /// </summary>
    StockIssue = 19
}

public static class NumberingDocumentTypeExtensions
{
    public static string ToDisplayString(this NumberingDocumentType type) => type switch
    {
        NumberingDocumentType.Invoice => "Facture",
        NumberingDocumentType.CreditNote => "Facture avoir",
        NumberingDocumentType.Quote => "Devis",
        NumberingDocumentType.DeliveryNote => "Bon de livraison",
        NumberingDocumentType.PurchaseOrder => "Bon de commande",
        NumberingDocumentType.StockTransfer => "Transfert stock",
        NumberingDocumentType.PhysicalInventory => "Inventaire physique",
        NumberingDocumentType.CashReceipt => "Encaissement caisse",
        NumberingDocumentType.CashExpense => "Decaissement caisse",
        NumberingDocumentType.BankDeposit => "Remise bancaire",
        NumberingDocumentType.SalesOrder => "Commande client",
        NumberingDocumentType.FeeInvoice => "Facture honoraires",
        NumberingDocumentType.FeeCreditNote => "Avoir honoraires",
        NumberingDocumentType.FeeQuote => "Devis honoraires",
        NumberingDocumentType.PurchaseReceipt => "Bon de réception",
        NumberingDocumentType.SupplierInvoice => "Facture fournisseur",
        NumberingDocumentType.SalesReturnNote => "Bon de retour",
        NumberingDocumentType.ZReport => "Clôture Z",
        NumberingDocumentType.StockEntry => "Bon d'entrée de stock",
        NumberingDocumentType.StockIssue => "Bon de sortie de stock",
        _ => type.ToString()
    };

    public static string DefaultFreeText(this NumberingDocumentType type) => type switch
    {
        NumberingDocumentType.Invoice => "FAC",
        NumberingDocumentType.CreditNote => "AVO",
        NumberingDocumentType.Quote => "DEV",
        NumberingDocumentType.DeliveryNote => "BL",
        NumberingDocumentType.PurchaseOrder => "BC",
        NumberingDocumentType.StockTransfer => "TR",
        NumberingDocumentType.PhysicalInventory => "INVE",
        NumberingDocumentType.CashReceipt => "ENC",
        NumberingDocumentType.CashExpense => "DEP",
        NumberingDocumentType.BankDeposit => "REM",
        NumberingDocumentType.SalesOrder => "CDE",
        NumberingDocumentType.FeeInvoice => "FAC",
        NumberingDocumentType.FeeCreditNote => "AVO",
        NumberingDocumentType.FeeQuote => "DEV",
        NumberingDocumentType.PurchaseReceipt => "BR",
        NumberingDocumentType.SupplierInvoice => "FS",
        NumberingDocumentType.SalesReturnNote => "BRT",
        NumberingDocumentType.ZReport => "Z",
        NumberingDocumentType.StockEntry => "BE",
        NumberingDocumentType.StockIssue => "BS",
        _ => "DOC"
    };

    public static string LegacyPrefix(this NumberingDocumentType type) => DefaultFreeText(type);
}