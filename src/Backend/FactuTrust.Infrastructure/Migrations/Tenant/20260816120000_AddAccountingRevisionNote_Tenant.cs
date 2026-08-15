using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

/// <summary>
/// Réviseur IA — socle. Migration strictement ADDITIVE :
/// <list type="bullet">
///   <item><c>AccountingControlRuns.EvaluatedRuleCount</c> : dénominateur du taux de conformité.
///         Défaut 0 sur les runs existants, ce qui laisse leur taux historique inchangé.</item>
///   <item><c>AccountingRevisionNotes</c> : le dossier de révision rédigé d'un run.</item>
/// </list>
/// Aucune table ni colonne existante n'est modifiée ou supprimée.
/// </summary>
[DbContext(typeof(TenantDbContext))]
[Migration("20260816120000_AddAccountingRevisionNote_Tenant")]
public partial class AddAccountingRevisionNote_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[AccountingControlRuns]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[dbo].[AccountingControlRuns]', N'EvaluatedRuleCount') IS NULL
BEGIN
    ALTER TABLE [AccountingControlRuns]
        ADD [EvaluatedRuleCount] int NOT NULL CONSTRAINT [DF_AccountingControlRuns_EvaluatedRuleCount] DEFAULT 0;
END

IF OBJECT_ID(N'[dbo].[AccountingRevisionNotes]', N'U') IS NULL
BEGIN
    CREATE TABLE [AccountingRevisionNotes] (
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
            FOREIGN KEY ([RunId]) REFERENCES [AccountingControlRuns] ([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_AccountingRevisionNotes_RunId] ON [AccountingRevisionNotes] ([RunId]);
    CREATE INDEX [IX_AccountingRevisionNotes_FiscalYear_GeneratedAt] ON [AccountingRevisionNotes] ([FiscalYear], [GeneratedAt]);
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[AccountingRevisionNotes]', N'U') IS NOT NULL DROP TABLE [AccountingRevisionNotes];

IF OBJECT_ID(N'[dbo].[AccountingControlRuns]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[dbo].[AccountingControlRuns]', N'EvaluatedRuleCount') IS NOT NULL
BEGIN
    IF OBJECT_ID(N'[dbo].[DF_AccountingControlRuns_EvaluatedRuleCount]', N'D') IS NOT NULL
        ALTER TABLE [AccountingControlRuns] DROP CONSTRAINT [DF_AccountingControlRuns_EvaluatedRuleCount];
    ALTER TABLE [AccountingControlRuns] DROP COLUMN [EvaluatedRuleCount];
END
""");
    }
}
