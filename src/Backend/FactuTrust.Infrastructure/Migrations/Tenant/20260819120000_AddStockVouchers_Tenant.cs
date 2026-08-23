using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

/// <summary>
/// Generic stock vouchers (bons d'entrée BE / bons de sortie BS).
/// Manual SQL migration (EF scaffold blocked by existing snapshot).
/// </summary>
[DbContext(typeof(TenantDbContext))]
[Migration("20260819120000_AddStockVouchers_Tenant")]
public partial class AddStockVouchers_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[StockVouchers]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[StockVouchers] (
        [Id] uniqueidentifier NOT NULL,
        [Number] nvarchar(50) NOT NULL,
        [NumberPrefix] nvarchar(10) NOT NULL,
        [NumberYear] int NOT NULL,
        [NumberSequence] int NOT NULL,
        [Kind] int NOT NULL,
        [Status] int NOT NULL,
        [VoucherDate] datetime2 NOT NULL,
        [WarehouseId] uniqueidentifier NOT NULL,
        [Reason] int NOT NULL,
        [ExternalReference] nvarchar(100) NULL,
        [Notes] nvarchar(2000) NULL,
        [ValidatedAt] datetime2 NULL,
        [CancelledAt] datetime2 NULL,
        [CancellationReason] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL CONSTRAINT [DF_StockVouchers_Version] DEFAULT (1),
        CONSTRAINT [PK_StockVouchers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StockVouchers_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId])
            REFERENCES [dbo].[Warehouses] ([Id]) ON DELETE NO ACTION
    );
    CREATE UNIQUE INDEX [IX_StockVouchers_Number] ON [dbo].[StockVouchers] ([Number]);
    CREATE INDEX [IX_StockVouchers_Status] ON [dbo].[StockVouchers] ([Status]);
    CREATE INDEX [IX_StockVouchers_Kind] ON [dbo].[StockVouchers] ([Kind]);
    CREATE INDEX [IX_StockVouchers_VoucherDate] ON [dbo].[StockVouchers] ([VoucherDate]);
    CREATE INDEX [IX_StockVouchers_WarehouseId] ON [dbo].[StockVouchers] ([WarehouseId]);
    CREATE INDEX [IX_StockVouchers_Kind_Status] ON [dbo].[StockVouchers] ([Kind], [Status]);
END

IF OBJECT_ID(N'[dbo].[StockVoucherLines]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[StockVoucherLines] (
        [Id] uniqueidentifier NOT NULL,
        [StockVoucherId] uniqueidentifier NOT NULL,
        [LineNumber] int NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ProductCode] nvarchar(50) NOT NULL,
        [ProductName] nvarchar(200) NOT NULL,
        [Unit] nvarchar(50) NULL,
        [Quantity] decimal(18, 4) NOT NULL,
        [UnitCost] decimal(18, 4) NOT NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_StockVoucherLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StockVoucherLines_StockVouchers_StockVoucherId] FOREIGN KEY ([StockVoucherId])
            REFERENCES [dbo].[StockVouchers] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_StockVoucherLines_StockVoucherId] ON [dbo].[StockVoucherLines] ([StockVoucherId]);
    CREATE INDEX [IX_StockVoucherLines_ProductId] ON [dbo].[StockVoucherLines] ([ProductId]);
    CREATE UNIQUE INDEX [IX_StockVoucherLines_StockVoucherId_ProductId]
        ON [dbo].[StockVoucherLines] ([StockVoucherId], [ProductId]);
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[StockVoucherLines]', N'U') IS NOT NULL
    DROP TABLE [dbo].[StockVoucherLines];
IF OBJECT_ID(N'[dbo].[StockVouchers]', N'U') IS NOT NULL
    DROP TABLE [dbo].[StockVouchers];
""");
    }
}
