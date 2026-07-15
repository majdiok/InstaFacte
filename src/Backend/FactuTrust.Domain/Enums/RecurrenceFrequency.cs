namespace FactuTrust.Domain.Enums;

/// <summary>Fréquence de génération automatique d'un modèle d'écriture récurrent.</summary>
public enum RecurrenceFrequency
{
    /// <summary>Pas de récurrence : modèle purement manuel (comportement historique).</summary>
    None = 0,

    Monthly = 1,
    Quarterly = 2,
    Yearly = 3
}
