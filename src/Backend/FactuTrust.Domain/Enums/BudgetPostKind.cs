namespace FactuTrust.Domain.Enums;

/// <summary>
/// Sens d'un poste budgétaire : détermine le calcul du réalisé à partir des lignes d'écriture
/// (charges = débit − crédit ; produits = crédit − débit).
/// </summary>
public enum BudgetPostKind
{
    /// <summary>Poste de charges (classe 6 en général).</summary>
    Expense = 0,

    /// <summary>Poste de produits (classe 7 en général).</summary>
    Revenue = 1
}
