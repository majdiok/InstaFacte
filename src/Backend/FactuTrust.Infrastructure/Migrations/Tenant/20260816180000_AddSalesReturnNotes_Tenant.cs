using System;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Adds sales return notes (bons de retour client) and ReturnedQuantity on BL / sales order lines.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260816180000_AddSalesReturnNotes_Tenant")]
    public partial class AddSalesReturnNotes_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.DeliveryNoteLines', N'ReturnedQuantity') IS NULL
    ALTER TABLE [dbo].[DeliveryNoteLines]
        ADD [ReturnedQuantity] decimal(18,4) NOT NULL CONSTRAINT [DF_DeliveryNoteLines_ReturnedQuantity] DEFAULT (0);
""");

            migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.SalesOrderLines', N'ReturnedQuantity') IS NULL
    ALTER TABLE [dbo].[SalesOrderLines]
        ADD [ReturnedQuantity] decimal(18,4) NOT NULL CONSTRAINT [DF_SalesOrderLines_ReturnedQuantity] DEFAULT (0);
""");

            migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[SalesReturnNotes]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SalesReturnNotes] (
        [Id] uniqueidentifier NOT NULL,
        [Number] nvarchar(50) NOT NULL,
        [NumberYear] int NOT NULL,
        [NumberSequence] int NOT NULL,
        [ReturnDate] datetime2 NOT NULL,
        [Status] int NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [DeliveryNoteId] uniqueidentifier NOT NULL,
        [WarehouseId] uniqueidentifier NULL,
        [Reason] nvarchar(500) NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [ConfirmedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_SalesReturnNotes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SalesReturnNotes_Clients_ClientId] FOREIGN KEY ([ClientId])
            REFERENCES [dbo].[Clients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SalesReturnNotes_DeliveryNotes_DeliveryNoteId] FOREIGN KEY ([DeliveryNoteId])
            REFERENCES [dbo].[DeliveryNotes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SalesReturnNotes_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId])
            REFERENCES [dbo].[Warehouses] ([Id]) ON DELETE NO ACTION
    );

    CREATE UNIQUE INDEX [IX_SalesReturnNotes_Number] ON [dbo].[SalesReturnNotes] ([Number]);
    CREATE INDEX [IX_SalesReturnNotes_DeliveryNoteId] ON [dbo].[SalesReturnNotes] ([DeliveryNoteId]);
    CREATE INDEX [IX_SalesReturnNotes_ClientId] ON [dbo].[SalesReturnNotes] ([ClientId]);
    CREATE INDEX [IX_SalesReturnNotes_Status] ON [dbo].[SalesReturnNotes] ([Status]);
    CREATE INDEX [IX_SalesReturnNotes_ReturnDate] ON [dbo].[SalesReturnNotes] ([ReturnDate]);
END
""");

            migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[SalesReturnNoteLines]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SalesReturnNoteLines] (
        [Id] uniqueidentifier NOT NULL,
        [SalesReturnNoteId] uniqueidentifier NOT NULL,
        [DeliveryNoteLineId] uniqueidentifier NOT NULL,
        [LineNumber] int NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ProductCode] nvarchar(50) NOT NULL,
        [Designation] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [Unit] nvarchar(50) NOT NULL,
        [UnitPriceHT] decimal(18,3) NOT NULL,
        [VatRatePercent] int NOT NULL,
        [DiscountPercent] decimal(5,2) NULL,
        [IsFodecApplicable] bit NOT NULL,
        [FodecRatePercent] decimal(5,2) NOT NULL,
        [ReturnedQuantity] decimal(18,4) NOT NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_SalesReturnNoteLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SalesReturnNoteLines_SalesReturnNotes_SalesReturnNoteId] FOREIGN KEY ([SalesReturnNoteId])
            REFERENCES [dbo].[SalesReturnNotes] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_SalesReturnNoteLines_DeliveryNoteLines_DeliveryNoteLineId] FOREIGN KEY ([DeliveryNoteLineId])
            REFERENCES [dbo].[DeliveryNoteLines] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SalesReturnNoteLines_Products_ProductId] FOREIGN KEY ([ProductId])
            REFERENCES [dbo].[Products] ([Id]) ON DELETE NO ACTION
    );

    CREATE INDEX [IX_SalesReturnNoteLines_SalesReturnNoteId] ON [dbo].[SalesReturnNoteLines] ([SalesReturnNoteId]);
    CREATE INDEX [IX_SalesReturnNoteLines_DeliveryNoteLineId] ON [dbo].[SalesReturnNoteLines] ([DeliveryNoteLineId]);
    CREATE INDEX [IX_SalesReturnNoteLines_ProductId] ON [dbo].[SalesReturnNoteLines] ([ProductId]);
END
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[SalesReturnNoteLines]', N'U') IS NOT NULL
    DROP TABLE [dbo].[SalesReturnNoteLines];
""");

            migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[SalesReturnNotes]', N'U') IS NOT NULL
    DROP TABLE [dbo].[SalesReturnNotes];
""");

            migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.DeliveryNoteLines', N'ReturnedQuantity') IS NOT NULL
    ALTER TABLE [dbo].[DeliveryNoteLines] DROP COLUMN [ReturnedQuantity];
""");

            migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.SalesOrderLines', N'ReturnedQuantity') IS NOT NULL
    ALTER TABLE [dbo].[SalesOrderLines] DROP COLUMN [ReturnedQuantity];
""");
        }
    }
}
