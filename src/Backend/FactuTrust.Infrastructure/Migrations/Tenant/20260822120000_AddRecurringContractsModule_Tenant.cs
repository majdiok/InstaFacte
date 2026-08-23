using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

[DbContext(typeof(TenantDbContext))]
[Migration("20260822120000_AddRecurringContractsModule_Tenant")]
public partial class AddRecurringContractsModule_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.Invoices', N'SourceRecurringContractId') IS NULL
    ALTER TABLE [Invoices] ADD [SourceRecurringContractId] uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.Invoices', N'SourceRecurringContractBillingRunId') IS NULL
    ALTER TABLE [Invoices] ADD [SourceRecurringContractBillingRunId] uniqueidentifier NULL;
""");

        migrationBuilder.Sql("""
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Invoices_SourceRecurringContractId' AND object_id = OBJECT_ID(N'dbo.Invoices'))
    CREATE INDEX [IX_Invoices_SourceRecurringContractId] ON [Invoices] ([SourceRecurringContractId]) WHERE [SourceRecurringContractId] IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Invoices_SourceRecurringContractBillingRunId' AND object_id = OBJECT_ID(N'dbo.Invoices'))
    CREATE INDEX [IX_Invoices_SourceRecurringContractBillingRunId] ON [Invoices] ([SourceRecurringContractBillingRunId]) WHERE [SourceRecurringContractBillingRunId] IS NOT NULL;
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[RecurringContracts]', N'U') IS NULL
BEGIN
    CREATE TABLE [RecurringContracts] (
        [Id] uniqueidentifier NOT NULL,
        [Number] nvarchar(50) NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL,
        [BillingFrequency] int NOT NULL,
        [BillingDayOfMonth] int NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NULL,
        [NextBillingDate] datetime2 NULL,
        [LastBilledPeriodEnd] datetime2 NULL,
        [PaymentTermTemplateId] uniqueidentifier NULL,
        [PriceListId] uniqueidentifier NULL,
        [AutoRenew] bit NOT NULL,
        [NoticePeriodDays] int NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [SourceQuoteId] uniqueidentifier NULL,
        [Reference] nvarchar(100) NULL,
        [Notes] nvarchar(2000) NULL,
        [SetupFeeBilled] bit NOT NULL CONSTRAINT [DF_RecurringContracts_SetupFeeBilled] DEFAULT 0,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(256) NULL,
        [UpdatedBy] nvarchar(256) NULL,
        [Version] int NOT NULL CONSTRAINT [DF_RecurringContracts_Version] DEFAULT 1,
        CONSTRAINT [PK_RecurringContracts] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_RecurringContracts_ClientId] ON [RecurringContracts] ([ClientId]);
    CREATE INDEX [IX_RecurringContracts_Status] ON [RecurringContracts] ([Status]);
    CREATE INDEX [IX_RecurringContracts_NextBillingDate] ON [RecurringContracts] ([NextBillingDate]) WHERE [NextBillingDate] IS NOT NULL;
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[RecurringContractLines]', N'U') IS NULL
BEGIN
    CREATE TABLE [RecurringContractLines] (
        [Id] uniqueidentifier NOT NULL,
        [RecurringContractId] uniqueidentifier NOT NULL,
        [LineType] int NOT NULL,
        [ProductId] uniqueidentifier NULL,
        [Description] nvarchar(500) NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [UnitPriceHT] decimal(18,3) NOT NULL,
        [VatRate] decimal(5,2) NOT NULL,
        [UsageMetricId] uniqueidentifier NULL,
        [IncludedQuantity] decimal(18,3) NULL,
        [OverageUnitPriceHT] decimal(18,3) NULL,
        [EffectiveFrom] datetime2 NOT NULL,
        [EffectiveTo] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(256) NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_RecurringContractLines] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_RecurringContractLines_RecurringContractId] ON [RecurringContractLines] ([RecurringContractId]);
    CREATE INDEX [IX_RecurringContractLines_RecurringContractId_IsActive] ON [RecurringContractLines] ([RecurringContractId], [IsActive]);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[UsageMetrics]', N'U') IS NULL
BEGIN
    CREATE TABLE [UsageMetrics] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Unit] nvarchar(30) NOT NULL,
        [AggregationMode] int NOT NULL,
        [ProductId] uniqueidentifier NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(256) NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_UsageMetrics] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_UsageMetrics_Code] ON [UsageMetrics] ([Code]);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[UsageRecords]', N'U') IS NULL
BEGIN
    CREATE TABLE [UsageRecords] (
        [Id] uniqueidentifier NOT NULL,
        [RecurringContractId] uniqueidentifier NOT NULL,
        [UsageMetricId] uniqueidentifier NOT NULL,
        [PeriodFrom] datetime2 NOT NULL,
        [PeriodTo] datetime2 NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [Source] int NOT NULL,
        [RecordedByUserId] uniqueidentifier NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(256) NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_UsageRecords] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_UsageRecords_Contract_Metric_Period_Source] ON [UsageRecords] ([RecurringContractId], [UsageMetricId], [PeriodFrom], [PeriodTo], [Source]);
    CREATE INDEX [IX_UsageRecords_Contract_Period] ON [UsageRecords] ([RecurringContractId], [PeriodFrom], [PeriodTo]);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[RecurringContractBillingRuns]', N'U') IS NULL
BEGIN
    CREATE TABLE [RecurringContractBillingRuns] (
        [Id] uniqueidentifier NOT NULL,
        [RecurringContractId] uniqueidentifier NOT NULL,
        [PeriodFrom] datetime2 NOT NULL,
        [PeriodTo] datetime2 NOT NULL,
        [Status] int NOT NULL,
        [InvoiceDraftId] uniqueidentifier NULL,
        [InvoiceId] uniqueidentifier NULL,
        [FixedAmount] decimal(18,3) NOT NULL,
        [UsageAmount] decimal(18,3) NOT NULL,
        [ProrationAmount] decimal(18,3) NOT NULL,
        [ErrorMessage] nvarchar(2000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(256) NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_RecurringContractBillingRuns] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_RecurringContractBillingRuns_Contract_Period] ON [RecurringContractBillingRuns] ([RecurringContractId], [PeriodFrom], [PeriodTo]);
    CREATE INDEX [IX_RecurringContractBillingRuns_Status] ON [RecurringContractBillingRuns] ([Status]);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[RecurringContractAmendments]', N'U') IS NULL
BEGIN
    CREATE TABLE [RecurringContractAmendments] (
        [Id] uniqueidentifier NOT NULL,
        [RecurringContractId] uniqueidentifier NOT NULL,
        [AmendmentType] int NOT NULL,
        [EffectiveDate] datetime2 NOT NULL,
        [ProrationPolicy] int NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [SnapshotBeforeJson] nvarchar(max) NULL,
        [SnapshotAfterJson] nvarchar(max) NULL,
        [CreatedByUserId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(256) NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_RecurringContractAmendments] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_RecurringContractAmendments_RecurringContractId] ON [RecurringContractAmendments] ([RecurringContractId]);
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS [RecurringContractAmendments];");
        migrationBuilder.Sql("DROP TABLE IF EXISTS [RecurringContractBillingRuns];");
        migrationBuilder.Sql("DROP TABLE IF EXISTS [UsageRecords];");
        migrationBuilder.Sql("DROP TABLE IF EXISTS [UsageMetrics];");
        migrationBuilder.Sql("DROP TABLE IF EXISTS [RecurringContractLines];");
        migrationBuilder.Sql("DROP TABLE IF EXISTS [RecurringContracts];");
        migrationBuilder.Sql("""
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Invoices_SourceRecurringContractBillingRunId' AND object_id = OBJECT_ID(N'dbo.Invoices'))
    DROP INDEX [IX_Invoices_SourceRecurringContractBillingRunId] ON [Invoices];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Invoices_SourceRecurringContractId' AND object_id = OBJECT_ID(N'dbo.Invoices'))
    DROP INDEX [IX_Invoices_SourceRecurringContractId] ON [Invoices];
IF COL_LENGTH(N'dbo.Invoices', N'SourceRecurringContractBillingRunId') IS NOT NULL
    ALTER TABLE [Invoices] DROP COLUMN [SourceRecurringContractBillingRunId];
IF COL_LENGTH(N'dbo.Invoices', N'SourceRecurringContractId') IS NOT NULL
    ALTER TABLE [Invoices] DROP COLUMN [SourceRecurringContractId];
""");
    }
}
