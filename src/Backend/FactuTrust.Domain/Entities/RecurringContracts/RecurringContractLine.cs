using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.RecurringContracts;

public sealed class RecurringContractLine : Entity
{
    public Guid RecurringContractId { get; private set; }
    public RecurringContractLineType LineType { get; private set; }
    public Guid? ProductId { get; private set; }
    public string Description { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public decimal UnitPriceHT { get; private set; }
    public decimal VatRate { get; private set; }
    public Guid? UsageMetricId { get; private set; }
    public decimal? IncludedQuantity { get; private set; }
    public decimal? OverageUnitPriceHT { get; private set; }
    public DateTime EffectiveFrom { get; private set; }
    public DateTime? EffectiveTo { get; private set; }
    public bool IsActive { get; private set; }
    public int SortOrder { get; private set; }

    private RecurringContractLine() { }

    public static Result<RecurringContractLine> Create(
        Guid recurringContractId,
        RecurringContractLineType lineType,
        string description,
        decimal quantity,
        decimal unitPriceHt,
        decimal vatRate,
        DateTime effectiveFrom,
        Guid? productId = null,
        Guid? usageMetricId = null,
        decimal? includedQuantity = null,
        decimal? overageUnitPriceHt = null,
        int sortOrder = 0)
    {
        description = description?.Trim() ?? string.Empty;
        if (recurringContractId == Guid.Empty)
            return Result.Failure<RecurringContractLine>(Error.Validation("RecurringContractId", "Le contrat est obligatoire"));
        if (string.IsNullOrEmpty(description))
            return Result.Failure<RecurringContractLine>(Error.Validation("Description", "La description est obligatoire"));
        if (quantity < 0)
            return Result.Failure<RecurringContractLine>(Error.Validation("Quantity", "La quantité ne peut pas être négative"));
        if (unitPriceHt < 0)
            return Result.Failure<RecurringContractLine>(Error.Validation("UnitPriceHT", "Le prix unitaire ne peut pas être négatif"));
        if (lineType == RecurringContractLineType.UsageMetered && usageMetricId is null)
            return Result.Failure<RecurringContractLine>(Error.Validation("UsageMetricId", "La métrique d'usage est obligatoire pour une ligne à consommation"));

        return Result.Success(new RecurringContractLine
        {
            RecurringContractId = recurringContractId,
            LineType = lineType,
            ProductId = productId,
            Description = description.Length > 500 ? description[..500] : description,
            Quantity = decimal.Round(quantity, 3, MidpointRounding.AwayFromZero),
            UnitPriceHT = decimal.Round(unitPriceHt, 3, MidpointRounding.AwayFromZero),
            VatRate = decimal.Round(vatRate, 2, MidpointRounding.AwayFromZero),
            UsageMetricId = usageMetricId,
            IncludedQuantity = includedQuantity.HasValue
                ? decimal.Round(includedQuantity.Value, 3, MidpointRounding.AwayFromZero)
                : null,
            OverageUnitPriceHT = overageUnitPriceHt.HasValue
                ? decimal.Round(overageUnitPriceHt.Value, 3, MidpointRounding.AwayFromZero)
                : null,
            EffectiveFrom = effectiveFrom.Date,
            SortOrder = sortOrder,
            IsActive = true
        });
    }

    public Result Update(
        string description,
        decimal quantity,
        decimal unitPriceHt,
        decimal vatRate,
        decimal? includedQuantity,
        decimal? overageUnitPriceHt,
        int sortOrder)
    {
        description = description?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(description))
            return Result.Failure(Error.Validation("Description", "La description est obligatoire"));
        if (quantity < 0)
            return Result.Failure(Error.Validation("Quantity", "La quantité ne peut pas être négative"));
        if (unitPriceHt < 0)
            return Result.Failure(Error.Validation("UnitPriceHT", "Le prix unitaire ne peut pas être négatif"));

        Description = description.Length > 500 ? description[..500] : description;
        Quantity = decimal.Round(quantity, 3, MidpointRounding.AwayFromZero);
        UnitPriceHT = decimal.Round(unitPriceHt, 3, MidpointRounding.AwayFromZero);
        VatRate = decimal.Round(vatRate, 2, MidpointRounding.AwayFromZero);
        IncludedQuantity = includedQuantity.HasValue
            ? decimal.Round(includedQuantity.Value, 3, MidpointRounding.AwayFromZero)
            : null;
        OverageUnitPriceHT = overageUnitPriceHt.HasValue
            ? decimal.Round(overageUnitPriceHt.Value, 3, MidpointRounding.AwayFromZero)
            : null;
        SortOrder = sortOrder;
        return Result.Success();
    }

    public void Deactivate(DateTime effectiveTo)
    {
        EffectiveTo = effectiveTo.Date;
        IsActive = false;
    }

    public bool IsEffectiveOn(DateTime date)
    {
        var d = date.Date;
        return IsActive && d >= EffectiveFrom.Date && (!EffectiveTo.HasValue || d <= EffectiveTo.Value.Date);
    }
}
