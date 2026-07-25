namespace FactuTrust.Domain.Entities.FirmGovernance;

/// <summary>
/// Historique d'affectation d'un dossier client à un gestionnaire comptable (FirmAccountant).
/// <see cref="EndedAt"/> null = affectation courante.
/// </summary>
public sealed class FirmDossierAssignmentHistory
{
    public Guid Id { get; set; }
    public Guid FirmTenantId { get; set; }
    public Guid FirmClientAssignmentId { get; set; }
    public Guid CompanyTenantId { get; set; }
    public Guid? AccountantUserId { get; set; }
    public string? AccountantDisplayName { get; set; }
    public Guid AssignedByUserId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
}
