namespace FactuTrust.Domain.Enums;

/// <summary>Statut de suivi d'une anomalie comptable détectée.</summary>
public enum AnomalyStatus
{
    Open = 0,
    InProgress = 1,
    Watch = 2,
    Corrected = 3,
    Ignored = 4
}
