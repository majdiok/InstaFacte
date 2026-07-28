-- Vague 1, lot 1 — commande client (tables SalesOrders et SalesOrderLines).
--
-- A executer sur CHAQUE base tenant si la migration EF ne peut pas etre appliquee par l'API.
-- Idempotent : rejouable sans effet de bord.
--
-- CREATION DE TABLES uniquement : aucune table ni colonne existante n'est touchee. Les cles
-- etrangeres vers Clients, Warehouses et Products sont toutes en RESTRICT — on n'efface jamais
-- un referentiel encore engage dans une commande.

BEGIN TRANSACTION;
GO

IF OBJECT_ID(N'[SalesOrders]', N'U') IS NULL
BEGIN
    CREATE TABLE [SalesOrders] (
        [Id] uniqueidentifier NOT NULL,
        [Number] nvarchar(50) NOT NULL,
        [NumberPrefix] nvarchar(10) NOT NULL,
        [NumberYear] int NOT NULL,
        [NumberSequence] int NOT NULL,
        [OrderDate] datetime2 NOT NULL,
        [ExpectedDeliveryDate] datetime2 NULL,
        [Status] int NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [Reference] nvarchar(100) NULL,
        [Notes] nvarchar(2000) NULL,
        [PaymentTerms] nvarchar(500) NULL,
        [WarehouseId] uniqueidentifier NULL,
        [SourceQuoteId] uniqueidentifier NULL,
        [SubTotal] decimal(18,3) NOT NULL,
        [SubTotalCurrency] nvarchar(3) NOT NULL,
        [FodecAmount] decimal(18,3) NOT NULL,
        [FodecAmountCurrency] nvarchar(3) NOT NULL,
        [TotalVat] decimal(18,3) NOT NULL,
        [TotalVatCurrency] nvarchar(3) NOT NULL,
        [FiscalStampAmount] decimal(18,3) NOT NULL,
        [FiscalStampAmountCurrency] nvarchar(3) NOT NULL,
        [TotalAmount] decimal(18,3) NOT NULL,
        [TotalAmountCurrency] nvarchar(3) NOT NULL,
        [ConfirmedAt] datetime2 NULL,
        [CompletedAt] datetime2 NULL,
        [CancelledAt] datetime2 NULL,
        [CancellationReason] nvarchar(500) NULL,
        [ClosedAt] datetime2 NULL,
        [ClosureReason] nvarchar(500) NULL,
        [IsStockReserved] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_SalesOrders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SalesOrders_Clients_ClientId] FOREIGN KEY ([ClientId])
            REFERENCES [Clients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SalesOrders_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId])
            REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF OBJECT_ID(N'[SalesOrderLines]', N'U') IS NULL
BEGIN
    CREATE TABLE [SalesOrderLines] (
        [Id] uniqueidentifier NOT NULL,
        [SalesOrderId] uniqueidentifier NOT NULL,
        [LineNumber] int NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ProductCode] nvarchar(50) NOT NULL,
        [ProductName] nvarchar(200) NOT NULL,
        [ProductDescription] nvarchar(1000) NULL,
        [Unit] nvarchar(50) NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [DeliveredQuantity] decimal(18,4) NOT NULL,
        [InvoicedQuantity] decimal(18,4) NOT NULL,
        [VatRate] int NOT NULL,
        [DiscountPercent] decimal(5,2) NULL,
        [IsFodecApplicable] bit NOT NULL,
        [FodecRatePercent] decimal(5,2) NOT NULL,
        [UnitPrice] decimal(18,3) NOT NULL,
        [UnitPriceCurrency] nvarchar(3) NOT NULL,
        [DiscountAmount] decimal(18,3) NOT NULL,
        [DiscountAmountCurrency] nvarchar(3) NOT NULL,
        [FodecAmount] decimal(18,3) NOT NULL,
        [FodecAmountCurrency] nvarchar(3) NOT NULL,
        [SubTotal] decimal(18,3) NOT NULL,
        [SubTotalCurrency] nvarchar(3) NOT NULL,
        [VatAmount] decimal(18,3) NOT NULL,
        [VatAmountCurrency] nvarchar(3) NOT NULL,
        [Total] decimal(18,3) NOT NULL,
        [TotalCurrency] nvarchar(3) NOT NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_SalesOrderLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SalesOrderLines_SalesOrders_SalesOrderId] FOREIGN KEY ([SalesOrderId])
            REFERENCES [SalesOrders] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_SalesOrderLines_Products_ProductId] FOREIGN KEY ([ProductId])
            REFERENCES [Products] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE [name] = N'IX_SalesOrders_Number')
    CREATE INDEX [IX_SalesOrders_Number] ON [SalesOrders] ([Number]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE [name] = N'IX_SalesOrders_Status')
    CREATE INDEX [IX_SalesOrders_Status] ON [SalesOrders] ([Status]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE [name] = N'IX_SalesOrders_OrderDate')
    CREATE INDEX [IX_SalesOrders_OrderDate] ON [SalesOrders] ([OrderDate]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE [name] = N'IX_SalesOrders_ClientId')
    CREATE INDEX [IX_SalesOrders_ClientId] ON [SalesOrders] ([ClientId]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE [name] = N'IX_SalesOrders_ExpectedDeliveryDate')
    CREATE INDEX [IX_SalesOrders_ExpectedDeliveryDate] ON [SalesOrders] ([ExpectedDeliveryDate]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE [name] = N'IX_SalesOrders_WarehouseId')
    CREATE INDEX [IX_SalesOrders_WarehouseId] ON [SalesOrders] ([WarehouseId]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE [name] = N'IX_SalesOrders_SourceQuoteId')
    CREATE INDEX [IX_SalesOrders_SourceQuoteId] ON [SalesOrders] ([SourceQuoteId])
        WHERE [SourceQuoteId] IS NOT NULL;
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE [name] = N'IX_SalesOrderLines_SalesOrderId')
    CREATE INDEX [IX_SalesOrderLines_SalesOrderId] ON [SalesOrderLines] ([SalesOrderId]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE [name] = N'IX_SalesOrderLines_ProductId')
    CREATE INDEX [IX_SalesOrderLines_ProductId] ON [SalesOrderLines] ([ProductId]);
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728120000_AddSalesOrders_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260728120000_AddSalesOrders_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

-- Verification
-- SELECT OBJECT_ID('SalesOrders') AS T1, OBJECT_ID('SalesOrderLines') AS T2;  -- non NULL
-- SELECT COUNT(*) FROM sys.indexes WHERE [name] LIKE 'IX_SalesOrder%';        -- doit valoir 9
