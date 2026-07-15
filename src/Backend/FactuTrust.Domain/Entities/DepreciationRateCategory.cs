using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Tunisian fiscal depreciation rate reference (seed from Taux-Amortissements).
/// </summary>
public sealed class DepreciationRateCategory : Entity
{
    public string Code { get; private set; } = null!;
    public string Label { get; private set; } = null!;
    public decimal LegalRatePercent { get; private set; }
    public string DefaultAssetAccount { get; private set; } = null!;
    public string DefaultDepreciationAccount { get; private set; } = null!;
    public string DefaultExpenseAccount { get; private set; } = null!;
    public bool IsNonDepreciable { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    private DepreciationRateCategory() { }

    public static DepreciationRateCategory Create(
        string code,
        string label,
        decimal legalRatePercent,
        string defaultAssetAccount,
        string defaultDepreciationAccount,
        string defaultExpenseAccount,
        bool isNonDepreciable,
        int sortOrder)
    {
        code = code?.Trim().ToUpperInvariant() ?? string.Empty;
        label = label?.Trim() ?? string.Empty;
        defaultAssetAccount = defaultAssetAccount?.Trim() ?? string.Empty;
        defaultDepreciationAccount = defaultDepreciationAccount?.Trim() ?? string.Empty;
        defaultExpenseAccount = defaultExpenseAccount?.Trim() ?? string.Empty;

        return new DepreciationRateCategory
        {
            Code = code,
            Label = label,
            LegalRatePercent = legalRatePercent,
            DefaultAssetAccount = defaultAssetAccount,
            DefaultDepreciationAccount = defaultDepreciationAccount,
            DefaultExpenseAccount = defaultExpenseAccount,
            IsNonDepreciable = isNonDepreciable,
            SortOrder = sortOrder,
            IsActive = true
        };
    }

    public decimal UsefulLifeYears =>
        IsNonDepreciable || LegalRatePercent <= 0 ? 0 : Math.Round(100m / LegalRatePercent, 2);
}