using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Persistence;

public partial class TenantDbContext
{
    private static void ConfigureStockTraceability(ModelBuilder builder)
    {
        builder.Entity<ProductLot>(entity =>
        {
            entity.ToTable("ProductLots");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LotNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.HasIndex(e => new { e.ProductId, e.LotNumber }).IsUnique();
            entity.HasIndex(e => e.ExpiryDate);
            entity.HasOne<Product>().WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StockLotBalance>(entity =>
        {
            entity.ToTable("StockLotBalances");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.QuantityOnHand).HasPrecision(18, 4);
            entity.Property(e => e.QuantityReserved).HasPrecision(18, 4);
            entity.HasIndex(e => new { e.StockItemId, e.ProductLotId }).IsUnique();
            entity.HasOne<StockItem>().WithMany().HasForeignKey(e => e.StockItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ProductLot>().WithMany().HasForeignKey(e => e.ProductLotId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProductSerial>(entity =>
        {
            entity.ToTable("ProductSerials");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SerialNumber).HasMaxLength(80).IsRequired();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.HasIndex(e => new { e.ProductId, e.SerialNumber }).IsUnique();
            entity.HasIndex(e => new { e.ProductId, e.WarehouseId, e.Status });
            entity.HasOne<Product>().WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProductLot>().WithMany().HasForeignKey(e => e.ProductLotId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        });

        builder.Entity<StockValuationLayer>(entity =>
        {
            entity.ToTable("StockValuationLayers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OriginalQuantity).HasPrecision(18, 4);
            entity.Property(e => e.RemainingQuantity).HasPrecision(18, 4);
            entity.Property(e => e.UnitCost).HasPrecision(18, 4);
            entity.Property(e => e.SourceReference).HasMaxLength(100);
            entity.HasIndex(e => new { e.StockItemId, e.ReceivedAt });
            entity.HasOne<StockItem>().WithMany().HasForeignKey(e => e.StockItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ProductLot>().WithMany().HasForeignKey(e => e.ProductLotId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        });

        builder.Entity<StockDocumentAllocation>(entity =>
        {
            entity.ToTable("StockDocumentAllocations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DocumentKind).HasConversion<int>();
            entity.Property(e => e.Quantity).HasPrecision(18, 4);
            entity.Property(e => e.UnitCost).HasPrecision(18, 4);
            entity.HasIndex(e => new { e.DocumentKind, e.DocumentLineId });
            entity.HasOne<Product>().WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProductLot>().WithMany().HasForeignKey(e => e.ProductLotId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
            entity.HasOne<ProductSerial>().WithMany().HasForeignKey(e => e.SerialId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        });

        builder.Entity<ProductAttributeDefinition>(entity =>
        {
            entity.ToTable("ProductAttributeDefinitions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(80).IsRequired();
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasMany(e => e.Values)
                .WithOne()
                .HasForeignKey(v => v.DefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(e => e.Values).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<ProductAttributeValue>(entity =>
        {
            entity.ToTable("ProductAttributeValues");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(80).IsRequired();
            entity.HasIndex(e => new { e.DefinitionId, e.Code }).IsUnique();
        });

        builder.Entity<ProductVariantAxis>(entity =>
        {
            entity.ToTable("ProductVariantAxes");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ParentProductId, e.DefinitionId }).IsUnique();
            entity.HasOne<Product>().WithMany().HasForeignKey(e => e.ParentProductId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ProductAttributeDefinition>().WithMany().HasForeignKey(e => e.DefinitionId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProductVariantAttributeValue>(entity =>
        {
            entity.ToTable("ProductVariantAttributeValues");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ProductId, e.AttributeValueId }).IsUnique();
            entity.HasOne<Product>().WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ProductAttributeValue>().WithMany().HasForeignKey(e => e.AttributeValueId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
