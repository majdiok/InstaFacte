using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Entities.Storefront;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.Storefront;

public sealed class StorefrontTenantWriter : IStorefrontTenantWriter
{
    private readonly TenantDbContextFactory _contextFactory;
    private readonly IStorefrontOutboxPayloadBuilder _payloadBuilder;
    private readonly ILogger<StorefrontTenantWriter> _logger;

    public StorefrontTenantWriter(
        TenantDbContextFactory contextFactory,
        IStorefrontOutboxPayloadBuilder payloadBuilder,
        ILogger<StorefrontTenantWriter> logger)
    {
        _contextFactory = contextFactory;
        _payloadBuilder = payloadBuilder;
        _logger = logger;
    }

    public async Task<StorefrontProductVisibilityResult> SetProductPublicVisibilityAsync(
        Guid productId,
        bool isPubliclyListed,
        Func<Guid, CancellationToken, Task<string?>> categoryLabelResolver,
        Guid storefrontProfileId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var product = await context.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
                if (product is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return StorefrontProductVisibilityResult.NotFound();
                }

                if (!product.SetPubliclyListed(isPubliclyListed))
                {
                    await transaction.CommitAsync(cancellationToken);
                    return StorefrontProductVisibilityResult.Unchanged(product.IsActive);
                }

                product.IncrementVersion();

                string payload;
                string eventType;
                if (isPubliclyListed)
                {
                    var categoryLabel = await categoryLabelResolver(product.CategoryId, cancellationToken);
                    payload = _payloadBuilder.BuildProductPayload(storefrontProfileId, tenantId, product, categoryLabel);
                    eventType = StorefrontOutboxEventTypes.ProductUpserted;
                }
                else
                {
                    payload = _payloadBuilder.BuildProductRemovedPayload(storefrontProfileId, tenantId, productId);
                    eventType = StorefrontOutboxEventTypes.ProductRemoved;
                }

                var message = StorefrontOutboxMessage.Create(
                    aggregateType: "Product",
                    aggregateId: product.Id,
                    sourceVersion: product.Version,
                    eventType: eventType,
                    payloadJson: payload);

                context.StorefrontOutboxMessages.Add(message);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "Storefront outbox {EventType} for product {ProductId} tenant {TenantId}",
                    eventType,
                    productId,
                    tenantId);

                return StorefrontProductVisibilityResult.Applied(product.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed storefront visibility write for product {ProductId}", productId);
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
}
