-- Idempotent tenant migration: AddMultiCurrency_Tenant
-- Socle multi-devises : catalogue des devises, taux de change, devise de transaction sur les écritures.
--
-- Strictement additif. Aucune colonne existante n'est modifiée et AUCUNE reprise de données n'est
-- effectuée : les colonnes ajoutées prennent une valeur par défaut qui décrit exactement l'existant
-- (écriture en devise de tenue, taux 1, aucun montant en devise). Rejouable sans effet de bord.
--
-- Rappel de sémantique, à ne jamais inverser :
--   * CurrencyExchangeRates.Rate = unités de devise de TENUE pour UNE unité de devise étrangère
--     (1 EUR = 3,31420 TND  =>  Rate = 3.314200 sur la devise EUR).
--   * JournalEntryLines.DebitCurrency / CreditCurrency (colonnes préexistantes) portent la devise du
--     Money et valent TOUJOURS la devise de tenue. Ce sont elles que somment les états comptables.
--   * JournalEntryLines.DebitAmountInCurrency / CreditAmountInCurrency ne sont significatives que
--     lorsque JournalEntries.CurrencyCode diffère de la devise de tenue ; elles restent à 0 sur tout
--     l'historique.
--
-- Ordre imposé : Currencies d'abord (CurrencyExchangeRates porte une FK vers elle), puis
-- l'amorçage de la devise de tenue, puis seulement l'index unique filtré qui en garantit l'unicité.

IF OBJECT_ID(N'dbo.Currencies', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Currencies
    (
        Id              uniqueidentifier NOT NULL,
        Code            nvarchar(3)      NOT NULL,
        Label           nvarchar(60)     NOT NULL,
        DecimalPlaces   int              NOT NULL,
        RatePeriodicity int              NOT NULL,
        IsActive        bit              NOT NULL,
        IsFunctional    bit              NOT NULL,
        CreatedAt       datetime2        NOT NULL,
        UpdatedAt       datetime2        NULL,
        CreatedBy       nvarchar(max)    NULL,
        UpdatedBy       nvarchar(max)    NULL,
        CONSTRAINT PK_Currencies PRIMARY KEY (Id)
    );
END

IF OBJECT_ID(N'dbo.CurrencyExchangeRates', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CurrencyExchangeRates
    (
        Id         uniqueidentifier NOT NULL,
        CurrencyId uniqueidentifier NOT NULL,
        FiscalYear int              NOT NULL,
        Month      int              NULL,
        Rate       decimal(18,6)    NOT NULL,
        CreatedAt  datetime2        NOT NULL,
        UpdatedAt  datetime2        NULL,
        CreatedBy  nvarchar(max)    NULL,
        UpdatedBy  nvarchar(max)    NULL,
        CONSTRAINT PK_CurrencyExchangeRates PRIMARY KEY (Id),
        CONSTRAINT FK_CurrencyExchangeRates_Currencies_CurrencyId
            FOREIGN KEY (CurrencyId) REFERENCES dbo.Currencies (Id) ON DELETE CASCADE
    );
END

-- Colonnes de transaction sur les écritures. ALTER TABLE et CREATE INDEX restent dans des batches
-- séparés : SQL Server refuse de référencer dans le même batch une colonne ajoutée par ce batch.

IF COL_LENGTH(N'dbo.JournalEntries', N'CurrencyCode') IS NULL
BEGIN
    ALTER TABLE dbo.JournalEntries
        ADD CurrencyCode nvarchar(3) NOT NULL CONSTRAINT DF_JournalEntries_CurrencyCode DEFAULT N'TND';
END

IF COL_LENGTH(N'dbo.JournalEntries', N'ExchangeRate') IS NULL
BEGIN
    ALTER TABLE dbo.JournalEntries
        ADD ExchangeRate decimal(18,6) NOT NULL CONSTRAINT DF_JournalEntries_ExchangeRate DEFAULT 1;
END

IF COL_LENGTH(N'dbo.JournalEntries', N'ExchangeRateOverridden') IS NULL
BEGIN
    ALTER TABLE dbo.JournalEntries
        ADD ExchangeRateOverridden bit NOT NULL CONSTRAINT DF_JournalEntries_ExchangeRateOverridden DEFAULT 0;
END

IF COL_LENGTH(N'dbo.JournalEntryLines', N'DebitAmountInCurrency') IS NULL
BEGIN
    ALTER TABLE dbo.JournalEntryLines
        ADD DebitAmountInCurrency decimal(18,3) NOT NULL CONSTRAINT DF_JournalEntryLines_DebitAmountInCurrency DEFAULT 0;
END

IF COL_LENGTH(N'dbo.JournalEntryLines', N'CreditAmountInCurrency') IS NULL
BEGIN
    ALTER TABLE dbo.JournalEntryLines
        ADD CreditAmountInCurrency decimal(18,3) NOT NULL CONSTRAINT DF_JournalEntryLines_CreditAmountInCurrency DEFAULT 0;
END

-- Amorçage de la devise de tenue. Doit précéder l'index unique filtré IX_Currencies_IsFunctional.
-- Le GUID est identique à celui de la migration C# : chaque tenant a sa propre base, il n'y a donc
-- aucun risque de collision.

IF OBJECT_ID(N'dbo.Currencies', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.Currencies WHERE IsFunctional = 1)
BEGIN
    INSERT INTO dbo.Currencies (Id, Code, Label, DecimalPlaces, RatePeriodicity, IsActive, IsFunctional, CreatedAt)
    VALUES (N'b7d1f3a2-6c4e-4a19-9f52-3d8e1c0b7a41', N'TND', N'Dinar Tunisien', 3, 0, 1, 1, SYSUTCDATETIME());
END

IF OBJECT_ID(N'dbo.Currencies', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Currencies_Code' AND object_id = OBJECT_ID(N'dbo.Currencies'))
BEGIN
    CREATE UNIQUE INDEX IX_Currencies_Code ON dbo.Currencies (Code);
END

IF OBJECT_ID(N'dbo.Currencies', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Currencies_IsFunctional' AND object_id = OBJECT_ID(N'dbo.Currencies'))
BEGIN
    CREATE UNIQUE INDEX IX_Currencies_IsFunctional ON dbo.Currencies (IsFunctional) WHERE IsFunctional = 1;
END

-- Month vaut NULL pour un taux fixe couvrant tout l'exercice. SQL Server considère les NULL comme
-- distincts dans un index unique : l'unicité du taux fixe exige donc son propre index filtré.

IF OBJECT_ID(N'dbo.CurrencyExchangeRates', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CurrencyExchangeRates_Currency_Year_Month' AND object_id = OBJECT_ID(N'dbo.CurrencyExchangeRates'))
BEGIN
    CREATE UNIQUE INDEX IX_CurrencyExchangeRates_Currency_Year_Month
        ON dbo.CurrencyExchangeRates (CurrencyId, FiscalYear, Month) WHERE Month IS NOT NULL;
END

IF OBJECT_ID(N'dbo.CurrencyExchangeRates', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CurrencyExchangeRates_Currency_Year_Fixed' AND object_id = OBJECT_ID(N'dbo.CurrencyExchangeRates'))
BEGIN
    CREATE UNIQUE INDEX IX_CurrencyExchangeRates_Currency_Year_Fixed
        ON dbo.CurrencyExchangeRates (CurrencyId, FiscalYear) WHERE Month IS NULL;
END

-- Marque la migration comme appliquée pour que TenantMigrationGuard ne rejoue pas le C#.

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260909160000_AddMultiCurrency_Tenant')
BEGIN
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20260909160000_AddMultiCurrency_Tenant', N'8.0.1');
END

-- Vérification :
--   SELECT MigrationId FROM dbo.__EFMigrationsHistory WHERE MigrationId LIKE N'%AddMultiCurrency%';
--   SELECT COL_LENGTH(N'dbo.JournalEntries', N'CurrencyCode'),
--          COL_LENGTH(N'dbo.JournalEntries', N'ExchangeRate'),
--          COL_LENGTH(N'dbo.JournalEntries', N'ExchangeRateOverridden'),
--          COL_LENGTH(N'dbo.JournalEntryLines', N'DebitAmountInCurrency'),
--          COL_LENGTH(N'dbo.JournalEntryLines', N'CreditAmountInCurrency');
--   SELECT Code, IsFunctional, DecimalPlaces FROM dbo.Currencies;
