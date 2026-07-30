-- Vague 1, lot 5 tranche 5B — paliers quantitatifs des grilles tarifaires.
--
-- A executer sur CHAQUE base tenant si la migration EF ne peut pas etre appliquee par l'API.
-- Idempotent : rejouable sans effet de bord.
--
-- Une table nouvelle uniquement, donc aucun risque sur l'existant : les prix deja en place
-- restent des prix de base sans palier, et se resolvent exactement comme avant.

BEGIN TRANSACTION;
GO

IF OBJECT_ID(N'[PriceListItemTiers]', N'U') IS NULL
BEGIN
    CREATE TABLE [PriceListItemTiers] (
        [Id] uniqueidentifier NOT NULL,
        [PriceListItemId] uniqueidentifier NOT NULL,
        [MinQuantity] decimal(18,4) NOT NULL,
        [UnitPriceHT] decimal(18,3) NOT NULL,
        [UnitPriceHTCurrency] nvarchar(3) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_PriceListItemTiers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PriceListItemTiers_PriceListItems_PriceListItemId] FOREIGN KEY ([PriceListItemId])
            REFERENCES [PriceListItems] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE [name] = N'IX_PriceListItemTiers_PriceListItemId_MinQuantity'
      AND [object_id] = OBJECT_ID(N'[PriceListItemTiers]')
)
BEGIN
    CREATE UNIQUE INDEX [IX_PriceListItemTiers_PriceListItemId_MinQuantity]
        ON [PriceListItemTiers] ([PriceListItemId], [MinQuantity]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730160000_AddPriceListItemTiers_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260730160000_AddPriceListItemTiers_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO
