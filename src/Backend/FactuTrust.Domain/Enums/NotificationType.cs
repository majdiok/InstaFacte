namespace FactuTrust.Domain.Enums;

/// <summary>
/// Type of an in-app user notification (extensible beyond the firm-assignment flow).
/// </summary>
public enum NotificationType
{
    FirmAssignmentRequested = 0,
    FirmAssignmentAccepted = 1,
    FirmAssignmentRejected = 2,
    FirmAssignmentRevoked = 3,
    FirmAssignmentCancelled = 4
}
