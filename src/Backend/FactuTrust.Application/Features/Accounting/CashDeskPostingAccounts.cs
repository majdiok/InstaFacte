using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Accounting;

/// <summary>
/// Comptes (SCE tunisien, catalogue <c>nct01-v1</c>) utilisés pour la comptabilisation des
/// opérations manuelles de caisse (encaissements / décaissements). Source unique de vérité sur
/// le modèle de <see cref="TunisianPostingAccounts"/> : toute génération d'écriture depuis une
/// <c>CashOperation</c> DOIT s'appuyer sur ces constantes et sur les fonctions pures ci-dessous,
/// faute de quoi le mapping catégorie → compte divergerait selon le point d'appel.
/// </summary>
public static class CashDeskPostingAccounts
{
    // ---- Décaissements (débit) -------------------------------------------------------------

    /// <summary>613 — Locations (loyers, location de véhicules).</summary>
    public const string Rent = "613";

    /// <summary>606 — Achats non stockés de matières et fournitures.</summary>
    public const string NonStockedPurchases = "606";

    /// <summary>615 — Entretien et réparations.</summary>
    public const string MaintenanceAndRepairs = "615";

    /// <summary>640 — Salaires et compléments de salaires (dette non constatée au préalable).</summary>
    public const string Salaries = "640";

    /// <summary>425 — Personnel - rémunérations dues (dette déjà constatée par l'OD de paie).</summary>
    public const string PersonnelPayable = "425";

    /// <summary>624 — Transports de biens et transports collectifs du personnel.</summary>
    public const string TransportOfGoodsAndStaff = "624";

    /// <summary>6251 — Voyages et déplacements (sous-compte de 625).</summary>
    public const string TravelAndTrips = "6251";

    /// <summary>623 — Publicité, publications, relations publiques.</summary>
    public const string Advertising = "623";

    /// <summary>622 — Rémunération d'intermédiaires et honoraires.</summary>
    public const string FeesAndCommissions = "622";

    /// <summary>616 — Primes d'assurances.</summary>
    public const string InsurancePremiums = "616";

    /// <summary>6651 — Impôts et taxes divers (sauf impôts sur les bénéfices).</summary>
    public const string MiscellaneousTaxes = "6651";

    /// <summary>63 — Charges diverses ordinaires (générique, reclassable par le comptable).</summary>
    public const string MiscellaneousOrdinaryExpenses = "63";

    // ---- Encaissements (crédit) -------------------------------------------------------------

    /// <summary>446 — Associés - opérations sur le capital.</summary>
    public const string PartnersCapitalOperations = "446";

    /// <summary>5321 — Comptes en dinars (banque).</summary>
    public const string BankInDinars = "5321";

    /// <summary>73 — Produits divers ordinaires (évite de gonfler le chiffre d'affaires).</summary>
    public const string MiscellaneousOrdinaryIncome = "73";

    // ---- Trésorerie ---------------------------------------------------------------------------

    /// <summary>5411 — Caisse en dinars.</summary>
    public const string CashInDinars = "5411";

    /// <summary>
    /// Compte de charge imputé pour un décaissement de caisse selon la catégorie métier.
    /// Switch exhaustif sur les 17 valeurs de <see cref="CashExpenseCategory"/> ;
    /// <see cref="CashExpenseCategory.BankDeposit"/> ne doit jamais atteindre ce point (skip en
    /// amont, garde interne de <c>AccountingService.GenerateCashOperationEntryAsync</c>) : lève
    /// une exception si c'est le cas — bruyant plutôt qu'une écriture fausse.
    /// </summary>
    /// <param name="category">Catégorie de dépense de l'opération de caisse.</param>
    /// <param name="payrollCycleExists">
    /// Vrai si un cycle de paie comptabilisé (écriture active <c>SourceEntityType = "PayrollRun"</c>)
    /// existe pour le dossier — n'est utile que pour <see cref="CashExpenseCategory.NetSalaries"/>.
    /// </param>
    public static string ExpenseAccount(CashExpenseCategory category, bool payrollCycleExists) => category switch
    {
        CashExpenseCategory.RentPayment => Rent,
        CashExpenseCategory.SuppliesAndConsumables => NonStockedPurchases,
        CashExpenseCategory.MaintenanceAndRepair => MaintenanceAndRepairs,
        CashExpenseCategory.NetSalaries => payrollCycleExists ? PersonnelPayable : Salaries,
        CashExpenseCategory.TransportCosts => TransportOfGoodsAndStaff,
        CashExpenseCategory.TravelAndTrips => TravelAndTrips,
        CashExpenseCategory.VehicleRepairMaintenance => MaintenanceAndRepairs,
        CashExpenseCategory.VehicleRentalAndTransport => Rent,
        CashExpenseCategory.UtilitiesAndEnergy => NonStockedPurchases,
        CashExpenseCategory.ProfessionalFees => FeesAndCommissions,
        CashExpenseCategory.Insurance => InsurancePremiums,
        CashExpenseCategory.TaxesAndDuties => MiscellaneousTaxes,
        CashExpenseCategory.MarketingAdvertising => Advertising,
        CashExpenseCategory.ITAndSoftware => TunisianPostingAccounts.PurchasesOfServices,
        CashExpenseCategory.SupplierInvoicePayment => TunisianPostingAccounts.Supplier,
        CashExpenseCategory.Other => MiscellaneousOrdinaryExpenses,
        CashExpenseCategory.BankDeposit => throw new ArgumentOutOfRangeException(
            nameof(category), category, "BankDeposit ne génère jamais d'écriture (skip en amont)."),
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Catégorie de dépense caisse inconnue.")
    };

    /// <summary>
    /// Compte de produit crédité pour un encaissement de caisse selon la catégorie métier.
    /// Switch exhaustif sur les 5 valeurs de <see cref="CashRevenueCategory"/>.
    /// </summary>
    public static string RevenueAccount(CashRevenueCategory category) => category switch
    {
        CashRevenueCategory.CashSalesReceipt => TunisianPostingAccounts.SalesOfGoods,
        CashRevenueCategory.ClientReceivablesReceipt => TunisianPostingAccounts.Client,
        CashRevenueCategory.PartnerContributionsReceipt => PartnersCapitalOperations,
        CashRevenueCategory.BankCreditReceipt => BankInDinars,
        CashRevenueCategory.Other => MiscellaneousOrdinaryIncome,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Catégorie de revenu caisse inconnue.")
    };

    /// <summary>
    /// Compte de trésorerie débité/crédité en contrepartie selon le moyen de paiement.
    /// <see cref="PaymentMethod.Cash"/> → 5411 ; tout autre moyen accepté (virement, chèque,
    /// carte, paiement mobile, autre) → 5321. <see cref="PaymentMethod.Traite"/> est refusée en
    /// amont par <c>CashOperation.Create</c> — toute arrivée ici est un bug qui doit être bruyant,
    /// pas une écriture fausse sur 5411/5321.
    /// </summary>
    public static string TreasuryAccount(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => CashInDinars,
        PaymentMethod.BankTransfer or PaymentMethod.Check or PaymentMethod.Card
            or PaymentMethod.MobilePayment or PaymentMethod.Other => BankInDinars,
        PaymentMethod.Traite => throw new ArgumentOutOfRangeException(
            nameof(method), method, "La traite n'a pas de contrepartie trésorerie immédiate en caisse manuelle (413/403, refusée en amont)."),
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Moyen de paiement inconnu.")
    };
}
