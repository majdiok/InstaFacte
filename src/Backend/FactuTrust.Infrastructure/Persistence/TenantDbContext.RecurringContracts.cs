using FactuTrust.Domain.Entities.RecurringContracts;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Persistence;

public partial class TenantDbContext
{
    private static void ConfigureRecurringContracts(ModelBuilder builder)
    {
        builder.Entity<RecurringContract>(entity =>
        {
            entity.ToTable("RecurringContracts");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Number).HasMaxLength(50);
            entity.Property(c => c.Status).HasConversion<int>().IsRequired();
            entity.Property(c => c.BillingFrequency).HasConversion<int>().IsRequired();
            entity.Property(c => c.Currency).HasMaxLength(3).IsRequired();
            entity.Property(c => c.Reference).HasMaxLength(100);
            entity.Property(c => c.Notes).HasMaxLength(2000);
            entity.HasIndex(c => c.ClientId);
            entity.HasIndex(c => c.Status);
            entity.HasIndex(c => c.NextBillingDate)
                .HasFilter("[NextBillingDate] IS NOT NULL");
            entity.Property(c => c.Version).IsConcurrencyToken();
            entity.HasMany(c => c.Lines)
                .WithOne()
                .HasForeignKey(l => l.RecurringContractId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(c => c.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<RecurringContractLine>(entity =>
        {
            entity.ToTable("RecurringContractLines");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.LineType).HasConversion<int>().IsRequired();
            entity.Property(l => l.Description).HasMaxLength(500).IsRequired();
            entity.Property(l => l.Quantity).HasPrecision(18, 3);
            entity.Property(l => l.UnitPriceHT).HasPrecision(18, 3);
            entity.Property(l => l.VatRate).HasPrecision(5, 2);
            entity.Property(l => l.IncludedQuantity).HasPrecision(18, 3);
            entity.Property(l => l.OverageUnitPriceHT).HasPrecision(18, 3);
            entity.HasIndex(l => l.RecurringContractId);
            entity.HasIndex(l => new { l.RecurringContractId, l.IsActive });
        });

        builder.Entity<UsageMetric>(entity =>
        {
            entity.ToTable("UsageMetrics");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Code).HasMaxLength(50).IsRequired();
            entity.Property(m => m.Name).HasMaxLength(200).IsRequired();
            entity.Property(m => m.Unit).HasMaxLength(30).IsRequired();
            entity.Property(m => m.AggregationMode).HasConversion<int>().IsRequired();
            entity.HasIndex(m => m.Code).IsUnique();
        });

        builder.Entity<UsageRecord>(entity =>
        {
            entity.ToTable("UsageRecords");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Quantity).HasPrecision(18, 3);
            entity.Property(r => r.Source).HasConversion<int>().IsRequired();
            entity.Property(r => r.Notes).HasMaxLength(500);
            entity.HasIndex(r => new { r.RecurringContractId, r.UsageMetricId, r.PeriodFrom, r.PeriodTo, r.Source });
            entity.HasIndex(r => new { r.RecurringContractId, r.PeriodFrom, r.PeriodTo });
        });

        builder.Entity<RecurringContractBillingRun>(entity =>
        {
            entity.ToTable("RecurringContractBillingRuns");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Status).HasConversion<int>().IsRequired();
            entity.Property(r => r.FixedAmount).HasPrecision(18, 3);
            entity.Property(r => r.UsageAmount).HasPrecision(18, 3);
            entity.Property(r => r.ProrationAmount).HasPrecision(18, 3);
            entity.Property(r => r.ErrorMessage).HasMaxLength(2000);
            entity.HasIndex(r => new { r.RecurringContractId, r.PeriodFrom, r.PeriodTo }).IsUnique();
            entity.HasIndex(r => r.Status);
            entity.Ignore(r => r.TotalAmount);
        });

        builder.Entity<RecurringContractAmendment>(entity =>
        {
            entity.ToTable("RecurringContractAmendments");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.AmendmentType).HasConversion<int>().IsRequired();
            entity.Property(a => a.ProrationPolicy).HasConversion<int>().IsRequired();
            entity.Property(a => a.Notes).HasMaxLength(2000);
            entity.HasIndex(a => a.RecurringContractId);
        });
    }
}
