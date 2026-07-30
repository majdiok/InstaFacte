-- Vague 1, lot 6 — plafond d'encours et delai de reglement habituel du client.
--
-- A executer sur CHAQUE base tenant si la migration EF ne peut pas etre appliquee par l'API.
-- Idempotent : rejouable sans effet de bord.
--
-- Strictement additif : deux colonnes nullables. Un client sans plafond n'est jamais en
-- depassement, et le plafond ne bloque rien de toute facon — il alimente une alerte.

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE [object_id] = OBJECT_ID(N'[Clients]') AND [name] = N'CreditLimit'
)
BEGIN
    ALTER TABLE [Clients] ADD [CreditLimit] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE [object_id] = OBJECT_ID(N'[Clients]') AND [name] = N'DefaultPaymentTermDays'
)
BEGIN
    ALTER TABLE [Clients] ADD [DefaultPaymentTermDays] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730220000_AddClientCreditTerms_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260730220000_AddClientCreditTerms_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO
