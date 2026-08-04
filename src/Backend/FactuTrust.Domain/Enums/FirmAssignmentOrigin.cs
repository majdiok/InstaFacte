namespace FactuTrust.Domain.Enums;

/// <summary>
/// Origin of a company-to-accounting-firm assignment.
/// </summary>
public enum FirmAssignmentOrigin
{
    /// <summary>Demande initiée par la société cliente (flux classique).</summary>
    CompanyRequest = 0,

    /// <summary>Dossier créé directement par le cabinet (client sans compte plateforme).</summary>
    FirmCreated = 1
}
