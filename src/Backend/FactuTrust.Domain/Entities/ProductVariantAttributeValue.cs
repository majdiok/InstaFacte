using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>Links a child SKU to one attribute value of its parent matrix.</summary>
public sealed class ProductVariantAttributeValue : Entity
{
    public Guid ProductId { get; private set; }
    public Guid AttributeValueId { get; private set; }

    private ProductVariantAttributeValue() { }

    public static Result<ProductVariantAttributeValue> Create(Guid productId, Guid attributeValueId)
    {
        if (productId == Guid.Empty)
            return Result.Failure<ProductVariantAttributeValue>(Error.Validation("ProductId", "Le produit est obligatoire"));
        if (attributeValueId == Guid.Empty)
            return Result.Failure<ProductVariantAttributeValue>(Error.Validation("AttributeValueId", "La valeur d'attribut est obligatoire"));

        return Result.Success(new ProductVariantAttributeValue
        {
            ProductId = productId,
            AttributeValueId = attributeValueId
        });
    }
}
