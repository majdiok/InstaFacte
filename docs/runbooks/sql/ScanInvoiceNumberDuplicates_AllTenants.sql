-- =============================================================================
-- Balayage des doublons de numéros de facture de vente (Invoices.Number)
-- sur toutes les bases tenant d'une instance SQL Server.
--
-- LECTURE SEULE — ne modifie aucune donnée.
--
-- Contexte : l'exigence fiscale tunisienne impose l'unicité du numéro de facture
-- de vente. Avant de déployer la migration ajoutant l'index UNIQUE sur
-- Invoices.Number, ce balayage doit être exécuté sur TOUTES les bases tenant :
-- une migration unique appliquée sur une base contenant des doublons échouerait
-- au boot du tenant et le bloquerait via TenantMigrationGuard.
--
-- Chemin privilégié : endpoint plateforme (PlatformAdmin)
--   GET /api/platform/migrations/tenants/invoice-number-integrity
-- Ce script est l'équivalent DBA quand l'API n'est pas disponible.
--
-- Adapter @Pattern si la convention de nommage des bases tenant diffère.
-- =============================================================================

SET NOCOUNT ON;

DECLARE @Pattern sysname = N'FactuTrust[_]Tenant[_]%';

DECLARE @db sysname, @sql nvarchar(max);

DECLARE @results TABLE (
    DbName sysname PRIMARY KEY,
    InvoiceCount int NOT NULL,
    DuplicateNumbers int NOT NULL,
    DuplicateRows int NOT NULL,
    HasUniqueIndexOnNumber bit NOT NULL
);
DECLARE @dupDetails TABLE (
    DbName sysname,
    Number nvarchar(50),
    InvoiceId uniqueidentifier,
    Status int,
    IssueDate date,
    CreatedAt datetime2,
    TotalAmount decimal(18, 3)
);
DECLARE @skipped TABLE (DbName sysname, Reason nvarchar(400));

DECLARE db_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT name FROM sys.databases
    WHERE name LIKE @Pattern
      AND state_desc = 'ONLINE'
    ORDER BY name;

OPEN db_cursor;
FETCH NEXT FROM db_cursor INTO @db;
WHILE @@FETCH_STATUS = 0
BEGIN
    BEGIN TRY
        SET @sql = N'
            IF OBJECT_ID(N''' + QUOTENAME(@db) + N'.dbo.Invoices'', N''U'') IS NULL
                SELECT CAST(NULL AS int), CAST(NULL AS int), CAST(NULL AS int), CAST(NULL AS bit);
            ELSE
                SELECT
                    (SELECT COUNT(*) FROM ' + QUOTENAME(@db) + N'.dbo.Invoices),
                    (SELECT COUNT(*) FROM (SELECT Number FROM ' + QUOTENAME(@db) + N'.dbo.Invoices GROUP BY Number HAVING COUNT(*) > 1) d),
                    (SELECT ISNULL(SUM(c), 0) FROM (SELECT COUNT(*) AS c FROM ' + QUOTENAME(@db) + N'.dbo.Invoices GROUP BY Number HAVING COUNT(*) > 1) d),
                    (SELECT CASE WHEN EXISTS (
                        SELECT 1
                        FROM ' + QUOTENAME(@db) + N'.sys.indexes ix
                        INNER JOIN ' + QUOTENAME(@db) + N'.sys.index_columns ic
                            ON ic.object_id = ix.object_id AND ic.index_id = ix.index_id AND ic.key_ordinal = 1
                        INNER JOIN ' + QUOTENAME(@db) + N'.sys.columns c
                            ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                        WHERE ix.object_id = OBJECT_ID(N''' + QUOTENAME(@db) + N'.dbo.Invoices'')
                          AND ix.is_unique = 1
                          AND c.name = N''Number'') THEN 1 ELSE 0 END);';

        DECLARE @invoiceCount int, @dupNumbers int, @dupRows int, @hasUnique bit;
        DECLARE @t TABLE (InvoiceCount int, DupNumbers int, DupRows int, HasUnique bit);
        DELETE FROM @t;
        INSERT INTO @t EXEC sp_executesql @sql;
        SELECT @invoiceCount = InvoiceCount, @dupNumbers = DupNumbers, @dupRows = DupRows, @hasUnique = HasUnique FROM @t;

        IF @invoiceCount IS NULL
            INSERT INTO @skipped VALUES (@db, N'Pas de table Invoices');
        ELSE
        BEGIN
            INSERT INTO @results VALUES (@db, @invoiceCount, @dupNumbers, @dupRows, ISNULL(@hasUnique, 0));
            IF @dupNumbers > 0
            BEGIN
                SET @sql = N'
                    SELECT ''' + @db + N''', i.Number, i.Id, i.Status, CAST(i.IssueDate AS date), i.CreatedAt, i.TotalAmount
                    FROM ' + QUOTENAME(@db) + N'.dbo.Invoices i
                    WHERE i.Number IN (
                        SELECT Number FROM ' + QUOTENAME(@db) + N'.dbo.Invoices
                        GROUP BY Number HAVING COUNT(*) > 1)
                    ORDER BY i.Number, i.CreatedAt;';
                INSERT INTO @dupDetails EXEC sp_executesql @sql;
            END
        END
    END TRY
    BEGIN CATCH
        INSERT INTO @skipped VALUES (@db, LEFT(ERROR_MESSAGE(), 400));
    END CATCH
    FETCH NEXT FROM db_cursor INTO @db;
END
CLOSE db_cursor;
DEALLOCATE db_cursor;

-- Synthèse : l'index unique n'est déployable que si BasesAvecDoublons = 0
-- ET BasesIgnorees = 0 (fail-closed : une base injoignable n'est pas "propre").
SELECT
    COUNT(*) AS BasesScannees,
    ISNULL(SUM(InvoiceCount), 0) AS TotalFactures,
    SUM(CASE WHEN DuplicateNumbers > 0 THEN 1 ELSE 0 END) AS BasesAvecDoublons,
    ISNULL(SUM(DuplicateNumbers), 0) AS NumerosEnDoublon,
    ISNULL(SUM(DuplicateRows), 0) AS FacturesConcernees,
    (SELECT COUNT(*) FROM @skipped) AS BasesIgnorees
FROM @results;

-- Bases avec doublons (arbitrage requis avant toute migration d'index unique)
SELECT * FROM @results WHERE DuplicateNumbers > 0 ORDER BY DbName;

-- Détail par facture (Status : 0=Brouillon, 1=Validée, 2=Signée, 4=Payée,
-- 5=Partiellement payée, 6=En retard, 7=Annulée, 8=Archivée)
SELECT * FROM @dupDetails ORDER BY DbName, Number, CreatedAt;

-- Bases ignorées ou en erreur
SELECT * FROM @skipped ORDER BY DbName;
