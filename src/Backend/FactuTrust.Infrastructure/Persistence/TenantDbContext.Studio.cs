using FactuTrust.Domain.Entities.Studio;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Persistence;

/// <summary>
/// EF Core mapping for the low-code "Studio" tables. Kept in its own partial file so the feature
/// is fully additive and isolated from the rest of the tenant schema. All five tables are new
/// (CreateTable-only migration); no existing entity mapping is touched.
/// </summary>
public partial class TenantDbContext
{
    public DbSet<CustomEntityDefinition> CustomEntityDefinitions => Set<CustomEntityDefinition>();
    public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();
    public DbSet<CustomRecord> CustomRecords => Set<CustomRecord>();
    public DbSet<CustomFormDefinition> CustomFormDefinitions => Set<CustomFormDefinition>();
    public DbSet<CustomReportDefinition> CustomReportDefinitions => Set<CustomReportDefinition>();
    public DbSet<CustomViewDefinition> CustomViewDefinitions => Set<CustomViewDefinition>();
    public DbSet<CustomFieldSequence> CustomFieldSequences => Set<CustomFieldSequence>();
    public DbSet<CustomSystemDefinition> CustomSystemDefinitions => Set<CustomSystemDefinition>();
    public DbSet<CustomEntityAutomation> CustomEntityAutomations => Set<CustomEntityAutomation>();
    public DbSet<CustomAutomationRun> CustomAutomationRuns => Set<CustomAutomationRun>();
    public DbSet<StudioAiBuildPlan> StudioAiBuildPlans => Set<StudioAiBuildPlan>();

    private static void ConfigureStudio(ModelBuilder builder)
    {
        builder.Entity<CustomSystemDefinition>(entity =>
        {
            entity.ToTable("CustomSystemDefinitions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Key).HasMaxLength(64).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Icon).HasMaxLength(64);
            entity.Property(e => e.Description).HasMaxLength(1024);
            entity.Property(e => e.OnboardingJson).HasColumnType("nvarchar(max)");

            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.HasIndex(e => new { e.TenantId, e.Key }).IsUnique();
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        builder.Entity<CustomEntityDefinition>(entity =>
        {
            entity.ToTable("CustomEntityDefinitions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Key).HasMaxLength(64).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(128).IsRequired();
            entity.Property(e => e.DisplayNamePlural).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Icon).HasMaxLength(64);
            entity.Property(e => e.Description).HasMaxLength(512);

            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.HasIndex(e => new { e.TenantId, e.Key }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.SystemId });

            // Soft-delete filter on this NEW table only (does not affect existing entities).
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        builder.Entity<CustomFieldDefinition>(entity =>
        {
            entity.ToTable("CustomFieldDefinitions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Key).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Label).HasMaxLength(128).IsRequired();
            entity.Property(e => e.FieldType).HasConversion<int>();
            entity.Property(e => e.ValidationRulesJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.OptionsJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.DefaultValueJson).HasColumnType("nvarchar(max)");

            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.HasIndex(e => new { e.TenantId, e.EntityDefinitionId, e.Key }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.EntityDefinitionId, e.SortOrder });
        });

        builder.Entity<CustomRecord>(entity =>
        {
            entity.ToTable("CustomRecords");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.DataJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(e => e.RowVersion).IsRowVersion();

            // Hot list path: filter by tenant + entity + not-deleted.
            entity.HasIndex(e => new { e.TenantId, e.EntityDefinitionId, e.IsDeleted });

            // Soft-delete filter on this NEW table only.
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        builder.Entity<CustomFormDefinition>(entity =>
        {
            entity.ToTable("CustomFormDefinitions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Key).HasMaxLength(64).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(128).IsRequired();
            entity.Property(e => e.LayoutJson).HasColumnType("nvarchar(max)").IsRequired();

            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.HasIndex(e => new { e.TenantId, e.Key }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.EntityDefinitionId });
        });

        builder.Entity<CustomReportDefinition>(entity =>
        {
            entity.ToTable("CustomReportDefinitions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Key).HasMaxLength(64).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(128).IsRequired();
            entity.Property(e => e.DataSourceKind).HasConversion<int>();
            entity.Property(e => e.DataSourceRef).HasMaxLength(128).IsRequired();
            entity.Property(e => e.DefinitionJson).HasColumnType("nvarchar(max)").IsRequired();

            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.HasIndex(e => new { e.TenantId, e.Key }).IsUnique();
        });

        builder.Entity<CustomViewDefinition>(entity =>
        {
            entity.ToTable("CustomViewDefinitions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Key).HasMaxLength(64).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(128).IsRequired();
            entity.Property(e => e.SourceTable).HasMaxLength(128).IsRequired();
            entity.Property(e => e.DefinitionJson).HasColumnType("nvarchar(max)").IsRequired();

            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.HasIndex(e => new { e.TenantId, e.Key }).IsUnique();
        });

        builder.Entity<CustomFieldSequence>(entity =>
        {
            entity.ToTable("CustomFieldSequences");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.FieldKey).HasMaxLength(64).IsRequired();
            entity.Property(e => e.CurrentValue).IsRequired();

            entity.Property(e => e.RowVersion).IsRowVersion();

            // One counter per (tenant, entity, field) — the atomic allocator upserts on this key.
            entity.HasIndex(e => new { e.TenantId, e.EntityDefinitionId, e.FieldKey }).IsUnique();
        });

        builder.Entity<CustomEntityAutomation>(entity =>
        {
            entity.ToTable("CustomEntityAutomations");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Trigger).HasConversion<int>();
            entity.Property(e => e.ActionKey).HasMaxLength(64).IsRequired();
            entity.Property(e => e.MappingJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(e => e.ConditionJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.ScheduleJson).HasColumnType("nvarchar(max)");

            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.HasIndex(e => new { e.TenantId, e.EntityDefinitionId });
        });

        builder.Entity<CustomAutomationRun>(entity =>
        {
            entity.ToTable("CustomAutomationRuns");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.ResultJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.Error).HasColumnType("nvarchar(max)");
            entity.Property(e => e.IdempotencyKey).HasMaxLength(128);

            // Lookups: runs of an automation, and "did this record already run successfully?" (idempotency).
            entity.HasIndex(e => new { e.TenantId, e.AutomationId, e.RecordId });
        });

        builder.Entity<StudioAiBuildPlan>(entity =>
        {
            entity.ToTable("StudioAiBuildPlans");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Kind).HasConversion<int>();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.SpecJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(e => e.SummaryJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(e => e.ResultJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.ErrorMessage).HasMaxLength(2048);

            // La transition Pending → Executing s'appuie sur ce jeton pour bloquer la double confirmation.
            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.HasIndex(e => new { e.TenantId, e.Status, e.CreatedAt });
        });
    }
}
