using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Services;

namespace FactuTrust.Application.Features.Products;

internal static class ProductTraceabilityFeatureMapper
{
    public static ProductTraceabilityFeatureFlags ToDomainFlags(StockTraceabilityOptions options) =>
        new(
            options.LotTrackingEnabled,
            options.SerialTrackingEnabled,
            options.ExpiryTrackingEnabled,
            options.FifoLifoValuationEnabled);
}
