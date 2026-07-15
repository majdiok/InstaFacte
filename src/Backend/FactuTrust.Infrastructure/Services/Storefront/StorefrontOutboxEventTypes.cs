namespace FactuTrust.Infrastructure.Services.Storefront;

internal static class StorefrontOutboxEventTypes
{
    public const string ProductUpserted = "storefront.product.upserted";
    public const string ProductRemoved = "storefront.product.removed";
}
