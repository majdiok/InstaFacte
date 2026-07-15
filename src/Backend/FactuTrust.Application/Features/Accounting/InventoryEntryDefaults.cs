using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Accounting;

/// <summary>Comptes par défaut (SCE tunisien) et politique d'extourne par type d'écriture d'inventaire.</summary>
public readonly record struct InventoryEntryDefault(
    InventoryEntryKind Kind,
    string Label,
    string DefaultDebitAccount,
    string DefaultCreditAccount,
    bool AutoReverse,
    string Hint);

public static class InventoryEntryDefaults
{
    public static readonly IReadOnlyList<InventoryEntryDefault> All = new[]
    {
        new InventoryEntryDefault(InventoryEntryKind.ChargeConstateeDavance,
            "Charges constatées d'avance", "471", "61",
            AutoReverse: true,
            "Part d'une charge déjà comptabilisée qui concerne l'exercice suivant. Extournée au nouvel exercice."),
        new InventoryEntryDefault(InventoryEntryKind.ProduitConstateDavance,
            "Produits constatés d'avance", "70", "472",
            AutoReverse: true,
            "Part d'un produit déjà comptabilisé qui concerne l'exercice suivant. Extournée au nouvel exercice."),
        new InventoryEntryDefault(InventoryEntryKind.ChargeAPayer,
            "Charges à payer", "61", "408",
            AutoReverse: true,
            "Charge de l'exercice non encore facturée. Extournée au nouvel exercice."),
        new InventoryEntryDefault(InventoryEntryKind.ProduitARecevoir,
            "Produits à recevoir", "418", "70",
            AutoReverse: true,
            "Produit de l'exercice non encore facturé. Extourné au nouvel exercice."),
        new InventoryEntryDefault(InventoryEntryKind.ProvisionDepreciation,
            "Provision pour dépréciation", "681", "491",
            AutoReverse: false,
            "Dépréciation d'un actif (créance, stock…). Écriture permanente, non extournée."),
        new InventoryEntryDefault(InventoryEntryKind.VariationStock,
            "Variation de stocks", "603", "31",
            AutoReverse: false,
            "Ajustement du stock final vs initial. Écriture permanente.")
    };

    public static InventoryEntryDefault? Find(InventoryEntryKind kind)
    {
        foreach (var d in All)
            if (d.Kind == kind)
                return d;
        return null;
    }
}
