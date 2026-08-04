-- Correctif tarification : colonnes d'audit manquantes (CreatedBy / UpdatedBy / Version).
--
-- Cause : les migrations 20260730100000 / 20260730160000 / 20260730200000 ont cree les
-- tables pricing sans les colonnes heritees de Entity / AggregateRoot, alors que le modele
-- EF les mappe. Symptome : HTTP 500 "Invalid column name 'CreatedBy'/'UpdatedBy'/'Version'"
-- sur POST /api/quotes (PromotionResolver), GET /api/pricing/price-lists, promotions,
-- payment-terms.
--
-- A executer sur CHAQUE base tenant si la migration EF ne peut pas etre appliquee par l'API.
-- Idempotent : rejouable sans effet de bord.

BEGIN TRANSACTION;
GO

-- ─── PriceLists (AggregateRoot) ───
IF OBJECT_ID(N'[PriceLists]', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[PriceLists]') AND [name] = N'CreatedBy')
        ALTER TABLE [PriceLists] ADD [CreatedBy] nvarchar(max) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[PriceLists]') AND [name] = N'UpdatedBy')
        ALTER TABLE [PriceLists] ADD [UpdatedBy] nvarchar(max) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[PriceLists]') AND [name] = N'Version')
        ALTER TABLE [PriceLists] ADD [Version] int NOT NULL CONSTRAINT [DF_PriceLists_Version] DEFAULT 1;
END;
GO

-- ─── ClientProductPrices (AggregateRoot) ───
IF OBJECT_ID(N'[ClientProductPrices]', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[ClientProductPrices]') AND [name] = N'CreatedBy')
        ALTER TABLE [ClientProductPrices] ADD [CreatedBy] nvarchar(max) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[ClientProductPrices]') AND [name] = N'UpdatedBy')
        ALTER TABLE [ClientProductPrices] ADD [UpdatedBy] nvarchar(max) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[ClientProductPrices]') AND [name] = N'Version')
        ALTER TABLE [ClientProductPrices] ADD [Version] int NOT NULL CONSTRAINT [DF_ClientProductPrices_Version] DEFAULT 1;
END;
GO

-- ─── Promotions (AggregateRoot) ───
IF OBJECT_ID(N'[Promotions]', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[Promotions]') AND [name] = N'CreatedBy')
        ALTER TABLE [Promotions] ADD [CreatedBy] nvarchar(max) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[Promotions]') AND [name] = N'UpdatedBy')
        ALTER TABLE [Promotions] ADD [UpdatedBy] nvarchar(max) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[Promotions]') AND [name] = N'Version')
        ALTER TABLE [Promotions] ADD [Version] int NOT NULL CONSTRAINT [DF_Promotions_Version] DEFAULT 1;
END;
GO

-- ─── PaymentTermTemplates (AggregateRoot) ───
IF OBJECT_ID(N'[PaymentTermTemplates]', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[PaymentTermTemplates]') AND [name] = N'CreatedBy')
        ALTER TABLE [PaymentTermTemplates] ADD [CreatedBy] nvarchar(max) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[PaymentTermTemplates]') AND [name] = N'UpdatedBy')
        ALTER TABLE [PaymentTermTemplates] ADD [UpdatedBy] nvarchar(max) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[PaymentTermTemplates]') AND [name] = N'Version')
        ALTER TABLE [PaymentTermTemplates] ADD [Version] int NOT NULL CONSTRAINT [DF_PaymentTermTemplates_Version] DEFAULT 1;
END;
GO

-- ─── PriceListItems (Entity) ───
IF OBJECT_ID(N'[PriceListItems]', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[PriceListItems]') AND [name] = N'CreatedBy')
        ALTER TABLE [PriceListItems] ADD [CreatedBy] nvarchar(max) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[PriceListItems]') AND [name] = N'UpdatedBy')
        ALTER TABLE [PriceListItems] ADD [UpdatedBy] nvarchar(max) NULL;
END;
GO

-- ─── PriceListItemTiers (Entity) ───
IF OBJECT_ID(N'[PriceListItemTiers]', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[PriceListItemTiers]') AND [name] = N'CreatedBy')
        ALTER TABLE [PriceListItemTiers] ADD [CreatedBy] nvarchar(max) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[PriceListItemTiers]') AND [name] = N'UpdatedBy')
        ALTER TABLE [PriceListItemTiers] ADD [UpdatedBy] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260731180000_AddPricingAuditColumns_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260731180000_AddPricingAuditColumns_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

-- Verification
-- SELECT t.name, c.name
-- FROM sys.tables t
-- JOIN sys.columns c ON c.object_id = t.object_id
-- WHERE t.name IN ('PriceLists','PriceListItems','PriceListItemTiers','ClientProductPrices','Promotions','PaymentTermTemplates')
--   AND c.name IN ('CreatedBy','UpdatedBy','Version')
-- ORDER BY t.name, c.name;
