using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Represents a product category for classification.
/// </summary>
public sealed class ProductCategory : Entity
{
    public const string DefaultCode = "GENERAL";

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; }

    private ProductCategory() { }

    public static Result<ProductCategory> Create(string code, string name, int displayOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Result.Failure<ProductCategory>(Error.Validation("Code", "Le code catégorie est obligatoire"));

        if (code.Length > 50)
            return Result.Failure<ProductCategory>(Error.Validation("Code", "Le code catégorie ne peut pas dépasser 50 caractères"));

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<ProductCategory>(Error.Validation("Name", "Le nom de la catégorie est obligatoire"));

        if (name.Length > 100)
            return Result.Failure<ProductCategory>(Error.Validation("Name", "Le nom de la catégorie ne peut pas dépasser 100 caractères"));

        var category = new ProductCategory
        {
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            DisplayOrder = displayOrder,
            IsActive = true
        };

        return Result.Success(category);
    }

    public void Update(string name, int displayOrder)
    {
        if (!string.IsNullOrWhiteSpace(name))
            Name = name.Trim();

        DisplayOrder = displayOrder;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Reactivate()
    {
        IsActive = true;
    }
}
