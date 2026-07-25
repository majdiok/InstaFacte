namespace FactuTrust.Application.Configuration;

public sealed class FirmGovernanceOptions
{
    public const string SectionName = "Features:FirmGovernance";

    public bool Enabled { get; set; }

    /// <summary>
    /// Taux horaire de revient cabinet (Décisiel FeuilleTempsPrixHoraire). Défaut 50.
    /// </summary>
    public decimal DefaultHourlyCostRate { get; set; } = 50m;
}

