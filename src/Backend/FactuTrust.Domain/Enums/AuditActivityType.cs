namespace FactuTrust.Domain.Enums;

/// <summary>Type d'événement dans la timeline d'une anomalie.</summary>
public enum AuditActivityType
{
    Detected = 0,
    Assigned = 1,
    Commented = 2,
    StatusChanged = 3,
    Ignored = 4,
    Corrected = 5
}
