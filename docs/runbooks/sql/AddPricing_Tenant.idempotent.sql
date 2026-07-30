-- Vague 1, lot 5 tranche 5A — tarification : grilles, prix negocies, affectation client.
--
-- A executer sur CHAQUE base tenant si la migration EF ne peut pas etre appliquee par l'API.
-- Idempotent : rejouable sans effet de bord.
--
-- Trois tables nouvelles (aucun risque sur l'existant) et une colonne additive sur Clients :
--  - PriceLists / PriceListItems : grilles tarifaires et leurs prix par produit ;
--  - ClientProductPrices : prix negocie ponctuel qui prime sur toute grille ;
--  - Clients.PriceListId nullable : grille affectee au client (NULL = tarif catalogue).
--
-- Les prix HT sont des Money possedes : montant decimal(18,3) + devise nvarchar(3).

BEGIN TRANSACTION;
GO

IF OBJECT_ID(N'[PriceLists]', N'U') IS NULL
BEGIN
    CREATE TABLE [PriceLists] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [IsActive] bit NOT NULL,
        [ValidFrom] datetime2 NULL,
        [ValidUntil] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_PriceLists] PRIMARY KEY ([Id])
    );
END;
GO

IF OBJECT_ID(N'[PriceListItems]', N'U') IS NULL
BEGIN
    CREATE TABLE [PriceListItems] (
        [Id] uniqueidentifier NOT NULL,
        [PriceListId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [UnitPriceHT] decimal(18,3) NOT NULL,
        [UnitPriceHTCurrency] nvarchar(3) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_PriceListItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PriceListItems_PriceLists_PriceListId] FOREIGN KEY ([PriceListId])
            REFERENCES [PriceLists] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'[ClientProductPrices]', N'U') IS NULL
BEGIN
    CREATE TABLE [ClientProductPrices] (
        [Id] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [UnitPriceHT] decimal(18,3) NOT NULL,
        [UnitPriceHTCurrency] nvarchar(3) NOT NULL,
        [IsActive] bit NOT NULL,
        [ValidFrom] datetime2 NULL,
        [ValidUntil] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_ClientProductPrices] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE [object_id] = OBJECT_ID(N'[Clients]') AND [name] = N'PriceListId'
)
BEGIN
    ALTER TABLE [Clients] ADD [PriceListId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE [name] = N'IX_PriceLists_IsActive' AND [object_id] = OBJECT_ID(N'[PriceLists]')
)
BEGIN
    CREATE INDEX [IX_PriceLists_IsActive] ON [PriceLists] ([IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE [name] = N'IX_PriceListItems_PriceListId_ProductId' AND [object_id] = OBJECT_ID(N'[PriceListItems]')
)
BEGIN
    CREATE UNIQUE INDEX [IX_PriceListItems_PriceListId_ProductId]
        ON [PriceListItems] ([PriceListId], [ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE [name] = N'IX_ClientProductPrices_ClientId_ProductId' AND [object_id] = OBJECT_ID(N'[ClientProductPrices]')
)
BEGIN
    CREATE UNIQUE INDEX [IX_ClientProductPrices_ClientId_ProductId]
        ON [ClientProductPrices] ([ClientId], [ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE [name] = N'IX_Clients_PriceListId' AND [object_id] = OBJECT_ID(N'[Clients]')
)
BEGIN
    CREATE INDEX [IX_Clients_PriceListId]
        ON [Clients] ([PriceListId])
        WHERE [PriceListId] IS NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730100000_AddPricing_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260730100000_AddPricing_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO
