namespace FactuTrust.Domain.Enums;

/// <summary>
/// Lifecycle of a company-to-accounting-firm assignment.
/// </summary>
public enum FirmAssignmentStatus
{
    PendingFirmApproval = 0,
    Active = 1,
    Rejected = 2,
    RevokedByCompany = 3,
    RevokedByFirm = 4,
    CancelledByCompany = 5
}

public static class FirmAssignmentStatusExtensions
{
    public static string ToDisplayString(this FirmAssignmentStatus status) => status switch
    {
        FirmAssignmentStatus.PendingFirmApproval => "En attente d'acceptation",
        FirmAssignmentStatus.Active => "Active",
        FirmAssignmentStatus.Rejected => "Refusée",
        FirmAssignmentStatus.RevokedByCompany => "Révoquée par la société",
        FirmAssignmentStatus.RevokedByFirm => "Résiliée par le cabinet",
        FirmAssignmentStatus.CancelledByCompany => "Annulée par la société",
        _ => status.ToString()
    };

    public static bool IsOpen(this FirmAssignmentStatus status) =>
        status is FirmAssignmentStatus.PendingFirmApproval or FirmAssignmentStatus.Active;
}
