namespace FactuTrust.Application.Configuration;

public enum TenantProvisioningStrategy
{
    /// <summary>Legacy path: CREATE DATABASE + full EF MigrateAsync.</summary>
    Migrate,

    /// <summary>Restore from a pre-migrated template backup, then apply pending migrations only.</summary>
    TemplateClone
}

public sealed class TenantProvisioningOptions
{
    public const string SectionName = "TenantProvisioning";

    public TenantProvisioningStrategy Strategy { get; set; } = TenantProvisioningStrategy.TemplateClone;

    public string TemplateDatabaseName { get; set; } = "FactuTrust_Tenant_Template";

    /// <summary>Path to the .bak file used for RESTORE. Created/updated by template maintenance.</summary>
    public string? TemplateBackupPath { get; set; }

    /// <summary>Directory for new tenant .mdf/.ldf files when restoring (LocalDB / on-prem).</summary>
    public string? DataFileDirectory { get; set; }

    /// <summary>Drop SQL database when firm registration rolls back after successful CREATE DATABASE.</summary>
    public bool CleanupOrphanDatabasesOnFailure { get; set; } = true;

    /// <summary>
    /// <c>true</c> force BACKUP WITH COMPRESSION, <c>false</c> never compresses,
    /// <c>null</c> tries compression then retries without (LocalDB / Express).
    /// </summary>
    public bool? UseBackupCompression { get; set; }
}
