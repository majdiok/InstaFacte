namespace FactuTrust.Domain.Enums;

/// <summary>
/// Types d'écritures d'inventaire de fin d'exercice (assistant guidé). Les régularisations de
/// périodicité (CCA/PCA/CAP/PAR) sont extournées au nouvel exercice ; les provisions et la
/// variation de stocks sont permanentes.
/// </summary>
public enum InventoryEntryKind
{
    /// <summary>Charges constatées d'avance (D 471 / C classe 6) — extournée.</summary>
    ChargeConstateeDavance = 0,

    /// <summary>Produits constatés d'avance (D classe 7 / C 472) — extournée.</summary>
    ProduitConstateDavance = 1,

    /// <summary>Charges à payer (D classe 6 / C 408/438) — extournée.</summary>
    ChargeAPayer = 2,

    /// <summary>Produits à recevoir (D 418/468 / C classe 7) — extournée.</summary>
    ProduitARecevoir = 3,

    /// <summary>Provision pour dépréciation (D 68x / C 29x/39x/49x) — permanente.</summary>
    ProvisionDepreciation = 4,

    /// <summary>Variation de stocks (603/713 vs classe 3) — permanente.</summary>
    VariationStock = 5
}
