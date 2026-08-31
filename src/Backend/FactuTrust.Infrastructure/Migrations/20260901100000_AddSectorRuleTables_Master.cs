using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations;

/// <summary>
/// Phase 2 — moteur de règles sectorielles en base (plan §WP-B1). Nine additive master tables:
/// the editable rule set (<c>SectorSegments</c>, <c>SectorDomains</c>, <c>SectorSegmentDomains</c>,
/// <c>SectorModuleRules</c>, <c>SectorModuleDependencies</c>, <c>SectorDefaultSettings</c>,
/// <c>SectorDataTemplates</c>, <c>SectorDataTemplateItems</c>) plus the single-row
/// <c>SectorRuleSetStamps</c> cache-busting version stamp. Hand-written idempotent SQL, following
/// the <c>AddExchangeRequestComments_Master</c> / <c>AddTenantSectorClassification_Master</c>
/// pattern (no Designer file). Unused until <c>Features:RegistrationSector:UseDbRules</c> is
/// flipped on (WP-B2) — no behavior change from this migration alone.
/// </summary>
[DbContext(typeof(MasterDbContext))]
[Migration("20260901100000_AddSectorRuleTables_Master")]
public partial class AddSectorRuleTables_Master : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.SectorSegments', N'U') IS NULL
BEGIN
    CREATE TABLE [SectorSegments] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [LabelFr] nvarchar(100) NOT NULL,
        [DescriptionFr] nvarchar(300) NOT NULL,
        [IconKey] nvarchar(50) NOT NULL,
        [SortOrder] int NOT NULL,
        [DefaultWarehouseName] nvarchar(100) NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_SectorSegments_IsActive] DEFAULT 1,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(450) NULL,
        [UpdatedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_SectorSegments] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [UX_SectorSegments_Code] ON [SectorSegments] ([Code]);
END

IF OBJECT_ID(N'dbo.SectorDomains', N'U') IS NULL
BEGIN
    CREATE TABLE [SectorDomains] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [LabelFr] nvarchar(100) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_SectorDomains_IsActive] DEFAULT 1,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(450) NULL,
        [UpdatedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_SectorDomains] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [UX_SectorDomains_Code] ON [SectorDomains] ([Code]);
END

IF OBJECT_ID(N'dbo.SectorSegmentDomains', N'U') IS NULL
BEGIN
    CREATE TABLE [SectorSegmentDomains] (
        [Id] uniqueidentifier NOT NULL,
        [SegmentId] uniqueidentifier NOT NULL,
        [DomainId] uniqueidentifier NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_SectorSegmentDomains_IsActive] DEFAULT 1,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(450) NULL,
        [UpdatedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_SectorSegmentDomains] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SectorSegmentDomains_SectorSegments] FOREIGN KEY ([SegmentId]) REFERENCES [SectorSegments] ([Id]),
        CONSTRAINT [FK_SectorSegmentDomains_SectorDomains] FOREIGN KEY ([DomainId]) REFERENCES [SectorDomains] ([Id])
    );
    CREATE UNIQUE INDEX [UX_SectorSegmentDomains_Segment_Domain] ON [SectorSegmentDomains] ([SegmentId], [DomainId]);
END

IF OBJECT_ID(N'dbo.SectorModuleRules', N'U') IS NULL
BEGIN
    CREATE TABLE [SectorModuleRules] (
        [Id] uniqueidentifier NOT NULL,
        [RuleKind] int NOT NULL,
        [SegmentId] uniqueidentifier NULL,
        [DomainId] uniqueidentifier NULL,
        [ModuleId] int NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_SectorModuleRules_IsActive] DEFAULT 1,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(450) NULL,
        [UpdatedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_SectorModuleRules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SectorModuleRules_SectorSegments] FOREIGN KEY ([SegmentId]) REFERENCES [SectorSegments] ([Id]),
        CONSTRAINT [FK_SectorModuleRules_SectorDomains] FOREIGN KEY ([DomainId]) REFERENCES [SectorDomains] ([Id]),
        CONSTRAINT [CK_SectorModuleRules_Kind] CHECK (
            ([RuleKind] = 0 AND [SegmentId] IS NOT NULL AND [DomainId] IS NULL) OR
            ([RuleKind] = 1 AND [DomainId] IS NOT NULL AND [SegmentId] IS NULL)
        )
    );
    CREATE UNIQUE INDEX [UX_SectorModuleRules_Kind_Segment_Domain_Module]
        ON [SectorModuleRules] ([RuleKind], [SegmentId], [DomainId], [ModuleId]);
END

IF OBJECT_ID(N'dbo.SectorModuleDependencies', N'U') IS NULL
BEGIN
    CREATE TABLE [SectorModuleDependencies] (
        [Id] uniqueidentifier NOT NULL,
        [ModuleId] int NOT NULL,
        [RequiredModuleId] int NOT NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_SectorModuleDependencies_IsActive] DEFAULT 1,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(450) NULL,
        [UpdatedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_SectorModuleDependencies] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_SectorModuleDependencies_NoSelfRef] CHECK ([ModuleId] <> [RequiredModuleId])
    );
    CREATE UNIQUE INDEX [UX_SectorModuleDependencies_Module_Required]
        ON [SectorModuleDependencies] ([ModuleId], [RequiredModuleId]);
END

IF OBJECT_ID(N'dbo.SectorDefaultSettings', N'U') IS NULL
BEGIN
    CREATE TABLE [SectorDefaultSettings] (
        [Id] uniqueidentifier NOT NULL,
        [SegmentCode] nvarchar(50) NULL,
        [DomainCode] nvarchar(50) NULL,
        [SettingKey] nvarchar(100) NOT NULL,
        [SettingValue] nvarchar(max) NOT NULL,
        [ValueType] nvarchar(20) NOT NULL CONSTRAINT [DF_SectorDefaultSettings_ValueType] DEFAULT 'string',
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_SectorDefaultSettings_IsActive] DEFAULT 1,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(450) NULL,
        [UpdatedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_SectorDefaultSettings] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [UX_SectorDefaultSettings_Segment_Domain_Key]
        ON [SectorDefaultSettings] ([SettingKey], [SegmentCode], [DomainCode]);
END

IF OBJECT_ID(N'dbo.SectorDataTemplates', N'U') IS NULL
BEGIN
    CREATE TABLE [SectorDataTemplates] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [SegmentCode] nvarchar(50) NULL,
        [DomainCode] nvarchar(50) NULL,
        [LabelFr] nvarchar(150) NOT NULL,
        [DescriptionFr] nvarchar(300) NULL,
        [Version] int NOT NULL CONSTRAINT [DF_SectorDataTemplates_Version] DEFAULT 1,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_SectorDataTemplates_IsActive] DEFAULT 1,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(450) NULL,
        [UpdatedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_SectorDataTemplates] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [UX_SectorDataTemplates_Code] ON [SectorDataTemplates] ([Code]);
END

IF OBJECT_ID(N'dbo.SectorDataTemplateItems', N'U') IS NULL
BEGIN
    CREATE TABLE [SectorDataTemplateItems] (
        [Id] uniqueidentifier NOT NULL,
        [TemplateId] uniqueidentifier NOT NULL,
        [ItemKind] nvarchar(30) NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_SectorDataTemplateItems_IsActive] DEFAULT 1,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(450) NULL,
        [UpdatedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_SectorDataTemplateItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SectorDataTemplateItems_SectorDataTemplates] FOREIGN KEY ([TemplateId])
            REFERENCES [SectorDataTemplates] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_SectorDataTemplateItems_TemplateId] ON [SectorDataTemplateItems] ([TemplateId]);
END

IF OBJECT_ID(N'dbo.SectorRuleSetStamps', N'U') IS NULL
BEGIN
    CREATE TABLE [SectorRuleSetStamps] (
        [Id] int NOT NULL,
        [Version] bigint NOT NULL CONSTRAINT [DF_SectorRuleSetStamps_Version] DEFAULT 0,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [UpdatedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_SectorRuleSetStamps] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_SectorRuleSetStamps_SingleRow] CHECK ([Id] = 1)
    );
END

IF NOT EXISTS (SELECT 1 FROM [SectorRuleSetStamps] WHERE [Id] = 1)
    INSERT INTO [SectorRuleSetStamps] ([Id], [Version], [UpdatedAtUtc], [UpdatedBy])
    VALUES (1, 0, SYSUTCDATETIME(), NULL);
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.SectorRuleSetStamps', N'U') IS NOT NULL
    DROP TABLE [SectorRuleSetStamps];
IF OBJECT_ID(N'dbo.SectorDataTemplateItems', N'U') IS NOT NULL
    DROP TABLE [SectorDataTemplateItems];
IF OBJECT_ID(N'dbo.SectorDataTemplates', N'U') IS NOT NULL
    DROP TABLE [SectorDataTemplates];
IF OBJECT_ID(N'dbo.SectorDefaultSettings', N'U') IS NOT NULL
    DROP TABLE [SectorDefaultSettings];
IF OBJECT_ID(N'dbo.SectorModuleDependencies', N'U') IS NOT NULL
    DROP TABLE [SectorModuleDependencies];
IF OBJECT_ID(N'dbo.SectorModuleRules', N'U') IS NOT NULL
    DROP TABLE [SectorModuleRules];
IF OBJECT_ID(N'dbo.SectorSegmentDomains', N'U') IS NOT NULL
    DROP TABLE [SectorSegmentDomains];
IF OBJECT_ID(N'dbo.SectorDomains', N'U') IS NOT NULL
    DROP TABLE [SectorDomains];
IF OBJECT_ID(N'dbo.SectorSegments', N'U') IS NOT NULL
    DROP TABLE [SectorSegments];
""");
    }
}
