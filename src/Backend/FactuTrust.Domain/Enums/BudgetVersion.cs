namespace FactuTrust.Domain.Enums;

/// <summary>
/// Version d'un budget : l'<see cref="Initial"/> est saisi librement puis figé par validation
/// (copié vers <see cref="Revised"/> qui devient la seule version modifiable).
/// </summary>
public enum BudgetVersion
{
    Initial = 0,
    Revised = 1
}
