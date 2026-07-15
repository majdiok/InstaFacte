using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Links a company tenant to an accounting firm tenant with invitation workflow.
/// </summary>
public sealed class FirmClientAssignment : AggregateRoot
{
    public Guid CompanyTenantId { get; private set; }
    public Guid FirmTenantId { get; private set; }
    public FirmAssignmentStatus Status { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public Guid? RespondedByUserId { get; private set; }
    public DateTime? RespondedAt { get; private set; }
    public Guid? RevokedByUserId { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? Notes { get; private set; }

    private FirmClientAssignment() { }

    public static Result<FirmClientAssignment> Request(
        Guid companyTenantId,
        Guid firmTenantId,
        Guid requestedByUserId,
        string? notes = null)
    {
        if (companyTenantId == Guid.Empty || firmTenantId == Guid.Empty)
            return Result.Failure<FirmClientAssignment>(Error.Validation("Tenant", "Identifiants tenant invalides"));

        if (companyTenantId == firmTenantId)
            return Result.Failure<FirmClientAssignment>(Error.Validation("Tenant", "Une société ne peut pas s'affecter elle-même"));

        if (requestedByUserId == Guid.Empty)
            return Result.Failure<FirmClientAssignment>(Error.Validation("User", "Utilisateur demandeur invalide"));

        return Result.Success(new FirmClientAssignment
        {
            CompanyTenantId = companyTenantId,
            FirmTenantId = firmTenantId,
            Status = FirmAssignmentStatus.PendingFirmApproval,
            RequestedByUserId = requestedByUserId,
            RequestedAt = DateTime.UtcNow,
            Notes = notes?.Trim()
        });
    }

    public Result Accept(Guid respondedByUserId)
    {
        if (Status != FirmAssignmentStatus.PendingFirmApproval)
            return Result.Failure(Error.Validation("Status", "Seule une demande en attente peut être acceptée"));

        if (respondedByUserId == Guid.Empty)
            return Result.Failure(Error.Validation("User", "Utilisateur invalide"));

        Status = FirmAssignmentStatus.Active;
        RespondedByUserId = respondedByUserId;
        RespondedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result Reject(Guid respondedByUserId)
    {
        if (Status != FirmAssignmentStatus.PendingFirmApproval)
            return Result.Failure(Error.Validation("Status", "Seule une demande en attente peut être refusée"));

        if (respondedByUserId == Guid.Empty)
            return Result.Failure(Error.Validation("User", "Utilisateur invalide"));

        Status = FirmAssignmentStatus.Rejected;
        RespondedByUserId = respondedByUserId;
        RespondedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result RevokeByCompany(Guid revokedByUserId)
    {
        if (Status != FirmAssignmentStatus.Active)
            return Result.Failure(Error.Validation("Status", "Seule une affectation active peut être révoquée"));

        Status = FirmAssignmentStatus.RevokedByCompany;
        RevokedByUserId = revokedByUserId;
        RevokedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result RevokeByFirm(Guid revokedByUserId)
    {
        if (Status != FirmAssignmentStatus.Active)
            return Result.Failure(Error.Validation("Status", "Seule une affectation active peut être résiliée"));

        Status = FirmAssignmentStatus.RevokedByFirm;
        RevokedByUserId = revokedByUserId;
        RevokedAt = DateTime.UtcNow;
        return Result.Success();
    }
}
