using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common;

/// <summary>
/// Scope d'accès dossier client pour un utilisateur cabinet.
/// FirmManager = tous les dossiers actifs ; FirmAccountant = dossiers affectés uniquement.
/// </summary>
public readonly record struct FirmDossierAccessScope(Guid UserId, string Role)
{
    public static FirmDossierAccessScope ForUser(Guid userId, string role) => new(userId, role);

    public static FirmDossierAccessScope ForUser(Guid userId, UserRole role) => new(userId, role.ToString());

    public bool IsFirmManager =>
        string.Equals(Role, nameof(UserRole.FirmManager), StringComparison.OrdinalIgnoreCase);

    public bool IsFirmAccountant =>
        string.Equals(Role, nameof(UserRole.FirmAccountant), StringComparison.OrdinalIgnoreCase);

    public bool RequiresAccountantAssignmentFilter => IsFirmAccountant;
}
