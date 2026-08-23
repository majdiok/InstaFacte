-- Bons de retour client (articles livrés non facturés).
--
-- À exécuter sur CHAQUE base tenant si la migration EF ne peut pas être appliquée par l'API.
-- Idempotent : rejouable sans effet de bord.
--
-- Ajoute ReturnedQuantity sur DeliveryNoteLines et SalesOrderLines (défaut 0),
-- puis les tables SalesReturnNotes / SalesReturnNoteLines.

BEGIN TRANSACTION;
GO

IF COL_LENGTH(N'dbo.DeliveryNoteLines', N'ReturnedQuantity') IS NULL
BEGIN
    ALTER TABLE [dbo].[DeliveryNoteLines]
        ADD [ReturnedQuantity] decimal(18,4) NOT NULL
            CONSTRAINT [DF_DeliveryNoteLines_ReturnedQuantity] DEFAULT (0);
    PRINT 'Colonne DeliveryNoteLines.ReturnedQuantity ajoutée.';
END
ELSE
    PRINT 'Colonne DeliveryNoteLines.ReturnedQuantity déjà présente — rien à faire.';
GO

IF COL_LENGTH(N'dbo.SalesOrderLines', N'ReturnedQuantity') IS NULL
BEGIN
    ALTER TABLE [dbo].[SalesOrderLines]
        ADD [ReturnedQuantity] decimal(18,4) NOT NULL
            CONSTRAINT [DF_SalesOrderLines_ReturnedQuantity] DEFAULT (0);
    PRINT 'Colonne SalesOrderLines.ReturnedQuantity ajoutée.';
END
ELSE
    PRINT 'Colonne SalesOrderLines.ReturnedQuantity déjà présente — rien à faire.';
GO

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
    PRINT 'Table SalesReturnNotes créée.';
END
ELSE
    PRINT 'Table SalesReturnNotes déjà présente — rien à faire.';
GO

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
    PRINT 'Table SalesReturnNoteLines créée.';
END
ELSE
    PRINT 'Table SalesReturnNoteLines déjà présente — rien à faire.';
GO

COMMIT TRANSACTION;
GO

-- Vérification
SELECT COL_LENGTH('dbo.DeliveryNoteLines', 'ReturnedQuantity') AS DeliveryNoteLineReturnedQty,
       COL_LENGTH('dbo.SalesOrderLines', 'ReturnedQuantity') AS SalesOrderLineReturnedQty,
       OBJECT_ID('dbo.SalesReturnNotes') AS SalesReturnNotesTable,
       OBJECT_ID('dbo.SalesReturnNoteLines') AS SalesReturnNoteLinesTable;
GO
