namespace FactuTrust.Domain.Enums;

/// <summary>
/// Références de rentabilité collaborateur (miroir Décisiel RentabilityReferenceEnum utile).
/// </summary>
public enum FirmRentabilityReference
{
    PayrollCost = 1,
    PayrollGross = 2,
    EmployerContributions = 3,
    PayrollExtras = 4,
    AdminPayrollCharge = 5,
    ItManagementCharge = 8,
    OperatingCharge = 11,
    TotalRevenue = 13,
    PortfolioCount = 14,
    ClientCreditBalance = 15,
    ClientDebitBalance = 16,
    AttachedCollaboratorsCount = 18
}
