using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Configurable tax rule (TVA, timbre fiscal, FODEC, etc.) for the tenant catalog.
/// </summary>
public sealed class Tax : AggregateRoot
{
    public string Name { get; private set; } = null!;
    public TaxType Type { get; private set; }
    public TaxValueType ValueType { get; private set; }
    public decimal Value { get; private set; }
    public TaxContext Context { get; private set; }
    public bool IsAppliedToProducts { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsSystem { get; private set; }
    public int DisplayOrder { get; private set; }

    private Tax() { }

    public static Result<Tax> Create(
        string name,
        TaxType type,
        TaxValueType valueType,
        decimal value,
        TaxContext context,
        bool isAppliedToProducts,
        bool isSystem,
        int displayOrder)
    {
        var nameValidation = ValidateName(name);
        if (nameValidation.IsFailure)
            return Result.Failure<Tax>(nameValidation.Error);

        var valueValidation = ValidateValue(valueType, value, type, isSystem);
        if (valueValidation.IsFailure)
            return Result.Failure<Tax>(valueValidation.Error);

        var tax = new Tax
        {
            Name = name.Trim(),
            Type = type,
            ValueType = valueType,
            Value = value,
            Context = context,
            IsAppliedToProducts = isAppliedToProducts,
            IsActive = true,
            IsSystem = isSystem,
            DisplayOrder = displayOrder
        };

        return Result.Success(tax);
    }

    /// <summary>
    /// Factory for system seed data with explicit id (migrations / provisioning).
    /// </summary>
    public static Result<Tax> CreateSystem(
        Guid id,
        string name,
        TaxType type,
        TaxValueType valueType,
        decimal value,
        TaxContext context,
        bool isAppliedToProducts,
        int displayOrder)
    {
        var created = Create(name, type, valueType, value, context, isAppliedToProducts, isSystem: true, displayOrder);
        if (created.IsFailure)
            return created;

        var tax = created.Value;
        tax.Id = id;
        return Result.Success(tax);
    }

    public Result Update(
        string name,
        TaxType type,
        TaxValueType valueType,
        decimal value,
        TaxContext context,
        bool isAppliedToProducts,
        int displayOrder)
    {
        if (IsSystem)
        {
            var nameValidation = ValidateName(name);
            if (nameValidation.IsFailure)
                return nameValidation;

            Name = name.Trim();
            DisplayOrder = displayOrder;
            IsAppliedToProducts = isAppliedToProducts;
            UpdatedAt = DateTime.UtcNow;
            return Result.Success();
        }

        var nameValidation2 = ValidateName(name);
        if (nameValidation2.IsFailure)
            return nameValidation2;

        var valueValidation = ValidateValue(valueType, value, type, isSystem: false);
        if (valueValidation.IsFailure)
            return valueValidation;

        Name = name.Trim();
        Type = type;
        ValueType = valueType;
        Value = value;
        Context = context;
        IsAppliedToProducts = isAppliedToProducts;
        DisplayOrder = displayOrder;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public void SetActive(bool active)
    {
        IsActive = active;
        UpdatedAt = DateTime.UtcNow;
    }

    private static Result ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(Error.Validation("Name", "Le nom de la taxe est obligatoire"));

        if (name.Length > 100)
            return Result.Failure(Error.Validation("Name", "Le nom ne peut pas dépasser 100 caractères"));

        return Result.Success();
    }

    private static Result ValidateValue(TaxValueType valueType, decimal value, TaxType type, bool isSystem)
    {
        if (valueType == TaxValueType.Percentage)
        {
            if (value < 0 || value > 100)
                return Result.Failure(Error.Validation("Value", "Le pourcentage doit être entre 0 et 100"));
        }
        else
        {
            if (value < 0)
                return Result.Failure(Error.Validation("Value", "Le montant fixe doit être positif ou nul"));
        }

        if (type == TaxType.VAT && valueType != TaxValueType.Percentage)
            return Result.Failure(Error.Validation("ValueType", "La TVA doit être exprimée en pourcentage"));

        if (isSystem && type == TaxType.VAT && valueType == TaxValueType.Percentage)
        {
            var v = (int)value;
            if (v is not (0 or 7 or 13 or 19))
                return Result.Failure(Error.Validation("Value", "Les taux de TVA système doivent être 0, 7, 13 ou 19 %"));
        }

        return Result.Success();
    }
}
