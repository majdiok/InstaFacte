using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

public sealed class ProductAttributeDefinition : AggregateRoot
{
    private readonly List<ProductAttributeValue> _values = new();

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public int SortOrder { get; private set; }
    public IReadOnlyCollection<ProductAttributeValue> Values => _values.AsReadOnly();

    private ProductAttributeDefinition() { }

    public static Result<ProductAttributeDefinition> Create(string code, string name, int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Result.Failure<ProductAttributeDefinition>(Error.Validation("Code", "Le code d'attribut est obligatoire"));
        if (code.Trim().Length > 30)
            return Result.Failure<ProductAttributeDefinition>(Error.Validation("Code", "Le code d'attribut ne peut pas dépasser 30 caractères"));
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<ProductAttributeDefinition>(Error.Validation("Name", "Le nom d'attribut est obligatoire"));
        if (name.Trim().Length > 80)
            return Result.Failure<ProductAttributeDefinition>(Error.Validation("Name", "Le nom d'attribut ne peut pas dépasser 80 caractères"));

        return Result.Success(new ProductAttributeDefinition
        {
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            SortOrder = sortOrder
        });
    }

    public Result<ProductAttributeValue> AddValue(string code, string name, int sortOrder = 0)
    {
        var valueResult = ProductAttributeValue.Create(Id, code, name, sortOrder);
        if (valueResult.IsFailure)
            return Result.Failure<ProductAttributeValue>(valueResult.Error);

        var value = valueResult.Value;
        if (_values.Any(v => v.Code == value.Code))
            return Result.Failure<ProductAttributeValue>(Error.Conflict($"La valeur '{value.Code}' existe déjà pour cet attribut"));

        _values.Add(value);
        return Result.Success(value);
    }

    public Result Update(string name, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(Error.Validation("Name", "Le nom d'attribut est obligatoire"));
        if (name.Trim().Length > 80)
            return Result.Failure(Error.Validation("Name", "Le nom d'attribut ne peut pas dépasser 80 caractères"));

        Name = name.Trim();
        SortOrder = sortOrder;
        return Result.Success();
    }

    public ProductAttributeValue? FindValue(Guid valueId) =>
        _values.FirstOrDefault(v => v.Id == valueId);

    public Result RemoveValue(Guid valueId)
    {
        var value = FindValue(valueId);
        if (value is null)
            return Result.Failure(Error.NotFound("ProductAttributeValue", valueId));

        _values.Remove(value);
        return Result.Success();
    }
}
