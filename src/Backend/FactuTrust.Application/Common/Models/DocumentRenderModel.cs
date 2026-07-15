using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Models;

/// <summary>
/// Projection neutre et auto-suffisante d'un document (facture, devis, avoir, bon de commande,
/// bon de livraison, facture d'achat) destinée au rendu PDF.
///
/// Tous les modèles visuels (<c>IDocumentTemplate</c>) consomment ce type : un même modèle peut
/// donc rendre n'importe quel type de document. Les différences propres à chaque type (échéance vs
/// validité, client vs fournisseur, quantités livrées, retenue à la source…) sont résolues en amont
/// par les mappers, puis exprimées ici via des champs optionnels et des drapeaux d'affichage.
///
/// Le modèle est volontairement « pur » : le logo et le QR code sont déjà résolus en bytes, de sorte
/// que les renderers n'effectuent aucune I/O et restent des fonctions déterministes du modèle.
/// </summary>
public sealed class DocumentRenderModel
{
    public PrintableDocumentType DocumentType { get; init; }

    /// <summary>Libellé du titre principal ("FACTURE", "FACTURE D'AVOIR", "DEVIS"…).</summary>
    public string TitleLabel { get; init; } = string.Empty;

    /// <summary>Numéro du document (ex. "FAC-2026-0001").</summary>
    public string DocumentNumber { get; init; } = string.Empty;

    public bool IsCreditNote { get; init; }

    /// <summary>Émetteur (société courante) — toujours présent pour le rendu de l'en-tête.</summary>
    public PartyRenderInfo Issuer { get; init; } = new();

    /// <summary>Logo de l'émetteur déjà téléchargé (peut être null).</summary>
    public byte[]? LogoBytes { get; init; }

    /// <summary>Libellé du tiers ("Facturé à", "Fournisseur", "Destinataire"…).</summary>
    public string PartyLabel { get; init; } = "Client";

    /// <summary>Tiers (client ou fournisseur).</summary>
    public PartyRenderInfo Party { get; init; } = new();

    /// <summary>Métadonnées affichées en tête (dates, statut, échéance, références…).</summary>
    public IReadOnlyList<DocumentMetaItem> MetaItems { get; init; } = Array.Empty<DocumentMetaItem>();

    public IReadOnlyList<DocumentLineModel> Lines { get; init; } = Array.Empty<DocumentLineModel>();

    /// <summary>Afficher la colonne remise dans le tableau des lignes.</summary>
    public bool ShowDiscountColumn { get; init; }

    /// <summary>Afficher les colonnes quantités commandée/livrée (bons de livraison).</summary>
    public bool ShowDeliveryQuantities { get; init; }

    // --- Totaux ---
    public decimal SubTotal { get; init; }
    public IReadOnlyList<VatBreakdownLine> VatBreakdown { get; init; } = Array.Empty<VatBreakdownLine>();

    /// <summary>Remise globale (null si non applicable / absente).</summary>
    public decimal? DiscountTotal { get; init; }

    public decimal Fodec { get; init; }
    public decimal FiscalStamp { get; init; }

    /// <summary>Montant de retenue à la source (factures d'achat) — null si non applicable.</summary>
    public decimal? WithholdingAmount { get; init; }
    public decimal? NetAfterWithholding { get; init; }

    public decimal Total { get; init; }
    public string Currency { get; init; } = "TND";
    public string AmountInWords { get; init; } = string.Empty;

    // --- Règlements (modèle modern-boxed-payments) ---
    public IReadOnlyList<PaymentRenderLine> Payments { get; init; } = Array.Empty<PaymentRenderLine>();

    // --- E-facture / QR ---
    public byte[]? QrBytes { get; init; }
    public IReadOnlyList<DocumentMetaItem> EInvoiceItems { get; init; } = Array.Empty<DocumentMetaItem>();

    // --- Pied / mentions ---
    public string? Notes { get; init; }
    public string? Terms { get; init; }
    public string? ClosingNote { get; init; }
    public string? SignatureHash { get; init; }
    public IReadOnlyList<string> LegalMentions { get; init; } = Array.Empty<string>();
}

/// <summary>Coordonnées d'une partie (émetteur ou tiers) prêtes à l'affichage.</summary>
public sealed class PartyRenderInfo
{
    public string Name { get; init; } = string.Empty;
    public string? TradeName { get; init; }
    public string? TaxId { get; init; }          // Matricule fiscal / NIF
    public string? CommerceRegistry { get; init; } // RC
    public IReadOnlyList<string> AddressLines { get; init; } = Array.Empty<string>();
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Rib { get; init; }
    public string? BankName { get; init; }
}

public sealed record DocumentMetaItem(string Label, string Value);

public sealed class DocumentLineModel
{
    public int LineNumber { get; init; }
    public string? Reference { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal Quantity { get; init; }
    public string? Unit { get; init; }
    public decimal UnitPriceHt { get; init; }
    public string VatLabel { get; init; } = string.Empty; // "TVA 19%" ou "Exo."
    public int VatRatePercent { get; init; }
    public decimal? DiscountPercent { get; init; }
    public decimal LineTotalHt { get; init; }

    // Bons de livraison
    public decimal? OrderedQuantity { get; init; }
    public decimal? DeliveredQuantity { get; init; }
}

public sealed record VatBreakdownLine(string Label, int RatePercent, decimal BaseHt, decimal Amount);

public sealed record PaymentRenderLine(string Reference, string Date, string Mode, decimal Amount);
