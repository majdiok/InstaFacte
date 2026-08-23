using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Domain.Services;

public static class ProductCommercialGuards
{
    public static Result EnsureCanAppearOnDocument(Product product)
    {
        if (product.IsVariantTemplate)
        {
            return Result.Failure(Error.Validation(
                "Product",
                "Un modèle de variantes ne peut pas être ajouté à un document. Choisissez une variante (SKU)."));
        }

        return Result.Success();
    }
}
