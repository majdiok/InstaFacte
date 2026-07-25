namespace FactuTrust.Domain.Entities.FirmGovernance;

/// <summary>
/// Lien de rattachement / binôme entre collaborateurs du même cabinet.
/// <see cref="IsSecondManager"/> = binôme (second manager), comme UserLink.IsSecondManager côté Décisiel.
/// </summary>
public sealed class FirmCollaboratorLink
{
    public Guid Id { get; set; }

    public Guid ParentUserId { get; set; }

    public Guid ChildUserId { get; set; }

    public bool IsSecondManager { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
