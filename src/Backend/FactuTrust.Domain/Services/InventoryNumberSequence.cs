namespace FactuTrust.Domain.Services;

/// <summary>
/// Séquence pour la génération des références d'inventaire (INVE-000001).
/// Une ligne par année pour réinitialiser le compteur chaque année.
/// </summary>
public sealed class InventoryNumberSequence
{
    public int Year { get; private set; }
    public int LastSequence { get; private set; }

    private InventoryNumberSequence() { }

    public static InventoryNumberSequence Create(int year)
    {
        return new InventoryNumberSequence
        {
            Year = year,
            LastSequence = 0
        };
    }

    public int IncrementAndGet()
    {
        LastSequence++;
        return LastSequence;
    }
}
