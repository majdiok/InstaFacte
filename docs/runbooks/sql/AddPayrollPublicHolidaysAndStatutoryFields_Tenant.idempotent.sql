-- Idempotent tenant migration: jours fériés paie + champs statutaires PayrollYearParameters
IF OBJECT_ID(N'[dbo].[PayrollPublicHolidays]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PayrollPublicHolidays] (
        [Id] uniqueidentifier NOT NULL,
        [Year] int NOT NULL,
        [Date] datetime2 NOT NULL,
        [Label] nvarchar(200) NOT NULL,
        [Kind] int NOT NULL,
        [IsPaid] bit NOT NULL,
        [IsEstimated] bit NOT NULL,
        [DecreeReference] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PayrollPublicHolidays] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_PayrollPublicHolidays_Year_Date] ON [dbo].[PayrollPublicHolidays]([Year], [Date]);
    CREATE INDEX [IX_PayrollPublicHolidays_Date] ON [dbo].[PayrollPublicHolidays]([Date]);
END;

IF COL_LENGTH('dbo.PayrollYearParameters', 'CnssMonthlyCeiling') IS NULL
    ALTER TABLE [dbo].[PayrollYearParameters] ADD [CnssMonthlyCeiling] decimal(18,3) NULL;
IF COL_LENGTH('dbo.PayrollYearParameters', 'CnssDailyCeiling') IS NULL
    ALTER TABLE [dbo].[PayrollYearParameters] ADD [CnssDailyCeiling] decimal(18,3) NULL;
IF COL_LENGTH('dbo.PayrollYearParameters', 'CssMonthlyCeiling') IS NULL
    ALTER TABLE [dbo].[PayrollYearParameters] ADD [CssMonthlyCeiling] decimal(18,3) NULL;
IF COL_LENGTH('dbo.PayrollYearParameters', 'AccidentWorkMonthlyCeiling') IS NULL
    ALTER TABLE [dbo].[PayrollYearParameters] ADD [AccidentWorkMonthlyCeiling] decimal(18,3) NULL;
IF COL_LENGTH('dbo.PayrollYearParameters', 'SickLeaveWaitingDays') IS NULL
    ALTER TABLE [dbo].[PayrollYearParameters] ADD [SickLeaveWaitingDays] int NOT NULL CONSTRAINT [DF_PayrollYearParameters_SickLeaveWaitingDays] DEFAULT 5;
IF COL_LENGTH('dbo.PayrollYearParameters', 'SickLeaveIjRatePercent') IS NULL
    ALTER TABLE [dbo].[PayrollYearParameters] ADD [SickLeaveIjRatePercent] decimal(8,4) NOT NULL CONSTRAINT [DF_PayrollYearParameters_SickLeaveIjRatePercent] DEFAULT 66.67;
IF COL_LENGTH('dbo.PayrollYearParameters', 'MaternityLeaveDurationDays') IS NULL
    ALTER TABLE [dbo].[PayrollYearParameters] ADD [MaternityLeaveDurationDays] int NOT NULL CONSTRAINT [DF_PayrollYearParameters_MaternityLeaveDurationDays] DEFAULT 60;
IF COL_LENGTH('dbo.PayrollYearParameters', 'PaternityLeaveDurationDays') IS NULL
    ALTER TABLE [dbo].[PayrollYearParameters] ADD [PaternityLeaveDurationDays] int NOT NULL CONSTRAINT [DF_PayrollYearParameters_PaternityLeaveDurationDays] DEFAULT 2;
IF COL_LENGTH('dbo.PayrollYearParameters', 'MaternityEmployerTopUpDefault') IS NULL
    ALTER TABLE [dbo].[PayrollYearParameters] ADD [MaternityEmployerTopUpDefault] decimal(8,4) NOT NULL CONSTRAINT [DF_PayrollYearParameters_MaternityEmployerTopUpDefault] DEFAULT 100;
