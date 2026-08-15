using FactuTrust.Domain.Entities.Treasury;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Persistence;

/// <summary>
/// Mapping EF Core du module « Trésorerie prévisionnelle par IA ».
/// Isolé dans un partial, comme le module Prévisions IA : retirer la fonctionnalité ne demande que
/// de supprimer ce fichier, les DbSet correspondants et l'appel à ConfigureTreasuryForecast.
/// </summary>
/// <remarks>
/// Tous les montants sont des <c>decimal(18,3)</c> nus et non des <c>Money</c> en owned type :
/// soldes et flux nets sont signés, et le value object refuse les valeurs négatives.
/// </remarks>
public partial class TenantDbContext
{
    private static void ConfigureTreasuryForecast(ModelBuilder builder)
    {
        ConfigureCashFlowForecastRun(builder);
        ConfigureCashFlowForecastLine(builder);
        ConfigureCashFlowForecastBucket(builder);
        ConfigureCashFlowScenario(builder);
        ConfigureCashFlowForecastInsight(builder);
        ConfigureRecurringCashCommitment(builder);
        ConfigureCashFlowForecastSettings(builder);
    }

    private static void ConfigureCashFlowForecastRun(ModelBuilder builder)
    {
        builder.Entity<CashFlowForecastRun>(entity =>
        {
            entity.ToTable("CashFlowForecastRuns");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.PeriodStart).IsRequired();
            entity.Property(e => e.PeriodEnd).IsRequired();
            entity.Property(e => e.HorizonMonths).IsRequired();
            entity.Property(e => e.OpeningBalance).HasPrecision(18, 3);
            entity.Property(e => e.TotalInflows).HasPrecision(18, 3);
            entity.Property(e => e.TotalOutflows).HasPrecision(18, 3);
            entity.Property(e => e.NetFlow).HasPrecision(18, 3);
            entity.Property(e => e.ClosingBalance).HasPrecision(18, 3);
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.Property(e => e.ConfidencePercent).HasPrecision(5, 2);
            entity.Property(e => e.MethodUsed).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.ComputedAt).IsRequired();
            entity.Property(e => e.DurationMs).IsRequired();
            entity.Property(e => e.InputsJson).HasMaxLength(8000);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.Property(e => e.AiAdjustmentApplied).IsRequired();
            entity.Property(e => e.AiModelRef).HasMaxLength(200);
            entity.Property(e => e.RowVersion).IsRowVersion();

            // Lecture dominante : « le dernier run exploitable pour cet horizon ».
            entity.HasIndex(e => new { e.Status, e.ComputedAt })
                  .HasDatabaseName("IX_CashFlowForecastRuns_StatusComputed");
            entity.HasIndex(e => e.ComputedAt);

            // Un recalcul remplace intégralement le run précédent : les enfants suivent la
            // suppression de leur parent, sinon la table de lignes croît sans fin.
            entity.HasMany(e => e.Lines)
                  .WithOne()
                  .HasForeignKey(l => l.ForecastRunId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Buckets)
                  .WithOne()
                  .HasForeignKey(b => b.ForecastRunId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Scenarios)
                  .WithOne()
                  .HasForeignKey(s => s.ForecastRunId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Insights)
                  .WithOne()
                  .HasForeignKey(i => i.ForecastRunId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(e => e.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(e => e.Buckets).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(e => e.Scenarios).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(e => e.Insights).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }

    private static void ConfigureCashFlowForecastLine(ModelBuilder builder)
    {
        builder.Entity<CashFlowForecastLine>(entity =>
        {
            entity.ToTable("CashFlowForecastLines");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ForecastRunId).IsRequired();
            entity.Property(e => e.Direction).IsRequired();
            entity.Property(e => e.SourceType).IsRequired();
            entity.Property(e => e.SourceReference).HasMaxLength(100);
            entity.Property(e => e.Label).HasMaxLength(300).IsRequired();
            entity.Property(e => e.ThirdPartyName).HasMaxLength(200);
            entity.Property(e => e.ContractualDate).IsRequired();
            entity.Property(e => e.ExpectedDate).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 3);
            entity.Property(e => e.ProbabilityPercent).HasPrecision(5, 2);
            entity.Property(e => e.WeightedAmount).HasPrecision(18, 3);
            entity.Property(e => e.IsConfirmed).IsRequired();

            // Lecture dominante : les flux d'un run triés par date attendue (Top 5, tableau).
            entity.HasIndex(e => new { e.ForecastRunId, e.ExpectedDate })
                  .HasDatabaseName("IX_CashFlowForecastLines_RunExpectedDate");
            entity.HasIndex(e => new { e.ForecastRunId, e.Direction })
                  .HasDatabaseName("IX_CashFlowForecastLines_RunDirection");
            // Traçabilité : « d'où vient cette ligne ? » et détection de double comptage.
            entity.HasIndex(e => new { e.SourceType, e.SourceId })
                  .HasDatabaseName("IX_CashFlowForecastLines_Source");
        });
    }

    private static void ConfigureCashFlowForecastBucket(ModelBuilder builder)
    {
        builder.Entity<CashFlowForecastBucket>(entity =>
        {
            entity.ToTable("CashFlowForecastBuckets");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ForecastRunId).IsRequired();
            entity.Property(e => e.PeriodStart).IsRequired();
            entity.Property(e => e.PeriodEnd).IsRequired();
            entity.Property(e => e.SequenceIndex).IsRequired();
            entity.Property(e => e.OpeningBalance).HasPrecision(18, 3);
            entity.Property(e => e.Inflows).HasPrecision(18, 3);
            entity.Property(e => e.Outflows).HasPrecision(18, 3);
            entity.Property(e => e.NetFlow).HasPrecision(18, 3);
            entity.Property(e => e.ClosingBalance).HasPrecision(18, 3);
            entity.Property(e => e.LowClosingBalance).HasPrecision(18, 3);
            entity.Property(e => e.HighClosingBalance).HasPrecision(18, 3);

            entity.HasIndex(e => new { e.ForecastRunId, e.SequenceIndex })
                  .IsUnique()
                  .HasDatabaseName("IX_CashFlowForecastBuckets_RunSequence");
        });
    }

    private static void ConfigureCashFlowScenario(ModelBuilder builder)
    {
        builder.Entity<CashFlowScenario>(entity =>
        {
            entity.ToTable("CashFlowScenarios");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ForecastRunId).IsRequired();
            entity.Property(e => e.Kind).IsRequired();
            entity.Property(e => e.ClosingBalance).HasPrecision(18, 3);
            entity.Property(e => e.NetFlow).HasPrecision(18, 3);
            entity.Property(e => e.DeterministicProbabilityPercent).HasPrecision(5, 2);
            entity.Property(e => e.ProbabilityPercent).HasPrecision(5, 2);
            entity.Property(e => e.ProbabilitySource).IsRequired();
            entity.Property(e => e.AiRationale).HasMaxLength(1000);
            entity.Property(e => e.AssumptionsJson).HasMaxLength(4000);

            // Un seul scénario de chaque nature par run.
            entity.HasIndex(e => new { e.ForecastRunId, e.Kind })
                  .IsUnique()
                  .HasDatabaseName("IX_CashFlowScenarios_RunKind");
        });
    }

    private static void ConfigureCashFlowForecastInsight(ModelBuilder builder)
    {
        builder.Entity<CashFlowForecastInsight>(entity =>
        {
            entity.ToTable("CashFlowForecastInsights");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ForecastRunId).IsRequired();
            entity.Property(e => e.Kind).IsRequired();
            entity.Property(e => e.Severity).IsRequired();
            entity.Property(e => e.Origin).IsRequired();
            entity.Property(e => e.Impact);
            entity.Property(e => e.ImpactDirection);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Detail).HasMaxLength(1000);
            entity.Property(e => e.EstimatedBalance).HasPrecision(18, 3);
            entity.Property(e => e.SortOrder).IsRequired();

            entity.HasIndex(e => new { e.ForecastRunId, e.Kind, e.SortOrder })
                  .HasDatabaseName("IX_CashFlowForecastInsights_RunKindSort");
        });
    }

    private static void ConfigureRecurringCashCommitment(ModelBuilder builder)
    {
        builder.Entity<RecurringCashCommitment>(entity =>
        {
            entity.ToTable("RecurringCashCommitments");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Label).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Direction).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 3);
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.Property(e => e.Frequency).IsRequired();
            entity.Property(e => e.DayOfMonth).IsRequired();
            entity.Property(e => e.StartDate).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.IsActive).IsRequired();

            // Lecture dominante du collecteur : les engagements actifs couvrant l'horizon.
            entity.HasIndex(e => new { e.IsActive, e.StartDate })
                  .HasDatabaseName("IX_RecurringCashCommitments_ActiveStart");
        });
    }

    private static void ConfigureCashFlowForecastSettings(ModelBuilder builder)
    {
        builder.Entity<CashFlowForecastSettings>(entity =>
        {
            entity.ToTable("CashFlowForecastSettings");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.CriticalThreshold).HasPrecision(18, 3);
            entity.Property(e => e.AlertThreshold).HasPrecision(18, 3);
            entity.Property(e => e.ComfortThreshold).HasPrecision(18, 3);
            entity.Property(e => e.PayrollPaymentDayOfMonth);
        });
    }
}
