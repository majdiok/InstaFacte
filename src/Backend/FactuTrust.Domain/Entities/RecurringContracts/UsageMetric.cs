using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.RecurringContracts;

public sealed class UsageMetric : Entity
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Unit { get; private set; } = null!;
    public UsageAggregationMode AggregationMode { get; private set; }
    public Guid? ProductId { get; private set; }
    public bool IsActive { get; private set; }

    private UsageMetric() { }

    public static Result<UsageMetric> Create(
        string code,
        string name,
        string unit,
        UsageAggregationMode aggregationMode,
        Guid? productId = null)
    {
        code = code?.Trim() ?? string.Empty;
        name = name?.Trim() ?? string.Empty;
        unit = unit?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(code))
            return Result.Failure<UsageMetric>(Error.Validation("Code", "Le code métrique est obligatoire"));
        if (string.IsNullOrEmpty(name))
            return Result.Failure<UsageMetric>(Error.Validation("Name", "Le libellé est obligatoire"));
        if (string.IsNullOrEmpty(unit))
            return Result.Failure<UsageMetric>(Error.Validation("Unit", "L'unité est obligatoire"));

        return Result.Success(new UsageMetric
        {
            Code = code.Length > 50 ? code[..50] : code,
            Name = name.Length > 200 ? name[..200] : name,
            Unit = unit.Length > 30 ? unit[..30] : unit,
            AggregationMode = aggregationMode,
            ProductId = productId,
            IsActive = true
        });
    }

    public Result Update(string name, string unit, UsageAggregationMode aggregationMode, bool isActive)
    {
        name = name?.Trim() ?? string.Empty;
        unit = unit?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
            return Result.Failure(Error.Validation("Name", "Le libellé est obligatoire"));
        if (string.IsNullOrEmpty(unit))
            return Result.Failure(Error.Validation("Unit", "L'unité est obligatoire"));

        Name = name.Length > 200 ? name[..200] : name;
        Unit = unit.Length > 30 ? unit[..30] : unit;
        AggregationMode = aggregationMode;
        IsActive = isActive;
        return Result.Success();
    }
}
