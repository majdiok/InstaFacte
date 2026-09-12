using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260911120000_AddOdooTimesheetsAlignment_Tenant")]
    public partial class AddOdooTimesheetsAlignment_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.Projects', N'SalesOrderId') IS NULL
    ALTER TABLE [Projects] ADD [SalesOrderId] uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.Projects', N'AnalyticAccountCode') IS NULL
    ALTER TABLE [Projects] ADD [AnalyticAccountCode] nvarchar(50) NULL;
IF COL_LENGTH(N'dbo.Projects', N'MilestonesEnabled') IS NULL
    ALTER TABLE [Projects] ADD [MilestonesEnabled] bit NOT NULL CONSTRAINT [DF_Projects_MilestonesEnabled] DEFAULT (0);
IF COL_LENGTH(N'dbo.Projects', N'AllocatedHours') IS NULL
    ALTER TABLE [Projects] ADD [AllocatedHours] decimal(18,2) NOT NULL CONSTRAINT [DF_Projects_AllocatedHours] DEFAULT (0);

IF COL_LENGTH(N'dbo.ProjectTimeEntries', N'SalesOrderLineId') IS NULL
    ALTER TABLE [ProjectTimeEntries] ADD [SalesOrderLineId] uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.ProjectTimeEntries', N'TimerStartedAtUtc') IS NULL
    ALTER TABLE [ProjectTimeEntries] ADD [TimerStartedAtUtc] datetime2 NULL;
IF COL_LENGTH(N'dbo.ProjectTimeEntries', N'EntrySource') IS NULL
    ALTER TABLE [ProjectTimeEntries] ADD [EntrySource] int NOT NULL CONSTRAINT [DF_ProjectTimeEntries_EntrySource] DEFAULT (0);

IF COL_LENGTH(N'dbo.ProjectMilestones', N'SalesOrderLineId') IS NULL
    ALTER TABLE [ProjectMilestones] ADD [SalesOrderLineId] uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.ProjectMilestones', N'IsReached') IS NULL
    ALTER TABLE [ProjectMilestones] ADD [IsReached] bit NOT NULL CONSTRAINT [DF_ProjectMilestones_IsReached] DEFAULT (0);
IF COL_LENGTH(N'dbo.ProjectMilestones', N'ReachedAt') IS NULL
    ALTER TABLE [ProjectMilestones] ADD [ReachedAt] datetime2 NULL;

IF COL_LENGTH(N'dbo.ProjectTasks', N'MilestoneId') IS NULL
    ALTER TABLE [ProjectTasks] ADD [MilestoneId] uniqueidentifier NULL;

IF COL_LENGTH(N'dbo.SalesOrders', N'ProjectId') IS NULL
    ALTER TABLE [SalesOrders] ADD [ProjectId] uniqueidentifier NULL;

IF COL_LENGTH(N'dbo.Products', N'ServiceInvoicingPolicy') IS NULL
    ALTER TABLE [Products] ADD [ServiceInvoicingPolicy] int NOT NULL CONSTRAINT [DF_Products_ServiceInvoicingPolicy] DEFAULT (1);
IF COL_LENGTH(N'dbo.Products', N'ServiceCreateOnOrder') IS NULL
    ALTER TABLE [Products] ADD [ServiceCreateOnOrder] int NOT NULL CONSTRAINT [DF_Products_ServiceCreateOnOrder] DEFAULT (0);

IF OBJECT_ID(N'dbo.TenantTimesheetSettings', N'U') IS NULL
CREATE TABLE [TenantTimesheetSettings] (
    [Id] uniqueidentifier NOT NULL,
    [BillingRateIndicatorsEnabled] bit NOT NULL,
    [BillingRateLeaderboardEnabled] bit NOT NULL,
    [TimeOffEntriesEnabled] bit NOT NULL,
    [EncodingMethod] int NOT NULL,
    [TimeOffProjectId] uniqueidentifier NULL,
    [TimeOffTaskId] uniqueidentifier NULL,
    [DefaultDailyWorkingHours] decimal(18,2) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(256) NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] nvarchar(256) NULL,
    [Version] int NOT NULL,
    CONSTRAINT [PK_TenantTimesheetSettings] PRIMARY KEY ([Id])
);

IF OBJECT_ID(N'dbo.EmployeeBillingTimeTargets', N'U') IS NULL
CREATE TABLE [EmployeeBillingTimeTargets] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Year] int NOT NULL,
    [Month] int NOT NULL,
    [TargetHours] decimal(18,2) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(256) NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] nvarchar(256) NULL,
    CONSTRAINT [PK_EmployeeBillingTimeTargets] PRIMARY KEY ([Id])
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EmployeeBillingTimeTargets_UserId_Year_Month')
    CREATE UNIQUE INDEX [IX_EmployeeBillingTimeTargets_UserId_Year_Month] ON [EmployeeBillingTimeTargets] ([UserId], [Year], [Month]);

IF OBJECT_ID(N'dbo.TimesheetTips', N'U') IS NULL
CREATE TABLE [TimesheetTips] (
    [Id] uniqueidentifier NOT NULL,
    [Text] nvarchar(500) NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(256) NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] nvarchar(256) NULL,
    CONSTRAINT [PK_TimesheetTips] PRIMARY KEY ([Id])
);

IF OBJECT_ID(N'dbo.ProjectUpdates', N'U') IS NULL
CREATE TABLE [ProjectUpdates] (
    [Id] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [Status] int NOT NULL,
    [ProgressPercent] int NOT NULL,
    [AuthorUserId] uniqueidentifier NOT NULL,
    [UpdateDate] datetime2 NOT NULL,
    [Description] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(256) NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] nvarchar(256) NULL,
    CONSTRAINT [PK_ProjectUpdates] PRIMARY KEY ([Id])
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProjectUpdates_ProjectId')
    CREATE INDEX [IX_ProjectUpdates_ProjectId] ON [ProjectUpdates] ([ProjectId]);

IF OBJECT_ID(N'dbo.TimeOffRequests', N'U') IS NULL
CREATE TABLE [TimeOffRequests] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [HoursPerDay] decimal(18,2) NOT NULL,
    [TypeName] nvarchar(100) NOT NULL,
    [RequiresApproval] bit NOT NULL,
    [Status] int NOT NULL,
    [TimesheetEntryId] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(256) NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] nvarchar(256) NULL,
    CONSTRAINT [PK_TimeOffRequests] PRIMARY KEY ([Id])
);
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.TimeOffRequests', N'U') IS NOT NULL DROP TABLE [TimeOffRequests];
IF OBJECT_ID(N'dbo.ProjectUpdates', N'U') IS NOT NULL DROP TABLE [ProjectUpdates];
IF OBJECT_ID(N'dbo.TimesheetTips', N'U') IS NOT NULL DROP TABLE [TimesheetTips];
IF OBJECT_ID(N'dbo.EmployeeBillingTimeTargets', N'U') IS NOT NULL DROP TABLE [EmployeeBillingTimeTargets];
IF OBJECT_ID(N'dbo.TenantTimesheetSettings', N'U') IS NOT NULL DROP TABLE [TenantTimesheetSettings];
""");
        }
    }
}
