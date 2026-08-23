using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

/// <summary>
/// Additive stock P0: product variants, lots/serials/expiry, FIFO-LIFO layers, document allocations.
/// Manual SQL (idempotent) — existing StockItems grain is unchanged.
/// ALTER then INDEX/FK are split across Sql() batches so SQL Server can see new columns
/// (same pattern as AddPosCashRegisterSessions_Tenant).
/// </summary>
[DbContext(typeof(TenantDbContext))]
[Migration("20260821180000_AddStockTraceabilityAndVariants_Tenant")]
public partial class AddStockTraceabilityAndVariants_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.Products', N'ParentProductId') IS NULL
    ALTER TABLE [dbo].[Products] ADD [ParentProductId] uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.Products', N'IsVariantTemplate') IS NULL
    ALTER TABLE [dbo].[Products] ADD [IsVariantTemplate] bit NOT NULL CONSTRAINT [DF_Products_IsVariantTemplate] DEFAULT (0);
IF COL_LENGTH(N'dbo.Products', N'TrackingMode') IS NULL
    ALTER TABLE [dbo].[Products] ADD [TrackingMode] int NOT NULL CONSTRAINT [DF_Products_TrackingMode] DEFAULT (0);
IF COL_LENGTH(N'dbo.Products', N'HasExpiryTracking') IS NULL
    ALTER TABLE [dbo].[Products] ADD [HasExpiryTracking] bit NOT NULL CONSTRAINT [DF_Products_HasExpiryTracking] DEFAULT (0);
IF COL_LENGTH(N'dbo.Products', N'PickingPolicy') IS NULL
    ALTER TABLE [dbo].[Products] ADD [PickingPolicy] int NOT NULL CONSTRAINT [DF_Products_PickingPolicy] DEFAULT (0);
IF COL_LENGTH(N'dbo.Products', N'CostingMethod') IS NULL
    ALTER TABLE [dbo].[Products] ADD [CostingMethod] int NOT NULL CONSTRAINT [DF_Products_CostingMethod] DEFAULT (0);
IF COL_LENGTH(N'dbo.Products', N'ExpiryAlertDays') IS NULL
    ALTER TABLE [dbo].[Products] ADD [ExpiryAlertDays] int NULL;
""");

        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.Products', N'ParentProductId') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Products_ParentProductId' AND object_id = OBJECT_ID(N'dbo.Products'))
    CREATE INDEX [IX_Products_ParentProductId] ON [dbo].[Products] ([ParentProductId]);
IF COL_LENGTH(N'dbo.Products', N'IsVariantTemplate') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Products_IsVariantTemplate' AND object_id = OBJECT_ID(N'dbo.Products'))
    CREATE INDEX [IX_Products_IsVariantTemplate] ON [dbo].[Products] ([IsVariantTemplate]);
IF COL_LENGTH(N'dbo.Products', N'ParentProductId') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Products_Products_ParentProductId')
    ALTER TABLE [dbo].[Products] ADD CONSTRAINT [FK_Products_Products_ParentProductId]
        FOREIGN KEY ([ParentProductId]) REFERENCES [dbo].[Products] ([Id]);
""");

        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.StockMovements', N'ProductLotId') IS NULL
    ALTER TABLE [dbo].[StockMovements] ADD [ProductLotId] uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.StockMovements', N'SerialId') IS NULL
    ALTER TABLE [dbo].[StockMovements] ADD [SerialId] uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.StockMovements', N'ValuationLayerId') IS NULL
    ALTER TABLE [dbo].[StockMovements] ADD [ValuationLayerId] uniqueidentifier NULL;
""");

        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.InventoryCountLines', N'ProductLotId') IS NULL
    ALTER TABLE [dbo].[InventoryCountLines] ADD [ProductLotId] uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.InventoryCountLines', N'LotNumber') IS NULL
    ALTER TABLE [dbo].[InventoryCountLines] ADD [LotNumber] nvarchar(50) NULL;
""");

        migrationBuilder.Sql("""
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InventoryCountLines_InventoryId_ProductId' AND object_id = OBJECT_ID(N'dbo.InventoryCountLines'))
    DROP INDEX [IX_InventoryCountLines_InventoryId_ProductId] ON [dbo].[InventoryCountLines];
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.ProductLots', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProductLots] (
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [LotNumber] nvarchar(50) NOT NULL,
        [ExpiryDate] datetime2 NULL,
        [ManufacturedOn] datetime2 NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ProductLots] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductLots_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id])
    );
    CREATE UNIQUE INDEX [IX_ProductLots_ProductId_LotNumber] ON [dbo].[ProductLots] ([ProductId], [LotNumber]);
    CREATE INDEX [IX_ProductLots_ExpiryDate] ON [dbo].[ProductLots] ([ExpiryDate]);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.StockLotBalances', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[StockLotBalances] (
        [Id] uniqueidentifier NOT NULL,
        [StockItemId] uniqueidentifier NOT NULL,
        [ProductLotId] uniqueidentifier NOT NULL,
        [QuantityOnHand] decimal(18,4) NOT NULL,
        [QuantityReserved] decimal(18,4) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_StockLotBalances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StockLotBalances_StockItems_StockItemId] FOREIGN KEY ([StockItemId]) REFERENCES [dbo].[StockItems] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_StockLotBalances_ProductLots_ProductLotId] FOREIGN KEY ([ProductLotId]) REFERENCES [dbo].[ProductLots] ([Id])
    );
    CREATE UNIQUE INDEX [IX_StockLotBalances_StockItemId_ProductLotId] ON [dbo].[StockLotBalances] ([StockItemId], [ProductLotId]);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.ProductSerials', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProductSerials] (
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [SerialNumber] nvarchar(80) NOT NULL,
        [ProductLotId] uniqueidentifier NULL,
        [WarehouseId] uniqueidentifier NULL,
        [Status] int NOT NULL,
        [ExpiryDate] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ProductSerials] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductSerials_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]),
        CONSTRAINT [FK_ProductSerials_ProductLots_ProductLotId] FOREIGN KEY ([ProductLotId]) REFERENCES [dbo].[ProductLots] ([Id])
    );
    CREATE UNIQUE INDEX [IX_ProductSerials_ProductId_SerialNumber] ON [dbo].[ProductSerials] ([ProductId], [SerialNumber]);
    CREATE INDEX [IX_ProductSerials_Product_Warehouse_Status] ON [dbo].[ProductSerials] ([ProductId], [WarehouseId], [Status]);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.StockValuationLayers', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[StockValuationLayers] (
        [Id] uniqueidentifier NOT NULL,
        [StockItemId] uniqueidentifier NOT NULL,
        [ProductLotId] uniqueidentifier NULL,
        [ReceivedAt] datetime2 NOT NULL,
        [OriginalQuantity] decimal(18,4) NOT NULL,
        [RemainingQuantity] decimal(18,4) NOT NULL,
        [UnitCost] decimal(18,4) NOT NULL,
        [SourceReference] nvarchar(100) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_StockValuationLayers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StockValuationLayers_StockItems_StockItemId] FOREIGN KEY ([StockItemId]) REFERENCES [dbo].[StockItems] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_StockValuationLayers_ProductLots_ProductLotId] FOREIGN KEY ([ProductLotId]) REFERENCES [dbo].[ProductLots] ([Id])
    );
    CREATE INDEX [IX_StockValuationLayers_StockItemId_ReceivedAt] ON [dbo].[StockValuationLayers] ([StockItemId], [ReceivedAt]);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.StockDocumentAllocations', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[StockDocumentAllocations] (
        [Id] uniqueidentifier NOT NULL,
        [DocumentKind] int NOT NULL,
        [DocumentLineId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ProductLotId] uniqueidentifier NULL,
        [SerialId] uniqueidentifier NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [UnitCost] decimal(18,4) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_StockDocumentAllocations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StockDocumentAllocations_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]),
        CONSTRAINT [FK_StockDocumentAllocations_ProductLots_ProductLotId] FOREIGN KEY ([ProductLotId]) REFERENCES [dbo].[ProductLots] ([Id]),
        CONSTRAINT [FK_StockDocumentAllocations_ProductSerials_SerialId] FOREIGN KEY ([SerialId]) REFERENCES [dbo].[ProductSerials] ([Id])
    );
    CREATE INDEX [IX_StockDocumentAllocations_Document] ON [dbo].[StockDocumentAllocations] ([DocumentKind], [DocumentLineId]);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.ProductAttributeDefinitions', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProductAttributeDefinitions] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(80) NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL CONSTRAINT [DF_ProductAttributeDefinitions_Version] DEFAULT (1),
        CONSTRAINT [PK_ProductAttributeDefinitions] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_ProductAttributeDefinitions_Code] ON [dbo].[ProductAttributeDefinitions] ([Code]);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.ProductAttributeValues', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProductAttributeValues] (
        [Id] uniqueidentifier NOT NULL,
        [DefinitionId] uniqueidentifier NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(80) NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ProductAttributeValues] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductAttributeValues_Definitions] FOREIGN KEY ([DefinitionId]) REFERENCES [dbo].[ProductAttributeDefinitions] ([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_ProductAttributeValues_DefinitionId_Code] ON [dbo].[ProductAttributeValues] ([DefinitionId], [Code]);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.ProductVariantAxes', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProductVariantAxes] (
        [Id] uniqueidentifier NOT NULL,
        [ParentProductId] uniqueidentifier NOT NULL,
        [DefinitionId] uniqueidentifier NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ProductVariantAxes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductVariantAxes_Products] FOREIGN KEY ([ParentProductId]) REFERENCES [dbo].[Products] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProductVariantAxes_Definitions] FOREIGN KEY ([DefinitionId]) REFERENCES [dbo].[ProductAttributeDefinitions] ([Id])
    );
    CREATE UNIQUE INDEX [IX_ProductVariantAxes_Parent_Definition] ON [dbo].[ProductVariantAxes] ([ParentProductId], [DefinitionId]);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.ProductVariantAttributeValues', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProductVariantAttributeValues] (
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [AttributeValueId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ProductVariantAttributeValues] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductVariantAttributeValues_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProductVariantAttributeValues_Values] FOREIGN KEY ([AttributeValueId]) REFERENCES [dbo].[ProductAttributeValues] ([Id])
    );
    CREATE UNIQUE INDEX [IX_ProductVariantAttributeValues_Product_Value] ON [dbo].[ProductVariantAttributeValues] ([ProductId], [AttributeValueId]);
END
""");

        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.InventoryCountLines', N'ProductLotId') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InventoryCountLines_NoLot' AND object_id = OBJECT_ID(N'dbo.InventoryCountLines'))
    CREATE UNIQUE INDEX [IX_InventoryCountLines_NoLot] ON [dbo].[InventoryCountLines] ([InventoryId], [ProductId]) WHERE [ProductLotId] IS NULL;
""");

        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.InventoryCountLines', N'ProductLotId') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InventoryCountLines_WithLot' AND object_id = OBJECT_ID(N'dbo.InventoryCountLines'))
    CREATE UNIQUE INDEX [IX_InventoryCountLines_WithLot] ON [dbo].[InventoryCountLines] ([InventoryId], [ProductId], [ProductLotId]) WHERE [ProductLotId] IS NOT NULL;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Additive schema — down is intentionally empty (tenant DBs never drop P0 stock columns).
    }
}
