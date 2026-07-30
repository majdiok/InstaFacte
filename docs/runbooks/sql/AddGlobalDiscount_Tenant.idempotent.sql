-- Vague 1, lot 5 tranche 5B — remise de pied de document (devis, commande, facture).
--
-- A executer sur CHAQUE base tenant si la migration EF ne peut pas etre appliquee par l'API.
-- Idempotent : rejouable sans effet de bord.
--
-- Strictement additif. Les montants sont crees a 0 et le pourcentage a NULL : sur l'existant,
-- la part de remise imputee a chaque ligne vaut zero, et le calcul (remise de ligne -> FODEC ->
-- TVA) reste identique au millime pres.
--
-- La remise est repartie sur les lignes au prorata de leur base HT, de sorte que le FODEC et la
-- base de TVA portent sur ce qui est reellement facture. Le timbre fiscal, droit fixe, n'est
-- pas touche.

BEGIN TRANSACTION;
GO

DECLARE @docTables TABLE ([name] sysname);
INSERT INTO @docTables VALUES (N'Invoices'), (N'Quotes'), (N'SalesOrders');

DECLARE @lineTables TABLE ([name] sysname);
INSERT INTO @lineTables VALUES (N'InvoiceLines'), (N'QuoteLines'), (N'SalesOrderLines');

DECLARE @t sysname, @sql nvarchar(max);

-- En-tetes de document : pourcentage + montant de la remise de pied.
DECLARE docCur CURSOR LOCAL FAST_FORWARD FOR SELECT [name] FROM @docTables;
OPEN docCur;
FETCH NEXT FROM docCur INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns
                   WHERE [object_id] = OBJECT_ID(QUOTENAME(@t)) AND [name] = N'GlobalDiscountPercent')
    BEGIN
        SET @sql = N'ALTER TABLE ' + QUOTENAME(@t) + N' ADD [GlobalDiscountPercent] decimal(5,2) NULL;';
        EXEC sp_executesql @sql;
    END;

    IF NOT EXISTS (SELECT * FROM sys.columns
                   WHERE [object_id] = OBJECT_ID(QUOTENAME(@t)) AND [name] = N'GlobalDiscountAmount')
    BEGIN
        SET @sql = N'ALTER TABLE ' + QUOTENAME(@t) + N' ADD [GlobalDiscountAmount] decimal(18,3) NOT NULL CONSTRAINT '
                 + QUOTENAME(N'DF_' + @t + N'_GlobalDiscountAmount') + N' DEFAULT 0;';
        EXEC sp_executesql @sql;
    END;

    IF NOT EXISTS (SELECT * FROM sys.columns
                   WHERE [object_id] = OBJECT_ID(QUOTENAME(@t)) AND [name] = N'GlobalDiscountAmountCurrency')
    BEGIN
        SET @sql = N'ALTER TABLE ' + QUOTENAME(@t) + N' ADD [GlobalDiscountAmountCurrency] nvarchar(3) NOT NULL CONSTRAINT '
                 + QUOTENAME(N'DF_' + @t + N'_GlobalDiscountAmountCurrency') + N' DEFAULT N''TND'';';
        EXEC sp_executesql @sql;
    END;

    FETCH NEXT FROM docCur INTO @t;
END;
CLOSE docCur;
DEALLOCATE docCur;
GO

-- Lignes : part de la remise de pied imputee a la ligne.
DECLARE @t2 sysname, @sql2 nvarchar(max);
DECLARE @lineTables2 TABLE ([name] sysname);
INSERT INTO @lineTables2 VALUES (N'InvoiceLines'), (N'QuoteLines'), (N'SalesOrderLines');

DECLARE lineCur CURSOR LOCAL FAST_FORWARD FOR SELECT [name] FROM @lineTables2;
OPEN lineCur;
FETCH NEXT FROM lineCur INTO @t2;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns
                   WHERE [object_id] = OBJECT_ID(QUOTENAME(@t2)) AND [name] = N'AllocatedGlobalDiscount')
    BEGIN
        SET @sql2 = N'ALTER TABLE ' + QUOTENAME(@t2) + N' ADD [AllocatedGlobalDiscount] decimal(18,3) NOT NULL CONSTRAINT '
                  + QUOTENAME(N'DF_' + @t2 + N'_AllocatedGlobalDiscount') + N' DEFAULT 0;';
        EXEC sp_executesql @sql2;
    END;

    IF NOT EXISTS (SELECT * FROM sys.columns
                   WHERE [object_id] = OBJECT_ID(QUOTENAME(@t2)) AND [name] = N'AllocatedGlobalDiscountCurrency')
    BEGIN
        SET @sql2 = N'ALTER TABLE ' + QUOTENAME(@t2) + N' ADD [AllocatedGlobalDiscountCurrency] nvarchar(3) NOT NULL CONSTRAINT '
                  + QUOTENAME(N'DF_' + @t2 + N'_AllocatedGlobalDiscountCurrency') + N' DEFAULT N''TND'';';
        EXEC sp_executesql @sql2;
    END;

    FETCH NEXT FROM lineCur INTO @t2;
END;
CLOSE lineCur;
DEALLOCATE lineCur;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730180000_AddGlobalDiscount_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260730180000_AddGlobalDiscount_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO
