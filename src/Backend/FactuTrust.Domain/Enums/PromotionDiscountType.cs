namespace FactuTrust.Domain.Enums;

/// <summary>Forme que prend la remise d'une promotion.</summary>
public enum PromotionDiscountType
{
    /// <summary>Pourcentage du montant de la ligne.</summary>
    Percentage = 0,

    /// <summary>Montant fixe par unité vendue.</summary>
    Amount = 1
}
