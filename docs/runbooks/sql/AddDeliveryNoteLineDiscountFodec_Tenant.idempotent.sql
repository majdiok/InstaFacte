-- Vague 0 — correctif 3 : remise et FODEC sur les lignes de bon de livraison.
--
-- A executer sur CHAQUE base tenant si la migration EF ne peut pas etre appliquee par l'API.
-- Idempotent : rejouable sans effet de bord.
--
-- Strictement additif : DiscountPercent nait NULL et IsFodecApplicable nait false, donc tous
-- les bons de livraison existants conservent EXACTEMENT les memes totaux. Aucun backfill.

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE [object_id] = OBJECT_ID(N'[DeliveryNoteLines]') AND [name] = N'DiscountPercent'
)
BEGIN
    ALTER TABLE [DeliveryNoteLines] ADD [DiscountPercent] decimal(5,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE [object_id] = OBJECT_ID(N'[DeliveryNoteLines]') AND [name] = N'IsFodecApplicable'
)
BEGIN
    ALTER TABLE [DeliveryNoteLines] ADD [IsFodecApplicable] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE [object_id] = OBJECT_ID(N'[DeliveryNoteLines]') AND [name] = N'FodecRatePercent'
)
BEGIN
    ALTER TABLE [DeliveryNoteLines] ADD [FodecRatePercent] decimal(5,2) NOT NULL DEFAULT 1.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727110000_AddDeliveryNoteLineDiscountFodec_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260727110000_AddDeliveryNoteLineDiscountFodec_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

-- Verification
-- SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
-- WHERE TABLE_NAME = 'DeliveryNoteLines'
--   AND COLUMN_NAME IN ('DiscountPercent', 'IsFodecApplicable', 'FodecRatePercent');
