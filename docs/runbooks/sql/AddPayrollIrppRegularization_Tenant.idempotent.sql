-- Idempotent tenant migration: AddPayrollIrppRegularization_Tenant
-- Paie : régularisation IRPP/CSS annuelle (décembre et solde de tout compte).
-- Crée la table des régularisations, ajoute les colonnes de portage sur le bulletin et le
-- cycle, et le drapeau d'activation par exercice.
--
-- Tous les défauts sont neutres (0 / false) : les bulletins et cycles déjà calculés
-- conservent exactement leur montant, et la fonctionnalité reste inactive tant que
-- l'exercice n'active pas EnableIrppRegularization.
--
-- Rejouable : chaque bloc est gardé, l'exécuter deux fois est sans effet.

IF OBJECT_ID(N'dbo.PayrollIrppRegularizations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PayrollIrppRegularizations
    (
        Id                  uniqueidentifier NOT NULL,
        EmployeeId          uniqueidentifier NOT NULL,
        Year                int              NOT NULL,
        Month               int              NOT NULL,
        Reason              int              NOT NULL,
        MonthsCounted       int              NOT NULL,
        CumulNetTaxable     decimal(18,3)    NOT NULL,
        CumulIrppWithheld   decimal(18,3)    NOT NULL,
        CumulCssWithheld    decimal(18,3)    NOT NULL,
        IrppDue             decimal(18,3)    NOT NULL,
        CssDue              decimal(18,3)    NOT NULL,
        ComputedIrppDelta   decimal(18,3)    NOT NULL,
        ComputedCssDelta    decimal(18,3)    NOT NULL,
        OverrideIrppDelta   decimal(18,3)    NULL,
        OverrideCssDelta    decimal(18,3)    NULL,
        Notes               nvarchar(1000)   NULL,
        DetailJson          nvarchar(max)    NULL,
        CreatedAt           datetime2        NOT NULL,
        UpdatedAt           datetime2        NULL,
        CreatedBy           nvarchar(max)    NULL,
        UpdatedBy           nvarchar(max)    NULL,
        Version             int              NOT NULL,
        CONSTRAINT PK_PayrollIrppRegularizations PRIMARY KEY (Id)
    );
END

-- Une seule régularisation par salarié et par mois : la régénération remplace la ligne.
IF OBJECT_ID(N'dbo.PayrollIrppRegularizations', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM sys.indexes
       WHERE name = N'IX_PayrollIrppRegularizations_EmployeeId_Year_Month'
         AND object_id = OBJECT_ID(N'dbo.PayrollIrppRegularizations'))
BEGIN
    CREATE UNIQUE INDEX IX_PayrollIrppRegularizations_EmployeeId_Year_Month
        ON dbo.PayrollIrppRegularizations (EmployeeId, Year, Month);
END

IF OBJECT_ID(N'dbo.PayrollIrppRegularizations', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM sys.indexes
       WHERE name = N'IX_PayrollIrppRegularizations_Year_Month'
         AND object_id = OBJECT_ID(N'dbo.PayrollIrppRegularizations'))
BEGIN
    CREATE INDEX IX_PayrollIrppRegularizations_Year_Month
        ON dbo.PayrollIrppRegularizations (Year, Month);
END

-- Portage sur le bulletin : montants signés (positif = rappel, négatif = restitution).
IF OBJECT_ID(N'dbo.Payslips', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.Payslips', N'IrppRegularization') IS NULL
    BEGIN
        ALTER TABLE dbo.Payslips ADD IrppRegularization decimal(18,3) NOT NULL CONSTRAINT DF_Payslips_IrppRegularization DEFAULT 0;
    END

    IF COL_LENGTH(N'dbo.Payslips', N'CssRegularization') IS NULL
    BEGIN
        ALTER TABLE dbo.Payslips ADD CssRegularization decimal(18,3) NOT NULL CONSTRAINT DF_Payslips_CssRegularization DEFAULT 0;
    END

    IF COL_LENGTH(N'dbo.Payslips', N'RegularizationDeferred') IS NULL
    BEGIN
        ALTER TABLE dbo.Payslips ADD RegularizationDeferred decimal(18,3) NOT NULL CONSTRAINT DF_Payslips_RegularizationDeferred DEFAULT 0;
    END
END

-- Totaux du cycle, tenus à part de TotalIrpp qui garde le sens d'IRPP mensuel.
IF OBJECT_ID(N'dbo.PayrollRuns', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.PayrollRuns', N'TotalIrppRegularization') IS NULL
    BEGIN
        ALTER TABLE dbo.PayrollRuns ADD TotalIrppRegularization decimal(18,3) NOT NULL CONSTRAINT DF_PayrollRuns_TotalIrppRegularization DEFAULT 0;
    END

    IF COL_LENGTH(N'dbo.PayrollRuns', N'TotalCssRegularization') IS NULL
    BEGIN
        ALTER TABLE dbo.PayrollRuns ADD TotalCssRegularization decimal(18,3) NOT NULL CONSTRAINT DF_PayrollRuns_TotalCssRegularization DEFAULT 0;
    END
END

-- Option d'exercice : désactivée par défaut pour les tenants existants.
IF OBJECT_ID(N'dbo.PayrollYearParameters', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.PayrollYearParameters', N'EnableIrppRegularization') IS NULL
    BEGIN
        ALTER TABLE dbo.PayrollYearParameters ADD EnableIrppRegularization bit NOT NULL CONSTRAINT DF_PayrollYearParameters_EnableIrppRegularization DEFAULT 0;
    END
END

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260805150000_AddPayrollIrppRegularization_Tenant')
BEGIN
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20260805150000_AddPayrollIrppRegularization_Tenant', N'8.0.1');
END
