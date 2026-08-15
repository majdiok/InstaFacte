-- =============================================================================================
-- Réviseur IA — socle. Script idempotent, base TENANT (dossier client).
-- Équivalent SQL de la migration 20260816120000_AddAccountingRevisionNote_Tenant.
--
-- Strictement additif : ajoute une colonne à AccountingControlRuns et crée AccountingRevisionNotes.
-- Aucune donnée existante n'est modifiée. Rejouable sans effet.
--
-- Prérequis : la migration 20260807180000_AddAccountingAuditModule_Tenant doit être appliquée
-- (table AccountingControlRuns présente). Sinon le script ne fait rien et le signale.
-- =============================================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'[dbo].[AccountingControlRuns]', N'U') IS NULL
BEGIN
    RAISERROR(N'AccountingControlRuns absente : appliquer d''abord AddAccountingAuditModule_Tenant.', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;

-- 1. Dénominateur du taux de conformité -------------------------------------------------------
IF COL_LENGTH(N'[dbo].[AccountingControlRuns]', N'EvaluatedRuleCount') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccountingControlRuns]
        ADD [EvaluatedRuleCount] int NOT NULL
        CONSTRAINT [DF_AccountingControlRuns_EvaluatedRuleCount] DEFAULT 0;
    PRINT N'+ AccountingControlRuns.EvaluatedRuleCount ajoutée (défaut 0).';
END
ELSE
    PRINT N'= AccountingControlRuns.EvaluatedRuleCount déjà présente.';

-- 2. Dossier de révision ----------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[AccountingRevisionNotes]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AccountingRevisionNotes] (
        [Id] uniqueidentifier NOT NULL,
        [RunId] uniqueidentifier NOT NULL,
        [FiscalYear] int NOT NULL,
        [PeriodFrom] date NOT NULL,
        [PeriodTo] date NOT NULL,
        [GeneratedAt] datetime2 NOT NULL,
        [GeneratedByUserId] uniqueidentifier NULL,
        [GeneratedByUserName] nvarchar(256) NULL,
        [AiGenerated] bit NOT NULL,
        [ModelRef] nvarchar(200) NULL,
        [FallbackReason] nvarchar(1000) NULL,
        [ExecutiveSummary] nvarchar(4000) NULL,
        [ItemsJson] nvarchar(max) NOT NULL,
        [TotalImpactAmount] decimal(18,3) NOT NULL,
        [AnomalyCount] int NOT NULL,
        [BlockingCount] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AccountingRevisionNotes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AccountingRevisionNotes_AccountingControlRuns_RunId]
            FOREIGN KEY ([RunId]) REFERENCES [dbo].[AccountingControlRuns] ([Id]) ON DELETE CASCADE
    );
    PRINT N'+ Table AccountingRevisionNotes créée.';
END
ELSE
    PRINT N'= Table AccountingRevisionNotes déjà présente.';

IF OBJECT_ID(N'[dbo].[AccountingRevisionNotes]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_AccountingRevisionNotes_RunId'
                     AND object_id = OBJECT_ID(N'[dbo].[AccountingRevisionNotes]'))
BEGIN
    CREATE UNIQUE INDEX [IX_AccountingRevisionNotes_RunId]
        ON [dbo].[AccountingRevisionNotes] ([RunId]);
    PRINT N'+ Index unique IX_AccountingRevisionNotes_RunId créé.';
END

IF OBJECT_ID(N'[dbo].[AccountingRevisionNotes]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_AccountingRevisionNotes_FiscalYear_GeneratedAt'
                     AND object_id = OBJECT_ID(N'[dbo].[AccountingRevisionNotes]'))
BEGIN
    CREATE INDEX [IX_AccountingRevisionNotes_FiscalYear_GeneratedAt]
        ON [dbo].[AccountingRevisionNotes] ([FiscalYear], [GeneratedAt]);
    PRINT N'+ Index IX_AccountingRevisionNotes_FiscalYear_GeneratedAt créé.';
END

-- 3. Historique de migration ------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory]
                   WHERE [MigrationId] = N'20260816120000_AddAccountingRevisionNote_Tenant')
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260816120000_AddAccountingRevisionNote_Tenant', N'8.0.0');
    PRINT N'+ Migration enregistrée dans __EFMigrationsHistory.';
END

COMMIT TRANSACTION;

-- Vérification --------------------------------------------------------------------------------
SELECT
    COL_LENGTH(N'[dbo].[AccountingControlRuns]', N'EvaluatedRuleCount') AS EvaluatedRuleCountColumn,
    OBJECT_ID(N'[dbo].[AccountingRevisionNotes]')                       AS RevisionNotesTable;
