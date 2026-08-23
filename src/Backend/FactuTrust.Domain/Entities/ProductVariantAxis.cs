using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>Attribute used as a variant axis on a template product (size, color, …).</summary>
public sealed class ProductVariantAxis : Entity
{
    public Guid ParentProductId { get; private set; }
    public Guid DefinitionId { get; private set; }
    public int SortOrder { get; private set; }

    private ProductVariantAxis() { }

    public static Result<ProductVariantAxis> Create(Guid parentProductId, Guid definitionId, int sortOrder = 0)
    {
        if (parentProductId == Guid.Empty)
            return Result.Failure<ProductVariantAxis>(Error.Validation("ParentProductId", "Le produit parent est obligatoire"));
        if (definitionId == Guid.Empty)
            return Result.Failure<ProductVariantAxis>(Error.Validation("DefinitionId", "L'attribut est obligatoire"));

        return Result.Success(new ProductVariantAxis
        {
            ParentProductId = parentProductId,
            DefinitionId = definitionId,
            SortOrder = sortOrder
        });
    }
}
