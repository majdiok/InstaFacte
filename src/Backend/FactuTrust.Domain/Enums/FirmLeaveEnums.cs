namespace FactuTrust.Domain.Enums;

/// <summary>Statut d'une demande de congé / absence cabinet.</summary>
public enum FirmLeaveRequestStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4
}

/// <summary>Unité de journée pour les demi-journées.</summary>
public enum FirmLeaveDayUnit
{
    FullDay = 0,
    Morning = 1,
    Afternoon = 2
}
