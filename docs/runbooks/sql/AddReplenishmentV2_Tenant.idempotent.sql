BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260509000000_AddInvoiceType_Tenant'
)
BEGIN
    ALTER TABLE [Invoices] ADD [Type] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260509000000_AddInvoiceType_Tenant'
)
BEGIN
    CREATE INDEX [IX_Invoices_Type] ON [Invoices] ([Type]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260509000000_AddInvoiceType_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260509000000_AddInvoiceType_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513124704_AddReplenishmentV2_Tenant'
)
BEGIN
    ALTER TABLE [ReplenishmentRecommendations] ADD [DaysOfStockRemaining] decimal(10,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513124704_AddReplenishmentV2_Tenant'
)
BEGIN
    ALTER TABLE [ReplenishmentRecommendations] ADD [EffectiveQty] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513124704_AddReplenishmentV2_Tenant'
)
BEGIN
    ALTER TABLE [ReplenishmentRecommendations] ADD [ManualQtyOverride] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513124704_AddReplenishmentV2_Tenant'
)
BEGIN
    ALTER TABLE [ReplenishmentRecommendations] ADD [ManualSupplierOverride] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513124704_AddReplenishmentV2_Tenant'
)
BEGIN
    ALTER TABLE [ReplenishmentRecommendations] ADD [PreferredSupplierId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513124704_AddReplenishmentV2_Tenant'
)
BEGIN
    ALTER TABLE [ReplenishmentRecommendations] ADD [PreferredSupplierName] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513124704_AddReplenishmentV2_Tenant'
)
BEGIN
    ALTER TABLE [ReplenishmentRecommendations] ADD [QuantityOnOrder] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513124704_AddReplenishmentV2_Tenant'
)
BEGIN
    ALTER TABLE [ReplenishmentRecommendations] ADD [UserNotes] nvarchar(1000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513124704_AddReplenishmentV2_Tenant'
)
BEGIN
    ALTER TABLE [Products] ADD [LeadTimeDaysOverride] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513124704_AddReplenishmentV2_Tenant'
)
BEGIN
    ALTER TABLE [Products] ADD [MinimumOrderQuantity] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513124704_AddReplenishmentV2_Tenant'
)
BEGIN
    ALTER TABLE [Products] ADD [PackagingQty] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513124704_AddReplenishmentV2_Tenant'
)
BEGIN
    ALTER TABLE [Products] ADD [PackagingUnit] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513124704_AddReplenishmentV2_Tenant'
)
BEGIN
    ALTER TABLE [Products] ADD [PreferredSupplierId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513124704_AddReplenishmentV2_Tenant'
)
BEGIN
    CREATE TABLE [ReplenishmentDecisionAudits] (
        [Id] uniqueidentifier NOT NULL,
        [RecommendationId] uniqueidentifier NOT NULL,
        [FromStatus] int NOT NULL,
        [ToStatus] int NOT NULL,
        [ActionType] nvarchar(32) NOT NULL,
        [Reason] nvarchar(500) NULL,
        [ActorUserId] nvarchar(450) NOT NULL,
        [ActedAt] datetime2 NOT NULL,
        [PayloadJson] nvarchar(4000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ReplenishmentDecisionAudits] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513124704_AddReplenishmentV2_Tenant'
)
BEGIN
    CREATE INDEX [IX_ReplenishmentRecommendations_ManualSupplier] ON [ReplenishmentRecommendations] ([ManualSupplierOverride]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513124704_AddReplenishmentV2_Tenant'
)
BEGIN
    CREATE INDEX [IX_ReplenishmentRecommendations_SupplierStatus] ON [ReplenishmentRecommendations] ([PreferredSupplierId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513124704_AddReplenishmentV2_Tenant'
)
BEGIN
    CREATE INDEX [IX_ReplenishmentDecisionAudits_ActedAt] ON [ReplenishmentDecisionAudits] ([ActedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513124704_AddReplenishmentV2_Tenant'
)
BEGIN
    CREATE INDEX [IX_ReplenishmentDecisionAudits_RecommendationActed] ON [ReplenishmentDecisionAudits] ([RecommendationId], [ActedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513124704_AddReplenishmentV2_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260513124704_AddReplenishmentV2_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

