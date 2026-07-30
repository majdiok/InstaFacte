-- Vague 1, lot 5 tranche 5C — promotions datees et conditions de reglement structurees.
--
-- A executer sur CHAQUE base tenant si la migration EF ne peut pas etre appliquee par l'API.
-- Idempotent : rejouable sans effet de bord.
--
-- Deux tables nouvelles uniquement : aucun risque sur l'existant. Le champ texte libre
-- PaymentTerms des documents n'est PAS touche — les documents emis le portent, et il reste ce
-- qui s'imprime ; les modeles servent a le produire et a calculer une echeance exploitable.

BEGIN TRANSACTION;
GO

IF OBJECT_ID(N'[Promotions]', N'U') IS NULL
BEGIN
    CREATE TABLE [Promotions] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [ProductId] uniqueidentifier NULL,
        [ProductCategoryId] uniqueidentifier NULL,
        [ClientId] uniqueidentifier NULL,
        [DiscountType] int NOT NULL,
        [DiscountPercent] decimal(5,2) NULL,
        [DiscountAmount] decimal(18,3) NULL,
        [DiscountAmountCurrency] nvarchar(3) NULL,
        [MinQuantity] decimal(18,4) NOT NULL,
        [StartsOn] datetime2 NOT NULL,
        [EndsOn] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        [Priority] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Promotions] PRIMARY KEY ([Id])
    );
END;
GO

IF OBJECT_ID(N'[PaymentTermTemplates]', N'U') IS NULL
BEGIN
    CREATE TABLE [PaymentTermTemplates] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [DelayDays] int NOT NULL,
        [DueMode] int NOT NULL,
        [DueDayOfMonth] int NULL,
        [EarlyPaymentDiscountPercent] decimal(5,2) NULL,
        [EarlyPaymentDays] int NULL,
        [IsActive] bit NOT NULL,
        [IsDefault] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_PaymentTermTemplates] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE [name] = N'IX_Promotions_IsActive_StartsOn_EndsOn' AND [object_id] = OBJECT_ID(N'[Promotions]')
)
BEGIN
    CREATE INDEX [IX_Promotions_IsActive_StartsOn_EndsOn]
        ON [Promotions] ([IsActive], [StartsOn], [EndsOn]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE [name] = N'IX_Promotions_ProductId' AND [object_id] = OBJECT_ID(N'[Promotions]')
)
BEGIN
    CREATE INDEX [IX_Promotions_ProductId]
        ON [Promotions] ([ProductId]) WHERE [ProductId] IS NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE [name] = N'IX_Promotions_ClientId' AND [object_id] = OBJECT_ID(N'[Promotions]')
)
BEGIN
    CREATE INDEX [IX_Promotions_ClientId]
        ON [Promotions] ([ClientId]) WHERE [ClientId] IS NOT NULL;
END;
GO

-- Une seule condition de reglement par defaut, garantie en base plutot que par discipline.
IF NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE [name] = N'IX_PaymentTermTemplates_IsDefault' AND [object_id] = OBJECT_ID(N'[PaymentTermTemplates]')
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentTermTemplates_IsDefault]
        ON [PaymentTermTemplates] ([IsDefault]) WHERE [IsDefault] = 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730200000_AddPromotionsAndPaymentTerms_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260730200000_AddPromotionsAndPaymentTerms_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO
