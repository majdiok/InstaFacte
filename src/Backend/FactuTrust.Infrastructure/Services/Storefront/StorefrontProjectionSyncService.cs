using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Entities.Storefront;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.Storefront;

/// <summary>
/// Consumes tenant-local <see cref="StorefrontOutboxMessage"/> rows and upserts the public
/// projection tables in the Master database with idempotent inbox deduplication.
/// </summary>
public sealed class StorefrontProjectionSyncService : BackgroundService
{
    private static readonly SemaphoreSlim GlobalGate = new(1, 1);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StorefrontProjectionSyncService> _logger;

    public StorefrontProjectionSyncService(
        IServiceScopeFactory scopeFactory,
        ILogger<StorefrontProjectionSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var options = scope.ServiceProvider.GetRequiredService<IOptions<StorefrontOptions>>().Value;
                if (!options.Enabled)
                {
                    await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
                    continue;
                }

                if (!await GlobalGate.WaitAsync(TimeSpan.Zero, stoppingToken))
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                try
                {
                    await RunSyncCycleAsync(scope.ServiceProvider, options, stoppingToken);
                }
                finally
                {
                    GlobalGate.Release();
                }

                await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(options.ProjectionPollIntervalSeconds, 5, 300)), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Storefront projection sync cycle failed");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    private async Task RunSyncCycleAsync(IServiceProvider sp, StorefrontOptions options, CancellationToken ct)
    {
        var master = sp.GetRequiredService<MasterDbContext>();
        var tenantService = sp.GetRequiredService<ITenantService>();

        var tenantIds = await master.StorefrontProfiles
            .AsNoTracking()
            .Where(p => p.Status == StorefrontStatus.Published || p.Status == StorefrontStatus.Suspended)
            .Select(p => p.TenantId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var tenantId in tenantIds)
        {
            var connectionString = await tenantService.GetConnectionStringAsync(tenantId, ct);
            if (string.IsNullOrEmpty(connectionString))
                continue;

            await ProcessTenantAsync(master, connectionString, tenantId, ct);
        }
    }

    private async Task ProcessTenantAsync(MasterDbContext master, string connectionString, Guid tenantId, CancellationToken ct)
    {
        var tenantOptions = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(connectionString, b => b.MigrationsAssembly(typeof(TenantDbContext).Assembly.FullName))
            .Options;

        await using var tenantDb = new TenantDbContext(tenantOptions);

        var pending = await tenantDb.StorefrontOutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.OccurredAt)
            .Take(50)
            .ToListAsync(ct);

        foreach (var message in pending)
        {
            try
            {
                var already = await master.StorefrontOutboxInboxEntries
                    .AsNoTracking()
                    .AnyAsync(
                        e => e.TenantId == tenantId
                             && e.AggregateType == message.AggregateType
                             && e.AggregateId == message.AggregateId
                             && e.SourceVersion == message.SourceVersion,
                        ct);

                if (already)
                {
                    message.MarkProcessed();
                    await tenantDb.SaveChangesAsync(ct);
                    continue;
                }

                if (message.AggregateType.Equals("Product", StringComparison.OrdinalIgnoreCase))
                {
                    if (message.EventType == StorefrontOutboxEventTypes.ProductUpserted)
                        await ApplyProductUpsertAsync(master, tenantId, message, ct);
                    else if (message.EventType == StorefrontOutboxEventTypes.ProductRemoved)
                        await ApplyProductRemovedAsync(master, tenantId, message, ct);
                    else
                        throw new InvalidOperationException($"Unknown storefront event type: {message.EventType}");
                }
                else
                {
                    _logger.LogWarning("Skipping unsupported aggregate type {Type}", message.AggregateType);
                }

                master.StorefrontOutboxInboxEntries.Add(
                    StorefrontOutboxInboxEntry.Mark(
                        tenantId,
                        message.AggregateType,
                        message.AggregateId,
                        message.SourceVersion));

                message.MarkProcessed();
                await master.SaveChangesAsync(ct);
                await tenantDb.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                master.ChangeTracker.Clear();
                message.RecordAttemptFailure(ex.Message);
                await tenantDb.SaveChangesAsync(ct);
                _logger.LogWarning(
                    ex,
                    "Failed processing storefront outbox message {MessageId} for tenant {TenantId}",
                    message.Id,
                    tenantId);
            }
        }
    }

    private static async Task ApplyProductUpsertAsync(
        MasterDbContext master,
        Guid tenantId,
        StorefrontOutboxMessage message,
        CancellationToken ct)
    {
        var dto = JsonSerializer.Deserialize<ProductOutboxPayload>(message.PayloadJson, SerializerOptions);
        if (dto is null)
            throw new InvalidOperationException("Invalid product outbox payload.");

        var existing = await master.StorefrontProducts
            .FirstOrDefaultAsync(
                p => p.StorefrontProfileId == dto.StorefrontProfileId && p.SourceProductId == dto.ProductId,
                ct);

        if (existing is null)
        {
            var seedVersion = Math.Max(0, message.SourceVersion - 1);
            var create = StorefrontProduct.Create(
                dto.StorefrontProfileId,
                tenantId,
                dto.ProductId,
                dto.Slug,
                dto.Name,
                dto.UnitPriceAmount,
                dto.Currency,
                seedVersion);

            if (create.IsFailure)
                throw new InvalidOperationException(create.Error.Description);

            var product = create.Value;
            if (!product.ApplyUpsert(
                    dto.Name,
                    SanitizeDescription(dto.Description),
                    dto.UnitPriceAmount,
                    dto.Currency,
                    dto.PublicImageUrl,
                    dto.ImageHash,
                    dto.CategoryLabel,
                    dto.StockDisplayStatus,
                    dto.IsVisible,
                    dto.DisplayOrder,
                    message.SourceVersion))
            {
                throw new InvalidOperationException("Initial storefront product projection could not be applied.");
            }

            master.StorefrontProducts.Add(product);
        }
        else
        {
            existing.ApplyUpsert(
                dto.Name,
                SanitizeDescription(dto.Description),
                dto.UnitPriceAmount,
                dto.Currency,
                dto.PublicImageUrl,
                dto.ImageHash,
                dto.CategoryLabel,
                dto.StockDisplayStatus,
                dto.IsVisible,
                dto.DisplayOrder,
                message.SourceVersion);
        }
    }

    private static async Task ApplyProductRemovedAsync(
        MasterDbContext master,
        Guid tenantId,
        StorefrontOutboxMessage message,
        CancellationToken ct)
    {
        var dto = JsonSerializer.Deserialize<ProductRemovedOutboxPayload>(message.PayloadJson, SerializerOptions);
        if (dto is null)
            throw new InvalidOperationException("Invalid product removed payload.");

        var existing = await master.StorefrontProducts
            .FirstOrDefaultAsync(
                p => p.StorefrontProfileId == dto.StorefrontProfileId && p.SourceProductId == dto.ProductId,
                ct);

        existing?.MarkInvisible();
    }

    private static string? SanitizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;
        var t = description.Trim();
        if (t.Length > StorefrontProduct.DescriptionMaxLength)
            t = t[..StorefrontProduct.DescriptionMaxLength];
        return t;
    }

    private sealed record ProductOutboxPayload(
        Guid StorefrontProfileId,
        Guid TenantId,
        Guid ProductId,
        string Slug,
        string Name,
        string? Description,
        decimal UnitPriceAmount,
        string Currency,
        string? PublicImageUrl,
        string? ImageHash,
        string? CategoryLabel,
        StockDisplayStatus StockDisplayStatus,
        bool IsVisible,
        int DisplayOrder);

    private sealed record ProductRemovedOutboxPayload(
        Guid StorefrontProfileId,
        Guid TenantId,
        Guid ProductId);
}
