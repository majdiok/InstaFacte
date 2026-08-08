namespace FactuTrust.Domain.Enums;

/// <summary>Catégorie métier d'une anomalie (alignée sur les modules de contrôle).</summary>
public enum AnomalyCategory
{
    Integrite = 0,
    Ecritures = 1,
    Comptes = 2,
    Lettrage = 3,
    Rapprochement = 4,
    Tva = 5,
    Analytique = 6,
    Fiscalite = 7,
    Immobilisations = 8,
    Budgetaire = 9,
    Liasse = 10,
    Banque = 11,
    Achats = 12,
    Ventes = 13,
    Paie = 14,
    Tresorerie = 15,
    Documents = 16
}
