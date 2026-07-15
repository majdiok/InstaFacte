namespace FactuTrust.Domain.Enums;

/// <summary>
/// Fixed vocabulary for cash desk expense classification (display labels in French).
/// </summary>
public enum CashExpenseCategory
{
    RentPayment = 0,
    SuppliesAndConsumables = 1,
    MaintenanceAndRepair = 2,
    NetSalaries = 3,
    TransportCosts = 4,
    TravelAndTrips = 5,
    VehicleRepairMaintenance = 6,
    VehicleRentalAndTransport = 7,
    UtilitiesAndEnergy = 8,
    ProfessionalFees = 9,
    Insurance = 10,
    TaxesAndDuties = 11,
    MarketingAdvertising = 12,
    ITAndSoftware = 13,
    BankDeposit = 14,

    /// <summary>
    /// Cash outflow tied to paying a supplier invoice (trade payables).
    /// </summary>
    SupplierInvoicePayment = 15,

    Other = 99
}

public static class CashExpenseCategoryExtensions
{
    public static string ToDisplayString(this CashExpenseCategory category) => category switch
    {
        CashExpenseCategory.RentPayment => "Paiement des loyers",
        CashExpenseCategory.SuppliesAndConsumables =>
            "Règlement des factures d'achats de fournitures et consommables",
        CashExpenseCategory.MaintenanceAndRepair => "Paiement des factures d'entretien et réparation",
        CashExpenseCategory.NetSalaries => "Paiement des salaires nets",
        CashExpenseCategory.TransportCosts => "Paiement des frais de transport",
        CashExpenseCategory.TravelAndTrips => "Voyages et déplacements",
        CashExpenseCategory.VehicleRepairMaintenance => "Véhicule (réparation, entretien)",
        CashExpenseCategory.VehicleRentalAndTransport => "Location véhicules et transport",
        CashExpenseCategory.UtilitiesAndEnergy => "Charges locatives et énergie (eau, électricité, gaz)",
        CashExpenseCategory.ProfessionalFees => "Honoraires et prestations externes",
        CashExpenseCategory.Insurance => "Assurances",
        CashExpenseCategory.TaxesAndDuties => "Impôts, taxes et redevances",
        CashExpenseCategory.MarketingAdvertising => "Marketing et publicité",
        CashExpenseCategory.ITAndSoftware => "Informatique et logiciels",
        CashExpenseCategory.BankDeposit => "Remise en banque",
        CashExpenseCategory.SupplierInvoicePayment => "Règlement facture fournisseur",
        CashExpenseCategory.Other => "Autre",
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };
}
