namespace FactuTrust.Application.Features.FixedAssets;

/// <summary>
/// Single shared resolution for "is the acquisition VAT capitalized into the asset's cost?" (NCT A5).
/// Passenger vehicles (véhicules de tourisme) do not entitle the VAT to deduction on account 43662: the
/// VAT must be capitalized into the asset's cost instead. Used by both the manual fixed-asset flow
/// (<c>CreateFixedAssetCommandHandler</c>/<c>UpdateFixedAssetCommandHandler</c>, which already load the
/// category) and the supplier-invoice flow (<c>CreateFixedAssetsFromSupplierInvoiceHandler</c> and the
/// journal-entry posting preparation in <c>AccountingService</c>), so both flows stay in sync.
/// </summary>
public static class FixedAssetVatRules
{
    /// <summary>
    /// Passenger-vehicle depreciation category code (véhicules de tourisme, NCT — 20 % / 5 ans).
    /// </summary>
    public const string PassengerVehicleCategoryCode = "VEH_PASS";

    /// <summary>
    /// NCT sub-account for "matériel de transport de personnes" (passenger vehicles).
    /// </summary>
    public const string PassengerVehicleAssetAccountPrefix = "2244";

    /// <summary>
    /// Resolves whether the acquisition VAT must be capitalized into the asset's cost instead of being
    /// posted as deductible VAT on 43662 — true for passenger vehicles (category code VEH_PASS, or an
    /// effective asset account under 2244).
    /// </summary>
    public static bool IsVatCapitalized(string? categoryCode, string? assetAccountNumber) =>
        string.Equals(categoryCode, PassengerVehicleCategoryCode, StringComparison.OrdinalIgnoreCase)
        || (assetAccountNumber?.StartsWith(PassengerVehicleAssetAccountPrefix, StringComparison.Ordinal) ?? false);
}
