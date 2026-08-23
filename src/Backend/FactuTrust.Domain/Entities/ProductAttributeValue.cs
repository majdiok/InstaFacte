using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

public sealed class ProductAttributeValue : Entity
{
    public Guid DefinitionId { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public int SortOrder { get; private set; }

    private ProductAttributeValue() { }

    internal static Result<ProductAttributeValue> Create(Guid definitionId, string code, string name, int sortOrder)
    {
        if (definitionId == Guid.Empty)
            return Result.Failure<ProductAttributeValue>(Error.Validation("DefinitionId", "L'attribut est obligatoire"));
        if (string.IsNullOrWhiteSpace(code))
            return Result.Failure<ProductAttributeValue>(Error.Validation("Code", "Le code de valeur est obligatoire"));
        if (code.Trim().Length > 30)
            return Result.Failure<ProductAttributeValue>(Error.Validation("Code", "Le code de valeur ne peut pas dépasser 30 caractères"));
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<ProductAttributeValue>(Error.Validation("Name", "Le libellé de valeur est obligatoire"));
        if (name.Trim().Length > 80)
            return Result.Failure<ProductAttributeValue>(Error.Validation("Name", "Le libellé de valeur ne peut pas dépasser 80 caractères"));

        return Result.Success(new ProductAttributeValue
        {
            DefinitionId = definitionId,
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            SortOrder = sortOrder
        });
    }

    public Result Update(string name, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(Error.Validation("Name", "Le libellé de valeur est obligatoire"));
        if (name.Trim().Length > 80)
            return Result.Failure(Error.Validation("Name", "Le libellé de valeur ne peut pas dépasser 80 caractères"));

        Name = name.Trim();
        SortOrder = sortOrder;
        return Result.Success();
    }
}
