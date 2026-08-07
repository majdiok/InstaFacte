namespace FactuTrust.Application.Features.Accounting;

/// <summary>
/// Comptes (SCE tunisien) et journaux utilisés pour la comptabilisation des factures de vente
/// et d'achat. Source unique de vérité : ces constantes reprennent à l'identique les numéros
/// historiquement codés en dur dans <c>AccountingService</c> et <c>SupplierInvoiceJournalLineBuilder</c>.
///
/// Toute proposition d'écriture construite à partir d'un document importé DOIT s'appuyer sur ces
/// constantes, faute de quoi une même facture serait comptabilisée différemment selon qu'elle
/// passe par la comptabilisation automatique ou par l'import en saisie manuelle.
/// </summary>
public static class TunisianPostingAccounts
{
    // ---- Tiers (comptes collectifs) ------------------------------------------------------

    /// <summary>Clients — ventes de biens et services.</summary>
    public const string Client = "4111";

    /// <summary>Fournisseurs — achats de biens et services.</summary>
    public const string Supplier = "4011";

    // ---- Produits (ventes) ---------------------------------------------------------------

    /// <summary>Ventes de marchandises. Compte par défaut lorsque la ligne n'est pas un service.</summary>
    public const string SalesOfGoods = "707";

    /// <summary>Ventes de services (produit rapproché dont <c>Type == ProductType.Service</c>).</summary>
    public const string SalesOfServices = "705";

    // ---- Charges (achats) ----------------------------------------------------------------

    /// <summary>Achats de marchandises.</summary>
    public const string PurchasesOfGoods = "607";

    /// <summary>Immobilisation par défaut lorsqu'une ligne d'achat est marquée immobilisation
    /// sans compte d'immobilisation explicite.</summary>
    public const string DefaultFixedAsset = "218";

    // ---- TVA ------------------------------------------------------------------------------

    /// <summary>TVA collectée sur débits (ventes).</summary>
    public const string VatCollected = "436711";

    /// <summary>TVA déductible sur biens et services (achats).</summary>
    public const string VatDeductibleGoods = "43666";

    /// <summary>TVA déductible sur immobilisations (achats d'immobilisations).</summary>
    public const string VatDeductibleFixedAssets = "43662";

    // ---- Autres taxes ---------------------------------------------------------------------

    /// <summary>FODEC collecté (dette envers l'État). Crédité à la vente, débité en avoir.</summary>
    public const string Fodec = "4477";

    /// <summary>
    /// Timbre fiscal sur une facture de VENTE : collecté pour le compte de l'État, donc au CRÉDIT
    /// d'un compte de dette (classe 4).
    /// </summary>
    public const string FiscalStampOnSale = "4478";

    /// <summary>
    /// Timbre fiscal sur une facture d'ACHAT : supporté par l'entreprise, donc au DÉBIT d'un
    /// compte de charge (classe 6). L'asymétrie avec <see cref="FiscalStampOnSale"/> est voulue.
    /// </summary>
    public const string FiscalStampOnPurchase = "6371";

    // ---- Journaux -------------------------------------------------------------------------

    /// <summary>Journal des ventes.</summary>
    public const string SalesJournalCode = "JV";

    /// <summary>Journal des achats.</summary>
    public const string PurchaseJournalCode = "JA";
}
