using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Forecasting;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Persistence;

/// <summary>
/// EF Core mapping for the AI Forecasting module entities.
/// Kept in a partial file to keep the main TenantDbContext readable and to make the module
/// self-contained: removing the AI Forecasting feature only requires deleting this file plus
/// the corresponding DbSets and the call to ConfigureForecasting.
/// </summary>
public partial class TenantDbContext
{
    private static void ConfigureForecasting(ModelBuilder builder)
    {
        ConfigureSalesForecast(builder);
        ConfigureReplenishmentRecommendation(builder);
        ConfigurePromotionRecommendation(builder);
        ConfigureProductAbcXyzClassification(builder);
        ConfigureForecastRecomputeAudit(builder);
        ConfigureReplenishmentDecisionAudit(builder);
        ConfigureProductReplenishmentV2(builder);
    }

    private static void ConfigureSalesForecast(ModelBuilder builder)
    {
        builder.Entity<SalesForecast>(entity =>
        {
            entity.ToTable("SalesForecasts");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ScopeType).IsRequired();
            entity.Property(e => e.ScopeId);
            entity.Property(e => e.Horizon).IsRequired();
            entity.Property(e => e.GeneratedAt).IsRequired();
            entity.Property(e => e.PeriodStart).IsRequired();
            entity.Property(e => e.PeriodEnd).IsRequired();
            entity.Property(e => e.ConfidencePercent).HasPrecision(5, 2);
            entity.Property(e => e.MethodUsed).IsRequired();
            entity.Property(e => e.InputsJson).HasMaxLength(8000);
            entity.Property(e => e.Notes).HasMaxLength(1000);

            entity.OwnsOne(e => e.ExpectedAmount, m =>
            {
                m.Property(p => p.Amount).HasColumnName("ExpectedAmount").HasPrecision(18, 3);
                m.Property(p => p.Currency).HasColumnName("ExpectedCurrency").HasMaxLength(3);
            });
            entity.OwnsOne(e => e.LowAmount, m =>
            {
                m.Property(p => p.Amount).HasColumnName("LowAmount").HasPrecision(18, 3);
                m.Property(p => p.Currency).HasColumnName("LowCurrency").HasMaxLength(3);
            });
            entity.OwnsOne(e => e.HighAmount, m =>
            {
                m.Property(p => p.Amount).HasColumnName("HighAmount").HasPrecision(18, 3);
                m.Property(p => p.Currency).HasColumnName("HighCurrency").HasMaxLength(3);
            });

            entity.HasIndex(e => new { e.ScopeType, e.ScopeId, e.Horizon, e.GeneratedAt })
                  .HasDatabaseName("IX_SalesForecasts_ScopeHorizonGenerated");
            entity.HasIndex(e => e.GeneratedAt);
        });
    }

    private static void ConfigureReplenishmentRecommendation(ModelBuilder builder)
    {
        builder.Entity<ReplenishmentRecommendation>(entity =>
        {
            entity.ToTable("ReplenishmentRecommendations");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ProductId).IsRequired();
            entity.Property(e => e.WarehouseId).IsRequired();
            entity.Property(e => e.GeneratedAt).IsRequired();

            entity.Property(e => e.RecommendedQty).HasPrecision(18, 3);
            entity.Property(e => e.Rop).HasPrecision(18, 3);
            entity.Property(e => e.SafetyStock).HasPrecision(18, 3);
            entity.Property(e => e.DailyDemand).HasPrecision(18, 3);
            entity.Property(e => e.LeadTimeDays).IsRequired();

            entity.Property(e => e.ReasonCodesJson).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.DismissedReason).HasMaxLength(500);
            entity.Property(e => e.ProcessedBy).HasMaxLength(450); // matches AspNet UserId length
            entity.Property(e => e.LinkedPurchaseOrderId);

            // V2 additive fields — null/0 default so legacy rows stay valid.
            entity.Property(e => e.PreferredSupplierId);
            entity.Property(e => e.PreferredSupplierName).HasMaxLength(200);
            entity.Property(e => e.QuantityOnOrder).HasPrecision(18, 3).HasDefaultValue(0m);
            entity.Property(e => e.EffectiveQty).HasPrecision(18, 3).HasDefaultValue(0m);
            entity.Property(e => e.ManualQtyOverride).HasPrecision(18, 3);
            entity.Property(e => e.ManualSupplierOverride);
            entity.Property(e => e.DaysOfStockRemaining).HasPrecision(10, 2);
            entity.Property(e => e.UserNotes).HasMaxLength(1000);

            entity.HasIndex(e => new { e.ProductId, e.GeneratedAt })
                  .HasDatabaseName("IX_ReplenishmentRecommendations_ProductGenerated");
            entity.HasIndex(e => new { e.WarehouseId, e.Status });
            entity.HasIndex(e => e.Status);
            // V2 lookup: list of recos still pending decision routed to a supplier.
            entity.HasIndex(e => new { e.PreferredSupplierId, e.Status })
                  .HasDatabaseName("IX_ReplenishmentRecommendations_SupplierStatus");
            // V2 filter `WHERE PreferredSupplierId = X OR ManualSupplierOverride = X` — SQL Server
            // cannot reuse the composite index above for the OR branch, so this dedicated index
            // keeps the query sargable when manual overrides are common.
            entity.HasIndex(e => e.ManualSupplierOverride)
                  .HasDatabaseName("IX_ReplenishmentRecommendations_ManualSupplier");
        });
    }

    private static void ConfigureReplenishmentDecisionAudit(ModelBuilder builder)
    {
        builder.Entity<ReplenishmentDecisionAudit>(entity =>
        {
            entity.ToTable("ReplenishmentDecisionAudits");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.RecommendationId).IsRequired();
            entity.Property(e => e.FromStatus).IsRequired();
            entity.Property(e => e.ToStatus).IsRequired();
            entity.Property(e => e.ActionType).HasMaxLength(32).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.ActorUserId).HasMaxLength(450).IsRequired();
            entity.Property(e => e.ActedAt).IsRequired();
            entity.Property(e => e.PayloadJson).HasMaxLength(4000);

            // Primary lookup: full timeline for a given recommendation.
            entity.HasIndex(e => new { e.RecommendationId, e.ActedAt })
                  .HasDatabaseName("IX_ReplenishmentDecisionAudits_RecommendationActed");
            entity.HasIndex(e => e.ActedAt);
        });
    }

    /// <summary>
    /// EF Core mapping for the Replenishment V2 additive columns on <see cref="Product"/>.
    /// Kept here (rather than in the main Product config) to keep the V2 surface optional
    /// — removing this method has no impact on the existing Product schema.
    /// </summary>
    private static void ConfigureProductReplenishmentV2(ModelBuilder builder)
    {
        builder.Entity<Product>(entity =>
        {
            entity.Property(p => p.PreferredSupplierId);
            entity.Property(p => p.MinimumOrderQuantity).HasPrecision(18, 3);
            entity.Property(p => p.PackagingUnit).HasMaxLength(50);
            entity.Property(p => p.PackagingQty).HasPrecision(18, 3);
            entity.Property(p => p.LeadTimeDaysOverride);
        });
    }

    private static void ConfigurePromotionRecommendation(ModelBuilder builder)
    {
        builder.Entity<PromotionRecommendation>(entity =>
        {
            entity.ToTable("PromotionRecommendations");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.GeneratedAt).IsRequired();
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.SuggestedDiscountPercent).HasPrecision(5, 2);
            entity.Property(e => e.ExpectedUpliftPercent).HasPrecision(6, 2);
            entity.Property(e => e.ValidFrom).IsRequired();
            entity.Property(e => e.ValidUntil).IsRequired();
            entity.Property(e => e.ReasoningSummary).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.ReasonCodesJson).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.RelatedEventCode).HasMaxLength(50);
            entity.Property(e => e.ProcessedBy).HasMaxLength(450);

            entity.HasIndex(e => new { e.ProductId, e.GeneratedAt });
            entity.HasIndex(e => new { e.CategoryId, e.GeneratedAt });
            entity.HasIndex(e => new { e.Status, e.ValidUntil });
            entity.HasIndex(e => e.Type);
        });
    }

    private static void ConfigureProductAbcXyzClassification(ModelBuilder builder)
    {
        builder.Entity<ProductAbcXyzClassification>(entity =>
        {
            entity.ToTable("ProductAbcXyzClassifications");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ProductId).IsRequired();
            entity.Property(e => e.ComputedAt).IsRequired();
            entity.Property(e => e.AbcClass).IsRequired();
            entity.Property(e => e.XyzClass).IsRequired();
            entity.Property(e => e.CumulativeRevenuePercent).HasPrecision(7, 4);
            entity.Property(e => e.DemandCv).HasPrecision(12, 6);
            entity.Property(e => e.ReferenceRevenue).HasPrecision(18, 3);
            entity.Property(e => e.ActiveMonths).IsRequired();

            entity.HasIndex(e => new { e.ProductId, e.ComputedAt })
                  .HasDatabaseName("IX_ProductAbcXyzClassifications_ProductComputed");
            entity.HasIndex(e => new { e.AbcClass, e.XyzClass });
        });
    }

    private static void ConfigureForecastRecomputeAudit(ModelBuilder builder)
    {
        builder.Entity<ForecastRecomputeAudit>(entity =>
        {
            entity.ToTable("ForecastRecomputeAudits");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.StartedAt).IsRequired();
            entity.Property(e => e.TriggerType).HasMaxLength(20).IsRequired();
            entity.Property(e => e.TriggeredBy).HasMaxLength(450);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.Property(e => e.Success).IsRequired();
            entity.Property(e => e.DurationMs).IsRequired();

            entity.HasIndex(e => e.StartedAt);
            entity.HasIndex(e => new { e.TriggerType, e.StartedAt });
        });
    }
}
