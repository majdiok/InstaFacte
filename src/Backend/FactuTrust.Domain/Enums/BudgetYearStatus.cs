namespace FactuTrust.Domain.Enums;

/// <summary>
/// Statut du budget d'un exercice : brouillon (l'Initial est modifiable) puis validé
/// (l'Initial est figé, seule la version révisée est modifiable).
/// </summary>
public enum BudgetYearStatus
{
    Draft = 0,
    Validated = 1
}
