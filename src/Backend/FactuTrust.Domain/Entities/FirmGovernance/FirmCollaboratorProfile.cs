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
    public Guid? PayrollEmployeeId { get; set; }

    /// <summary>Comment la liaison paie a été établie (manuelle ou rapprochement email).</summary>
    public FirmPayrollLinkSource PayrollLinkSource { get; set; } = FirmPayrollLinkSource.None;

    /// <summary>Horodatage de la dernière liaison (manuelle ou automatique).</summary>
    public DateTime? PayrollLinkedAt { get; set; }

    /// <summary>
    /// Entrée du collaborateur dans le cabinet, pour proratiser sa présence sur un exercice.
    /// </summary>
    /// <remarks>
    /// Nulle par défaut, auquel cas le collaborateur est réputé présent toute l'année — les
    /// exercices déjà analysés conservent donc exactement leurs heures productives. Délibérément
    /// portée ici plutôt que dérivée du salarié de paie : la rentabilité ne doit pas dépendre de
    /// la disponibilité de la base de paie.
    /// </remarks>
    public DateTime? HiredOn { get; set; }

    /// <summary>Sortie du collaborateur. Nulle tant qu'il est en poste.</summary>
    public DateTime? LeftOn { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
