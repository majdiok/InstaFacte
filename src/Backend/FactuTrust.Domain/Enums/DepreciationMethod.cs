namespace FactuTrust.Domain.Enums;

public enum DepreciationMethod
{
    /// <summary>Amortissement linéaire (constant), prorata temporis en jours base 360 — règle de droit commun.</summary>
    Linear = 0,

    /// <summary>
    /// Amortissement accéléré (Décret 2008-492, art. 2) : taux linéaire × coefficient 1,5 (deux équipes)
    /// ou 2 (trois équipes). Annuité constante sur la base amortissable, réservé au matériel
    /// industriel des industries manufacturières non saisonnières fonctionnant en équipes multiples.
    /// </summary>
    Accelerated = 1,

    /// <summary>
    /// Amortissement intégral (Décret 2008-492, art. 4) : dotation unique de la totalité de la base
    /// l'exercice de mise en service — réservé aux actifs de faible valeur (≤ 200 DT).
    /// </summary>
    Integral = 2
}
