-- Core sales document audit columns (CreatedBy, UpdatedBy, Version).
-- MigrationId: 20260803180000_FixCoreDocumentAuditColumns_Tenant
--
-- À exécuter sur CHAQUE base tenant si la migration EF ne peut pas être appliquée
-- par l'API (urgence production). Idempotent : rejouable sans effet de bord.

BEGIN TRANSACTION;
GO

-- P0: Invoices (POS / wizard submit)
IF EXISTS (SELECT * FROM sys.tables WHERE name = N'Invoices')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Invoices]') AND name = N'Version')
        ALTER TABLE [Invoices] ADD [Version] int NOT NULL CONSTRAINT [DF_Invoices_Version] DEFAULT 1;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Invoices]') AND name = N'CreatedBy')
        ALTER TABLE [Invoices] ADD [CreatedBy] nvarchar(max) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Invoices]') AND name = N'UpdatedBy')
        ALTER TABLE [Invoices] ADD [UpdatedBy] nvarchar(max) NULL;
END
GO

-- P0: Clients (création client passager POS)
IF EXISTS (SELECT * FROM sys.tables WHERE name = N'Clients')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Clients]') AND name = N'Version')
        ALTER TABLE [Clients] ADD [Version] int NOT NULL CONSTRAINT [DF_Clients_Version] DEFAULT 1;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Clients]') AND name = N'CreatedBy')
        ALTER TABLE [Clients] ADD [CreatedBy] nvarchar(max) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Clients]') AND name = N'UpdatedBy')
        ALTER TABLE [Clients] ADD [UpdatedBy] nvarchar(max) NULL;
END
GO

-- P1: InvoiceLines
IF EXISTS (SELECT * FROM sys.tables WHERE name = N'InvoiceLines')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[InvoiceLines]') AND name = N'CreatedBy')
        ALTER TABLE [InvoiceLines] ADD [CreatedBy] nvarchar(max) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[InvoiceLines]') AND name = N'UpdatedBy')
        ALTER TABLE [InvoiceLines] ADD [UpdatedBy] nvarchar(max) NULL;
END
GO

-- P1: InvoiceDrafts
IF EXISTS (SELECT * FROM sys.tables WHERE name = N'InvoiceDrafts')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[InvoiceDrafts]') AND name = N'CreatedBy')
        ALTER TABLE [InvoiceDrafts] ADD [CreatedBy] nvarchar(max) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[InvoiceDrafts]') AND name = N'UpdatedBy')
        ALTER TABLE [InvoiceDrafts] ADD [UpdatedBy] nvarchar(max) NULL;
END
GO

-- P2: Quotes
IF EXISTS (SELECT * FROM sys.tables WHERE name = N'Quotes')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Quotes]') AND name = N'Version')
        ALTER TABLE [Quotes] ADD [Version] int NOT NULL CONSTRAINT [DF_Quotes_Version] DEFAULT 1;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Quotes]') AND name = N'CreatedBy')
        ALTER TABLE [Quotes] ADD [CreatedBy] nvarchar(max) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Quotes]') AND name = N'UpdatedBy')
        ALTER TABLE [Quotes] ADD [UpdatedBy] nvarchar(max) NULL;
END
GO

-- P2: SalesOrders
IF EXISTS (SELECT * FROM sys.tables WHERE name = N'SalesOrders')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[SalesOrders]') AND name = N'Version')
        ALTER TABLE [SalesOrders] ADD [Version] int NOT NULL CONSTRAINT [DF_SalesOrders_Version] DEFAULT 1;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[SalesOrders]') AND name = N'CreatedBy')
        ALTER TABLE [SalesOrders] ADD [CreatedBy] nvarchar(max) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[SalesOrders]') AND name = N'UpdatedBy')
        ALTER TABLE [SalesOrders] ADD [UpdatedBy] nvarchar(max) NULL;
END
GO

-- P2: DeliveryNotes
IF EXISTS (SELECT * FROM sys.tables WHERE name = N'DeliveryNotes')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[DeliveryNotes]') AND name = N'Version')
        ALTER TABLE [DeliveryNotes] ADD [Version] int NOT NULL CONSTRAINT [DF_DeliveryNotes_Version] DEFAULT 1;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[DeliveryNotes]') AND name = N'CreatedBy')
        ALTER TABLE [DeliveryNotes] ADD [CreatedBy] nvarchar(max) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[DeliveryNotes]') AND name = N'UpdatedBy')
        ALTER TABLE [DeliveryNotes] ADD [UpdatedBy] nvarchar(max) NULL;
END
GO

-- P2: Products
IF EXISTS (SELECT * FROM sys.tables WHERE name = N'Products')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Products]') AND name = N'Version')
        ALTER TABLE [Products] ADD [Version] int NOT NULL CONSTRAINT [DF_Products_Version] DEFAULT 1;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Products]') AND name = N'CreatedBy')
        ALTER TABLE [Products] ADD [CreatedBy] nvarchar(max) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Products]') AND name = N'UpdatedBy')
        ALTER TABLE [Products] ADD [UpdatedBy] nvarchar(max) NULL;
END
GO

IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NOT NULL
AND NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803180000_FixCoreDocumentAuditColumns_Tenant')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260803180000_FixCoreDocumentAuditColumns_Tenant', N'8.0.1');
END
GO

COMMIT TRANSACTION;
GO
