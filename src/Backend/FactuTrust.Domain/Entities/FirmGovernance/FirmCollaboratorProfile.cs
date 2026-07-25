using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.FirmGovernance;

/// <summary>
/// Profil métier d'un collaborateur cabinet (1:1 avec ApplicationUser).
/// L'adresse est un snapshot — jamais une FK partagée avec le Tenant (évite la corruption d'adresse cabinet).
/// </summary>
public sealed class FirmCollaboratorProfile
{
    public Guid UserId { get; set; }

    public CollaboratorCivility Civility { get; set; } = CollaboratorCivility.Mr;

    public string Qualification { get; set; } = string.Empty;

    /// <summary>Téléphone fixe professionnel. Le mobile pro utilise Identity PhoneNumber.</summary>
    public string? PhoneLandline { get; set; }

    public bool UseFirmAddress { get; set; }

    public string? AddressLine { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }

    public string? CniFileName { get; set; }
    public string? CniContentType { get; set; }
    public DateTime? CniUploadedAt { get; set; }

    /// <summary>
    /// Ancien override du taux horaire de revient, conservé pour les cabinets qui l'ont alimenté.
    /// </summary>
    /// <remarks>
    /// Obsolète : le taux se pilote désormais par exercice via <c>FirmCollaboratorYearCost</c>, une
    /// valeur unique ne pouvant pas décrire plusieurs exercices sans réécrire l'historique. Cette
    /// colonne n'est plus qu'un repli, entre le taux dérivé et le taux par défaut du cabinet.
    /// </remarks>
    public decimal? HourlyCostRate { get; set; }

    /// <summary>
    /// Salarié correspondant dans la paie du cabinet, pour l'import du coût employeur.
    /// </summary>
    /// <remarks>
    /// Liaison explicite, choisie par le manager : un rapprochement automatique par nom
    /// confondrait les homonymes et imputerait un coût au mauvais collaborateur.
    /// </remarks>
    public Guid? PayrollEmployeeId { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
