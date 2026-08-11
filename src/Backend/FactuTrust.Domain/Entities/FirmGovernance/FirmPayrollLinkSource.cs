namespace FactuTrust.Domain.Entities.FirmGovernance;

/// <summary>Origine de la liaison entre un collaborateur cabinet et un salarié de la paie interne.</summary>
public enum FirmPayrollLinkSource
{
    None = 0,
    Manual = 1,
    AutoEmail = 2,
    ProvisionedFromCollaborator = 3
}
