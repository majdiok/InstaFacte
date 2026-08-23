using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

/// <summary>
/// POS cash registers, register sessions, Z reports, cart drafts and held tickets.
/// Additive nullable session FKs on Invoices / Payments / CashExpenses.
/// Migration manuelle (scaffold EF bloqué par snapshot existant).
/// </summary>
[DbContext(typeof(TenantDbContext))]
[Migration("20260818180000_AddPosCashRegisterSessions_Tenant")]
public partial class AddPosCashRegisterSessions_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[CashRegisters]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CashRegisters] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [WarehouseId] uniqueidentifier NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL CONSTRAINT [DF_CashRegisters_Version] DEFAULT (1),
        CONSTRAINT [PK_CashRegisters] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CashRegisters_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId])
            REFERENCES [dbo].[Warehouses] ([Id]) ON DELETE NO ACTION
    );
    CREATE UNIQUE INDEX [IX_CashRegisters_Code] ON [dbo].[CashRegisters] ([Code]);
    CREATE INDEX [IX_CashRegisters_WarehouseId] ON [dbo].[CashRegisters] ([WarehouseId]);
    CREATE INDEX [IX_CashRegisters_IsActive] ON [dbo].[CashRegisters] ([IsActive]);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[CashRegisters]', N'U') IS NOT NULL
AND OBJECT_ID(N'[dbo].[Warehouses]', N'U') IS NOT NULL
BEGIN
    INSERT INTO [dbo].[CashRegisters] (
        [Id], [Code], [Name], [WarehouseId], [IsActive],
        [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy], [Version])
    SELECT
        NEWID(),
        LEFT(N'WH-' + UPPER(w.[Code]), 20),
        LEFT(N'Caisse ' + w.[Name], 100),
        w.[Id],
        1,
        SYSUTCDATETIME(),
        NULL,
        NULL,
        NULL,
        1
    FROM [dbo].[Warehouses] w
    WHERE w.[IsActive] = 1
      AND NOT EXISTS (
          SELECT 1 FROM [dbo].[CashRegisters] r WHERE r.[WarehouseId] = w.[Id]);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[CashRegisterSessions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CashRegisterSessions] (
        [Id] uniqueidentifier NOT NULL,
        [CashRegisterId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL,
        [OpenedAt] datetime2 NOT NULL,
        [OpenedByUserId] uniqueidentifier NOT NULL,
        [OpeningFloat] decimal(18,3) NOT NULL,
        [OpeningFloatCurrency] nvarchar(3) NOT NULL,
        [ClosedAt] datetime2 NULL,
        [ClosedByUserId] uniqueidentifier NULL,
        [ClosingCountedCash] decimal(18,3) NULL,
        [ClosingCountedCashCurrency] nvarchar(3) NULL,
        [ClosingExpectedCash] decimal(18,3) NULL,
        [ClosingExpectedCashCurrency] nvarchar(3) NULL,
        [CashVariance] decimal(18,3) NULL,
        [CashVarianceCurrency] nvarchar(3) NULL,
        [ZReportId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL CONSTRAINT [DF_CashRegisterSessions_Version] DEFAULT (1),
        CONSTRAINT [PK_CashRegisterSessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CashRegisterSessions_CashRegisters_CashRegisterId] FOREIGN KEY ([CashRegisterId])
            REFERENCES [dbo].[CashRegisters] ([Id]) ON DELETE NO ACTION
    );
    CREATE UNIQUE INDEX [IX_CashRegisterSessions_OpenPerRegister]
        ON [dbo].[CashRegisterSessions] ([CashRegisterId]) WHERE [Status] = 0;
    CREATE INDEX [IX_CashRegisterSessions_CashRegisterId_OpenedAt]
        ON [dbo].[CashRegisterSessions] ([CashRegisterId], [OpenedAt]);
    CREATE UNIQUE INDEX [IX_CashRegisterSessions_ZReportId]
        ON [dbo].[CashRegisterSessions] ([ZReportId]) WHERE [ZReportId] IS NOT NULL;
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[ZReports]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ZReports] (
        [Id] uniqueidentifier NOT NULL,
        [Number] nvarchar(50) NOT NULL,
        [NumberYear] int NOT NULL,
        [NumberSequence] int NOT NULL,
        [CashRegisterSessionId] uniqueidentifier NOT NULL,
        [GeneratedAt] datetime2 NOT NULL,
        [SnapshotJson] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL CONSTRAINT [DF_ZReports_Version] DEFAULT (1),
        CONSTRAINT [PK_ZReports] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ZReports_CashRegisterSessions_CashRegisterSessionId] FOREIGN KEY ([CashRegisterSessionId])
            REFERENCES [dbo].[CashRegisterSessions] ([Id]) ON DELETE NO ACTION
    );
    CREATE UNIQUE INDEX [IX_ZReports_Number] ON [dbo].[ZReports] ([Number]);
    CREATE UNIQUE INDEX [IX_ZReports_CashRegisterSessionId] ON [dbo].[ZReports] ([CashRegisterSessionId]);
    CREATE INDEX [IX_ZReports_GeneratedAt] ON [dbo].[ZReports] ([GeneratedAt]);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[PosCartDrafts]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PosCartDrafts] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [CashRegisterId] uniqueidentifier NOT NULL,
        [StateJson] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL CONSTRAINT [DF_PosCartDrafts_Version] DEFAULT (1),
        CONSTRAINT [PK_PosCartDrafts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PosCartDrafts_CashRegisters_CashRegisterId] FOREIGN KEY ([CashRegisterId])
            REFERENCES [dbo].[CashRegisters] ([Id]) ON DELETE NO ACTION
    );
    CREATE UNIQUE INDEX [IX_PosCartDrafts_UserId_CashRegisterId]
        ON [dbo].[PosCartDrafts] ([UserId], [CashRegisterId]);
    CREATE INDEX [IX_PosCartDrafts_CashRegisterId] ON [dbo].[PosCartDrafts] ([CashRegisterId]);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[PosHeldTickets]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PosHeldTickets] (
        [Id] uniqueidentifier NOT NULL,
        [CashRegisterId] uniqueidentifier NOT NULL,
        [CashRegisterSessionId] uniqueidentifier NULL,
        [HeldByUserId] uniqueidentifier NOT NULL,
        [Label] nvarchar(200) NOT NULL,
        [TotalTtc] decimal(18,3) NOT NULL,
        [LineCount] int NOT NULL,
        [StateJson] nvarchar(max) NOT NULL,
        [HeldAt] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL CONSTRAINT [DF_PosHeldTickets_Version] DEFAULT (1),
        CONSTRAINT [PK_PosHeldTickets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PosHeldTickets_CashRegisters_CashRegisterId] FOREIGN KEY ([CashRegisterId])
            REFERENCES [dbo].[CashRegisters] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PosHeldTickets_CashRegisterSessions_CashRegisterSessionId] FOREIGN KEY ([CashRegisterSessionId])
            REFERENCES [dbo].[CashRegisterSessions] ([Id]) ON DELETE NO ACTION
    );
    CREATE INDEX [IX_PosHeldTickets_CashRegisterId] ON [dbo].[PosHeldTickets] ([CashRegisterId]);
    CREATE INDEX [IX_PosHeldTickets_CashRegisterSessionId]
        ON [dbo].[PosHeldTickets] ([CashRegisterSessionId]) WHERE [CashRegisterSessionId] IS NOT NULL;
    CREATE INDEX [IX_PosHeldTickets_HeldByUserId] ON [dbo].[PosHeldTickets] ([HeldByUserId]);
    CREATE INDEX [IX_PosHeldTickets_HeldAt] ON [dbo].[PosHeldTickets] ([HeldAt]);
END
""");

        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.Invoices', N'CashRegisterSessionId') IS NULL
    ALTER TABLE [dbo].[Invoices] ADD [CashRegisterSessionId] uniqueidentifier NULL;
""");

        migrationBuilder.Sql("""
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Invoices_CashRegisterSessionId' AND object_id = OBJECT_ID(N'dbo.Invoices'))
    CREATE INDEX [IX_Invoices_CashRegisterSessionId] ON [dbo].[Invoices] ([CashRegisterSessionId])
        WHERE [CashRegisterSessionId] IS NOT NULL;
""");

        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.Payments', N'CashRegisterSessionId') IS NULL
    ALTER TABLE [dbo].[Payments] ADD [CashRegisterSessionId] uniqueidentifier NULL;
""");

        migrationBuilder.Sql("""
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Payments_CashRegisterSessionId' AND object_id = OBJECT_ID(N'dbo.Payments'))
    CREATE INDEX [IX_Payments_CashRegisterSessionId] ON [dbo].[Payments] ([CashRegisterSessionId])
        WHERE [CashRegisterSessionId] IS NOT NULL;
""");

        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.CashExpenses', N'CashRegisterSessionId') IS NULL
    ALTER TABLE [dbo].[CashExpenses] ADD [CashRegisterSessionId] uniqueidentifier NULL;
""");

        migrationBuilder.Sql("""
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CashExpenses_CashRegisterSessionId' AND object_id = OBJECT_ID(N'dbo.CashExpenses'))
    CREATE INDEX [IX_CashExpenses_CashRegisterSessionId] ON [dbo].[CashExpenses] ([CashRegisterSessionId])
        WHERE [CashRegisterSessionId] IS NOT NULL;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CashExpenses_CashRegisterSessionId' AND object_id = OBJECT_ID(N'dbo.CashExpenses'))
    DROP INDEX [IX_CashExpenses_CashRegisterSessionId] ON [dbo].[CashExpenses];
IF COL_LENGTH(N'dbo.CashExpenses', N'CashRegisterSessionId') IS NOT NULL
    ALTER TABLE [dbo].[CashExpenses] DROP COLUMN [CashRegisterSessionId];
""");

        migrationBuilder.Sql("""
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Payments_CashRegisterSessionId' AND object_id = OBJECT_ID(N'dbo.Payments'))
    DROP INDEX [IX_Payments_CashRegisterSessionId] ON [dbo].[Payments];
IF COL_LENGTH(N'dbo.Payments', N'CashRegisterSessionId') IS NOT NULL
    ALTER TABLE [dbo].[Payments] DROP COLUMN [CashRegisterSessionId];
""");

        migrationBuilder.Sql("""
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Invoices_CashRegisterSessionId' AND object_id = OBJECT_ID(N'dbo.Invoices'))
    DROP INDEX [IX_Invoices_CashRegisterSessionId] ON [dbo].[Invoices];
IF COL_LENGTH(N'dbo.Invoices', N'CashRegisterSessionId') IS NOT NULL
    ALTER TABLE [dbo].[Invoices] DROP COLUMN [CashRegisterSessionId];
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[PosHeldTickets]', N'U') IS NOT NULL DROP TABLE [dbo].[PosHeldTickets];
IF OBJECT_ID(N'[dbo].[PosCartDrafts]', N'U') IS NOT NULL DROP TABLE [dbo].[PosCartDrafts];
IF OBJECT_ID(N'[dbo].[ZReports]', N'U') IS NOT NULL DROP TABLE [dbo].[ZReports];
IF OBJECT_ID(N'[dbo].[CashRegisterSessions]', N'U') IS NOT NULL DROP TABLE [dbo].[CashRegisterSessions];
IF OBJECT_ID(N'[dbo].[CashRegisters]', N'U') IS NOT NULL DROP TABLE [dbo].[CashRegisters];
""");
    }
}
