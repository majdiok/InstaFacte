namespace FactuTrust.Application.Configuration;

public sealed class FirmGovernanceOptions
{
    public const string SectionName = "Features:FirmGovernance";

    public bool Enabled { get; set; }

    /// <summary>
    /// Taux horaire de revient cabinet (Décisiel FeuilleTempsPrixHoraire). Défaut 50.
    /// </summary>
    public decimal DefaultHourlyCostRate { get; set; } = 50m;

    /// <summary>Rapprochement automatique collaborateur ↔ salarié paie par email identique.</summary>
    public bool AutoLinkCollaboratorsByEmail { get; set; } = true;

    /// <summary>Import des coûts employeur après validation d'une paie cabinet (tenant natif).</summary>
    public bool AutoImportOnPayrollValidate { get; set; } = true;

    /// <summary>Sync silencieuse avant le préremplissage de la rentabilité collaborateur.</summary>
    public bool SilentImportBeforeRentabilityPrefill { get; set; } = true;

    /// <summary>Active la paie interne du cabinet (module slim sous /firm/payroll).</summary>
    public bool EnableFirmInternalPayroll { get; set; }

    /// <summary>Crée automatiquement un salarié paie à la création d'un collaborateur cabinet.</summary>
    public bool AutoProvisionPayrollOnCollaboratorCreate { get; set; }

    /// <summary>
    /// Salaire de base du contrat créé au provisionnement, quand aucun n'est fourni.
    /// </summary>
    /// <remarks>
    /// Simple amorce : le cabinet corrige ensuite le contrat depuis la fiche salarié. La valeur par
    /// défaut reprend celle qui était codée en dur, pour ne pas modifier les provisionnements en place.
    /// </remarks>
    public decimal DefaultProvisionBaseSalary { get; set; } = 1_500m;
}

