using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

[DbContext(typeof(TenantDbContext))]
[Migration("20260807180000_AddAccountingAuditModule_Tenant")]
public partial class AddAccountingAuditModule_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[AccountingControlRuns]', N'U') IS NULL
BEGIN
    CREATE TABLE [AccountingControlRuns] (
        [Id] uniqueidentifier NOT NULL,
        [FiscalYear] int NOT NULL,
        [PeriodFrom] date NOT NULL,
        [PeriodTo] date NOT NULL,
        [StartedAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        [TriggeredByUserId] uniqueidentifier NULL,
        [TriggeredByUserName] nvarchar(256) NULL,
        [Status] int NOT NULL,
        [ComplianceRate] decimal(5,1) NOT NULL,
        [TotalAnomalies] int NOT NULL,
        [BlockingCount] int NOT NULL,
        [WarningCount] int NOT NULL,
        [InfoCount] int NOT NULL,
        [ErrorMessage] nvarchar(2000) NULL,
        [ModuleCodesFilter] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AccountingControlRuns] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_AccountingControlRuns_FiscalYear_CompletedAt] ON [AccountingControlRuns] ([FiscalYear], [CompletedAt]);
END

IF OBJECT_ID(N'[dbo].[AccountingAnomalies]', N'U') IS NULL
BEGIN
    CREATE TABLE [AccountingAnomalies] (
        [Id] uniqueidentifier NOT NULL,
        [RunId] uniqueidentifier NOT NULL,
        [RuleCode] nvarchar(64) NOT NULL,
        [ModuleCode] nvarchar(64) NOT NULL,
        [Fingerprint] nvarchar(128) NOT NULL,
        [Severity] int NOT NULL,
        [Category] int NOT NULL,
        [Title] nvarchar(500) NOT NULL,
        [Description] nvarchar(4000) NULL,
        [Impact] nvarchar(2000) NULL,
        [AccountRef] nvarchar(32) NULL,
        [Amount] decimal(18,3) NOT NULL,
        [PeriodFrom] date NULL,
        [PeriodTo] date NULL,
        [Status] int NOT NULL,
        [AssignedToUserId] uniqueidentifier NULL,
        [AssignedToUserName] nvarchar(256) NULL,
        [DetectedAt] datetime2 NOT NULL,
        [ResolvedAt] datetime2 NULL,
        [DeepLinkRoute] nvarchar(256) NULL,
        [RecommendationsJson] nvarchar(4000) NULL,
        [IgnoreReason] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AccountingAnomalies] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AccountingAnomalies_AccountingControlRuns_RunId] FOREIGN KEY ([RunId]) REFERENCES [AccountingControlRuns] ([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_AccountingAnomalies_Fingerprint] ON [AccountingAnomalies] ([Fingerprint]);
    CREATE INDEX [IX_AccountingAnomalies_RunId_Severity] ON [AccountingAnomalies] ([RunId], [Severity]);
    CREATE INDEX [IX_AccountingAnomalies_Status] ON [AccountingAnomalies] ([Status]);
END

IF OBJECT_ID(N'[dbo].[AccountingAnomalyLines]', N'U') IS NULL
BEGIN
    CREATE TABLE [AccountingAnomalyLines] (
        [Id] uniqueidentifier NOT NULL,
        [AnomalyId] uniqueidentifier NOT NULL,
        [JournalEntryId] uniqueidentifier NULL,
        [JournalEntryLineId] uniqueidentifier NULL,
        [EntryDate] datetime2 NULL,
        [AccountNumber] nvarchar(32) NULL,
        [Label] nvarchar(500) NULL,
        [Debit] decimal(18,3) NOT NULL,
        [Credit] decimal(18,3) NOT NULL,
        [PieceRef] nvarchar(100) NULL,
        [JustificationStatus] nvarchar(64) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AccountingAnomalyLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AccountingAnomalyLines_AccountingAnomalies_AnomalyId] FOREIGN KEY ([AnomalyId]) REFERENCES [AccountingAnomalies] ([Id]) ON DELETE CASCADE
    );
END

IF OBJECT_ID(N'[dbo].[AccountingAnomalyActivities]', N'U') IS NULL
BEGIN
    CREATE TABLE [AccountingAnomalyActivities] (
        [Id] uniqueidentifier NOT NULL,
        [AnomalyId] uniqueidentifier NOT NULL,
        [ActivityType] int NOT NULL,
        [UserId] uniqueidentifier NULL,
        [UserName] nvarchar(256) NULL,
        [Message] nvarchar(2000) NOT NULL,
        [OldValue] nvarchar(500) NULL,
        [NewValue] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AccountingAnomalyActivities] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AccountingAnomalyActivities_AccountingAnomalies_AnomalyId] FOREIGN KEY ([AnomalyId]) REFERENCES [AccountingAnomalies] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AccountingAnomalyActivities_AnomalyId] ON [AccountingAnomalyActivities] ([AnomalyId]);
END

IF OBJECT_ID(N'[dbo].[AccountingControlSchedules]', N'U') IS NULL
BEGIN
    CREATE TABLE [AccountingControlSchedules] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [CronExpression] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL,
        [FiscalYearOffset] int NULL,
        [ModuleCodesFilter] nvarchar(500) NULL,
        [NotifyEmails] nvarchar(1000) NULL,
        [CreatedByUserId] uniqueidentifier NULL,
        [LastRunAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AccountingControlSchedules] PRIMARY KEY ([Id])
    );
END

IF OBJECT_ID(N'[dbo].[AccountingControlRuleSettings]', N'U') IS NULL
BEGIN
    CREATE TABLE [AccountingControlRuleSettings] (
        [Id] uniqueidentifier NOT NULL,
        [RuleCode] nvarchar(64) NOT NULL,
        [IsEnabled] bit NOT NULL,
        [IntThreshold] int NULL,
        [DecimalThreshold] decimal(18,3) NULL,
        [JsonOptions] nvarchar(4000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AccountingControlRuleSettings] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_AccountingControlRuleSettings_RuleCode] ON [AccountingControlRuleSettings] ([RuleCode]);
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[AccountingAnomalyActivities]', N'U') IS NOT NULL DROP TABLE [AccountingAnomalyActivities];
IF OBJECT_ID(N'[dbo].[AccountingAnomalyLines]', N'U') IS NOT NULL DROP TABLE [AccountingAnomalyLines];
IF OBJECT_ID(N'[dbo].[AccountingAnomalies]', N'U') IS NOT NULL DROP TABLE [AccountingAnomalies];
IF OBJECT_ID(N'[dbo].[AccountingControlRuns]', N'U') IS NOT NULL DROP TABLE [AccountingControlRuns];
IF OBJECT_ID(N'[dbo].[AccountingControlSchedules]', N'U') IS NOT NULL DROP TABLE [AccountingControlSchedules];
IF OBJECT_ID(N'[dbo].[AccountingControlRuleSettings]', N'U') IS NOT NULL DROP TABLE [AccountingControlRuleSettings];
""");
    }
}
