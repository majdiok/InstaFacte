using FactuTrust.Domain.Entities.AccountingAudit;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Persistence;

public partial class TenantDbContext
{
    public DbSet<AccountingControlRun> AccountingControlRuns => Set<AccountingControlRun>();
    public DbSet<AccountingAnomaly> AccountingAnomalies => Set<AccountingAnomaly>();
    public DbSet<AccountingAnomalyLine> AccountingAnomalyLines => Set<AccountingAnomalyLine>();
    public DbSet<AccountingAnomalyActivity> AccountingAnomalyActivities => Set<AccountingAnomalyActivity>();
    public DbSet<AccountingControlSchedule> AccountingControlSchedules => Set<AccountingControlSchedule>();
    public DbSet<AccountingControlRuleSetting> AccountingControlRuleSettings => Set<AccountingControlRuleSetting>();
    public DbSet<AccountingRevisionNote> AccountingRevisionNotes => Set<AccountingRevisionNote>();

    private static void ConfigureAccountingAudit(ModelBuilder builder)
    {
        builder.Entity<AccountingControlRun>(entity =>
        {
            entity.ToTable("AccountingControlRuns");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TriggeredByUserName).HasMaxLength(256);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.Property(e => e.ModuleCodesFilter).HasMaxLength(500);
            entity.Property(e => e.ComplianceRate).HasPrecision(5, 1);
            entity.Property(e => e.EvaluatedRuleCount).HasDefaultValue(0);
            entity.HasIndex(e => new { e.FiscalYear, e.CompletedAt });
        });

        builder.Entity<AccountingAnomaly>(entity =>
        {
            entity.ToTable("AccountingAnomalies");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RuleCode).HasMaxLength(64).IsRequired();
            entity.Property(e => e.ModuleCode).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Fingerprint).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(4000);
            entity.Property(e => e.Impact).HasMaxLength(2000);
            entity.Property(e => e.AccountRef).HasMaxLength(32);
            entity.Property(e => e.AssignedToUserName).HasMaxLength(256);
            entity.Property(e => e.DeepLinkRoute).HasMaxLength(256);
            entity.Property(e => e.RecommendationsJson).HasMaxLength(4000);
            entity.Property(e => e.IgnoreReason).HasMaxLength(1000);
            entity.Property(e => e.Amount).HasPrecision(18, 3);
            entity.HasIndex(e => e.Fingerprint).IsUnique();
            entity.HasIndex(e => new { e.RunId, e.Severity });
            entity.HasIndex(e => e.Status);
            entity.HasOne(e => e.Run).WithMany(r => r.Anomalies).HasForeignKey(e => e.RunId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AccountingAnomalyLine>(entity =>
        {
            entity.ToTable("AccountingAnomalyLines");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AccountNumber).HasMaxLength(32);
            entity.Property(e => e.Label).HasMaxLength(500);
            entity.Property(e => e.PieceRef).HasMaxLength(100);
            entity.Property(e => e.JustificationStatus).HasMaxLength(64);
            entity.Property(e => e.Debit).HasPrecision(18, 3);
            entity.Property(e => e.Credit).HasPrecision(18, 3);
            entity.HasOne(e => e.Anomaly).WithMany(a => a.Lines).HasForeignKey(e => e.AnomalyId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AccountingAnomalyActivity>(entity =>
        {
            entity.ToTable("AccountingAnomalyActivities");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserName).HasMaxLength(256);
            entity.Property(e => e.Message).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.OldValue).HasMaxLength(500);
            entity.Property(e => e.NewValue).HasMaxLength(500);
            entity.HasOne(e => e.Anomaly).WithMany(a => a.Activities).HasForeignKey(e => e.AnomalyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.AnomalyId);
        });

        builder.Entity<AccountingControlSchedule>(entity =>
        {
            entity.ToTable("AccountingControlSchedules");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.CronExpression).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ModuleCodesFilter).HasMaxLength(500);
            entity.Property(e => e.NotifyEmails).HasMaxLength(1000);
        });

        builder.Entity<AccountingControlRuleSetting>(entity =>
        {
            entity.ToTable("AccountingControlRuleSettings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RuleCode).HasMaxLength(64).IsRequired();
            entity.Property(e => e.JsonOptions).HasMaxLength(4000);
            entity.Property(e => e.DecimalThreshold).HasPrecision(18, 3);
            entity.HasIndex(e => e.RuleCode).IsUnique();
        });

        builder.Entity<AccountingRevisionNote>(entity =>
        {
            entity.ToTable("AccountingRevisionNotes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.GeneratedByUserName).HasMaxLength(256);
            entity.Property(e => e.ModelRef).HasMaxLength(200);
            entity.Property(e => e.FallbackReason).HasMaxLength(1000);
            entity.Property(e => e.ExecutiveSummary).HasMaxLength(4000);
            entity.Property(e => e.TotalImpactAmount).HasPrecision(18, 3);
            // Les notes de travail sont libres et volumineuses : pas de plafond de longueur.
            entity.Property(e => e.ItemsJson).IsRequired();
            entity.HasIndex(e => new { e.FiscalYear, e.GeneratedAt });
            // Une note par run : régénérer remplace, on n'empile pas les versions.
            entity.HasIndex(e => e.RunId).IsUnique();
            entity.HasOne(e => e.Run).WithMany().HasForeignKey(e => e.RunId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
