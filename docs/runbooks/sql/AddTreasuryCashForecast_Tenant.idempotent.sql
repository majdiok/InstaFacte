-- Idempotent tenant migration: AddTreasuryCashForecast_Tenant
-- Module « Trésorerie prévisionnelle par IA ».
-- Crée 7 tables et leurs index. Strictement additif : aucune table ni colonne existante n'est
-- touchée. Rejouable sans effet de bord.
--
-- Ordre imposé : CashFlowForecastRuns d'abord (les 4 tables enfants portent une FK vers elle).

IF OBJECT_ID(N'dbo.CashFlowForecastRuns', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CashFlowForecastRuns
    (
        Id                   uniqueidentifier NOT NULL,
        PeriodStart          datetime2        NOT NULL,
        PeriodEnd            datetime2        NOT NULL,
        HorizonMonths        int              NOT NULL,
        OpeningBalance       decimal(18,3)    NOT NULL,
        TotalInflows         decimal(18,3)    NOT NULL,
        TotalOutflows        decimal(18,3)    NOT NULL,
        NetFlow              decimal(18,3)    NOT NULL,
        ClosingBalance       decimal(18,3)    NOT NULL,
        Currency             nvarchar(3)      NOT NULL,
        ConfidencePercent    decimal(5,2)     NOT NULL,
        MethodUsed           int              NOT NULL,
        Status               int              NOT NULL,
        ComputedAt           datetime2        NOT NULL,
        ComputedByUserId     uniqueidentifier NULL,
        DurationMs           int              NOT NULL,
        InputsJson           nvarchar(max)    NULL,
        ErrorMessage         nvarchar(2000)   NULL,
        AiAdjustmentApplied  bit              NOT NULL,
        AiModelRef           nvarchar(200)    NULL,
        AiAnalyzedAt         datetime2        NULL,
        RowVersion           rowversion       NOT NULL,
        CreatedAt            datetime2        NOT NULL,
        UpdatedAt            datetime2        NULL,
        CreatedBy            nvarchar(max)    NULL,
        UpdatedBy            nvarchar(max)    NULL,
        CONSTRAINT PK_CashFlowForecastRuns PRIMARY KEY (Id)
    );
END

IF OBJECT_ID(N'dbo.CashFlowForecastLines', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CashFlowForecastLines
    (
        Id                 uniqueidentifier NOT NULL,
        ForecastRunId      uniqueidentifier NOT NULL,
        Direction          int              NOT NULL,
        SourceType         int              NOT NULL,
        SourceId           uniqueidentifier NULL,
        SourceReference    nvarchar(100)    NULL,
        Label              nvarchar(300)    NOT NULL,
        ThirdPartyName     nvarchar(200)    NULL,
        ContractualDate    datetime2        NOT NULL,
        ExpectedDate       datetime2        NOT NULL,
        Amount             decimal(18,3)    NOT NULL,
        ProbabilityPercent decimal(5,2)     NOT NULL,
        WeightedAmount     decimal(18,3)    NOT NULL,
        IsConfirmed        bit              NOT NULL,
        CreatedAt          datetime2        NOT NULL,
        UpdatedAt          datetime2        NULL,
        CreatedBy          nvarchar(max)    NULL,
        UpdatedBy          nvarchar(max)    NULL,
        CONSTRAINT PK_CashFlowForecastLines PRIMARY KEY (Id),
        CONSTRAINT FK_CashFlowForecastLines_CashFlowForecastRuns_ForecastRunId
            FOREIGN KEY (ForecastRunId) REFERENCES dbo.CashFlowForecastRuns (Id) ON DELETE CASCADE
    );
END

IF OBJECT_ID(N'dbo.CashFlowForecastBuckets', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CashFlowForecastBuckets
    (
        Id                 uniqueidentifier NOT NULL,
        ForecastRunId      uniqueidentifier NOT NULL,
        PeriodStart        datetime2        NOT NULL,
        PeriodEnd          datetime2        NOT NULL,
        SequenceIndex      int              NOT NULL,
        OpeningBalance     decimal(18,3)    NOT NULL,
        Inflows            decimal(18,3)    NOT NULL,
        Outflows           decimal(18,3)    NOT NULL,
        NetFlow            decimal(18,3)    NOT NULL,
        ClosingBalance     decimal(18,3)    NOT NULL,
        LowClosingBalance  decimal(18,3)    NOT NULL,
        HighClosingBalance decimal(18,3)    NOT NULL,
        CreatedAt          datetime2        NOT NULL,
        UpdatedAt          datetime2        NULL,
        CreatedBy          nvarchar(max)    NULL,
        UpdatedBy          nvarchar(max)    NULL,
        CONSTRAINT PK_CashFlowForecastBuckets PRIMARY KEY (Id),
        CONSTRAINT FK_CashFlowForecastBuckets_CashFlowForecastRuns_ForecastRunId
            FOREIGN KEY (ForecastRunId) REFERENCES dbo.CashFlowForecastRuns (Id) ON DELETE CASCADE
    );
END

IF OBJECT_ID(N'dbo.CashFlowScenarios', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CashFlowScenarios
    (
        Id                              uniqueidentifier NOT NULL,
        ForecastRunId                   uniqueidentifier NOT NULL,
        Kind                            int              NOT NULL,
        ClosingBalance                  decimal(18,3)    NOT NULL,
        NetFlow                         decimal(18,3)    NOT NULL,
        DeterministicProbabilityPercent decimal(5,2)     NOT NULL,
        ProbabilityPercent              decimal(5,2)     NOT NULL,
        ProbabilitySource               int              NOT NULL,
        AiRationale                     nvarchar(1000)   NULL,
        AssumptionsJson                 nvarchar(4000)   NULL,
        CreatedAt                       datetime2        NOT NULL,
        UpdatedAt                       datetime2        NULL,
        CreatedBy                       nvarchar(max)    NULL,
        UpdatedBy                       nvarchar(max)    NULL,
        CONSTRAINT PK_CashFlowScenarios PRIMARY KEY (Id),
        CONSTRAINT FK_CashFlowScenarios_CashFlowForecastRuns_ForecastRunId
            FOREIGN KEY (ForecastRunId) REFERENCES dbo.CashFlowForecastRuns (Id) ON DELETE CASCADE
    );
END

IF OBJECT_ID(N'dbo.CashFlowForecastInsights', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CashFlowForecastInsights
    (
        Id               uniqueidentifier NOT NULL,
        ForecastRunId    uniqueidentifier NOT NULL,
        Kind             int              NOT NULL,
        Severity         int              NOT NULL,
        Origin           int              NOT NULL,
        Impact           int              NULL,
        ImpactDirection  int              NULL,
        Title            nvarchar(200)    NOT NULL,
        Detail           nvarchar(1000)   NULL,
        PeriodStart      datetime2        NULL,
        EstimatedBalance decimal(18,3)    NULL,
        SortOrder        int              NOT NULL,
        CreatedAt        datetime2        NOT NULL,
        UpdatedAt        datetime2        NULL,
        CreatedBy        nvarchar(max)    NULL,
        UpdatedBy        nvarchar(max)    NULL,
        CONSTRAINT PK_CashFlowForecastInsights PRIMARY KEY (Id),
        CONSTRAINT FK_CashFlowForecastInsights_CashFlowForecastRuns_ForecastRunId
            FOREIGN KEY (ForecastRunId) REFERENCES dbo.CashFlowForecastRuns (Id) ON DELETE CASCADE
    );
END

IF OBJECT_ID(N'dbo.RecurringCashCommitments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecurringCashCommitments
    (
        Id          uniqueidentifier NOT NULL,
        Label       nvarchar(200)    NOT NULL,
        Direction   int              NOT NULL,
        Amount      decimal(18,3)    NOT NULL,
        Currency    nvarchar(3)      NOT NULL,
        Frequency   int              NOT NULL,
        DayOfMonth  int              NOT NULL,
        StartDate   datetime2        NOT NULL,
        EndDate     datetime2        NULL,
        Category    nvarchar(100)    NULL,
        Notes       nvarchar(500)    NULL,
        IsActive    bit              NOT NULL,
        CreatedAt   datetime2        NOT NULL,
        UpdatedAt   datetime2        NULL,
        CreatedBy   nvarchar(max)    NULL,
        UpdatedBy   nvarchar(max)    NULL,
        CONSTRAINT PK_RecurringCashCommitments PRIMARY KEY (Id)
    );
END

IF OBJECT_ID(N'dbo.CashFlowForecastSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CashFlowForecastSettings
    (
        Id                       uniqueidentifier NOT NULL,
        CriticalThreshold        decimal(18,3)    NOT NULL,
        AlertThreshold           decimal(18,3)    NOT NULL,
        ComfortThreshold         decimal(18,3)    NOT NULL,
        PayrollPaymentDayOfMonth int              NULL,
        CreatedAt                datetime2        NOT NULL,
        UpdatedAt                datetime2        NULL,
        CreatedBy                nvarchar(max)    NULL,
        UpdatedBy                nvarchar(max)    NULL,
        CONSTRAINT PK_CashFlowForecastSettings PRIMARY KEY (Id)
    );
END

-- Index

IF OBJECT_ID(N'dbo.CashFlowForecastRuns', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CashFlowForecastRuns_StatusComputed' AND object_id = OBJECT_ID(N'dbo.CashFlowForecastRuns'))
BEGIN
    CREATE INDEX IX_CashFlowForecastRuns_StatusComputed ON dbo.CashFlowForecastRuns (Status, ComputedAt);
END

IF OBJECT_ID(N'dbo.CashFlowForecastRuns', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CashFlowForecastRuns_ComputedAt' AND object_id = OBJECT_ID(N'dbo.CashFlowForecastRuns'))
BEGIN
    CREATE INDEX IX_CashFlowForecastRuns_ComputedAt ON dbo.CashFlowForecastRuns (ComputedAt);
END

IF OBJECT_ID(N'dbo.CashFlowForecastLines', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CashFlowForecastLines_RunExpectedDate' AND object_id = OBJECT_ID(N'dbo.CashFlowForecastLines'))
BEGIN
    CREATE INDEX IX_CashFlowForecastLines_RunExpectedDate ON dbo.CashFlowForecastLines (ForecastRunId, ExpectedDate);
END

IF OBJECT_ID(N'dbo.CashFlowForecastLines', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CashFlowForecastLines_RunDirection' AND object_id = OBJECT_ID(N'dbo.CashFlowForecastLines'))
BEGIN
    CREATE INDEX IX_CashFlowForecastLines_RunDirection ON dbo.CashFlowForecastLines (ForecastRunId, Direction);
END

IF OBJECT_ID(N'dbo.CashFlowForecastLines', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CashFlowForecastLines_Source' AND object_id = OBJECT_ID(N'dbo.CashFlowForecastLines'))
BEGIN
    CREATE INDEX IX_CashFlowForecastLines_Source ON dbo.CashFlowForecastLines (SourceType, SourceId);
END

IF OBJECT_ID(N'dbo.CashFlowForecastBuckets', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CashFlowForecastBuckets_RunSequence' AND object_id = OBJECT_ID(N'dbo.CashFlowForecastBuckets'))
BEGIN
    CREATE UNIQUE INDEX IX_CashFlowForecastBuckets_RunSequence ON dbo.CashFlowForecastBuckets (ForecastRunId, SequenceIndex);
END

IF OBJECT_ID(N'dbo.CashFlowScenarios', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CashFlowScenarios_RunKind' AND object_id = OBJECT_ID(N'dbo.CashFlowScenarios'))
BEGIN
    CREATE UNIQUE INDEX IX_CashFlowScenarios_RunKind ON dbo.CashFlowScenarios (ForecastRunId, Kind);
END

IF OBJECT_ID(N'dbo.CashFlowForecastInsights', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CashFlowForecastInsights_RunKindSort' AND object_id = OBJECT_ID(N'dbo.CashFlowForecastInsights'))
BEGIN
    CREATE INDEX IX_CashFlowForecastInsights_RunKindSort ON dbo.CashFlowForecastInsights (ForecastRunId, Kind, SortOrder);
END

IF OBJECT_ID(N'dbo.RecurringCashCommitments', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RecurringCashCommitments_ActiveStart' AND object_id = OBJECT_ID(N'dbo.RecurringCashCommitments'))
BEGIN
    CREATE INDEX IX_RecurringCashCommitments_ActiveStart ON dbo.RecurringCashCommitments (IsActive, StartDate);
END

-- Marque la migration comme appliquée pour que TenantMigrationGuard ne rejoue pas le C#.

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260813190000_AddTreasuryCashForecast_Tenant')
BEGIN
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20260813190000_AddTreasuryCashForecast_Tenant', N'8.0.1');
END
