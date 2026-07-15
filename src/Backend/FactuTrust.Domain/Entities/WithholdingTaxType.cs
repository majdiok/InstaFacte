using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Catalogue entry mapping a TEJ operation code (e.g. RS1_000001) to its category,
/// default rate, and applicable scope. Seeded from the official TEJ XSD codes.
/// </summary>
public sealed class WithholdingTaxType : AggregateRoot
{
    public string Code { get; private set; } = null!;
    public WithholdingCategory Category { get; private set; }
    public string Label { get; private set; } = null!;
    public string? LabelAr { get; private set; }
    public decimal DefaultRate { get; private set; }
    public string? ArticleReference { get; private set; }
    public bool ApplicableToResident { get; private set; }
    public bool ApplicableToNonResident { get; private set; }
    public decimal? MinimumThreshold { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsSystem { get; private set; }
    public int DisplayOrder { get; private set; }

    private WithholdingTaxType() { }

    public static WithholdingTaxType CreateSystem(
        string code,
        WithholdingCategory category,
        string label,
        decimal defaultRate,
        string? articleReference = null,
        bool applicableToResident = true,
        bool applicableToNonResident = false,
        decimal? minimumThreshold = null,
        string? labelAr = null,
        int displayOrder = 0)
    {
        return new WithholdingTaxType
        {
            Code = code,
            Category = category,
            Label = label,
            LabelAr = labelAr,
            DefaultRate = defaultRate,
            ArticleReference = articleReference,
            ApplicableToResident = applicableToResident,
            ApplicableToNonResident = applicableToNonResident,
            MinimumThreshold = minimumThreshold,
            IsActive = true,
            IsSystem = true,
            DisplayOrder = displayOrder
        };
    }

    public static Result<WithholdingTaxType> Create(
        string code,
        WithholdingCategory category,
        string label,
        decimal defaultRate,
        string? articleReference = null,
        bool applicableToResident = true,
        bool applicableToNonResident = false,
        decimal? minimumThreshold = null,
        string? labelAr = null,
        int displayOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Result.Failure<WithholdingTaxType>(Error.Validation("Code", "Le code d'opération TEJ est obligatoire"));

        if (string.IsNullOrWhiteSpace(label))
            return Result.Failure<WithholdingTaxType>(Error.Validation("Label", "Le libellé est obligatoire"));

        if (defaultRate < 0 || defaultRate > 100)
            return Result.Failure<WithholdingTaxType>(Error.Validation("DefaultRate", "Le taux doit être compris entre 0 et 100"));

        return Result.Success(new WithholdingTaxType
        {
            Code = code.Trim(),
            Category = category,
            Label = label.Trim(),
            LabelAr = labelAr?.Trim(),
            DefaultRate = defaultRate,
            ArticleReference = articleReference?.Trim(),
            ApplicableToResident = applicableToResident,
            ApplicableToNonResident = applicableToNonResident,
            MinimumThreshold = minimumThreshold,
            IsActive = true,
            IsSystem = false,
            DisplayOrder = displayOrder
        });
    }

    public void Update(string label, decimal defaultRate, string? articleReference, string? labelAr, int displayOrder)
    {
        if (IsSystem) return;

        if (!string.IsNullOrWhiteSpace(label))
            Label = label.Trim();

        if (defaultRate >= 0 && defaultRate <= 100)
            DefaultRate = defaultRate;

        ArticleReference = articleReference?.Trim();
        LabelAr = labelAr?.Trim();
        DisplayOrder = displayOrder;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }
}
