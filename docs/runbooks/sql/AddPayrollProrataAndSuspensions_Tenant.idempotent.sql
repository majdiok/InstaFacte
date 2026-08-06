-- Idempotent tenant migration: prorata automatique + suspensions paie
IF OBJECT_ID(N'[dbo].[EmployeePayrollSuspensions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[EmployeePayrollSuspensions] (
        [Id] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [Type] int NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NULL,
        [IsPaid] bit NOT NULL,
        [Reason] nvarchar(500) NULL,
        [IsApproved] bit NOT NULL,
        [ApprovedAt] datetime2 NULL,
        [ApprovedBy] nvarchar(450) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_EmployeePayrollSuspensions] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_EmployeePayrollSuspensions_EmployeeId] ON [dbo].[EmployeePayrollSuspensions]([EmployeeId]);
    CREATE INDEX [IX_EmployeePayrollSuspensions_StartDate_EndDate] ON [dbo].[EmployeePayrollSuspensions]([StartDate], [EndDate]);
END;

IF COL_LENGTH('dbo.Payslips', 'ProrataWorkedDays') IS NULL
    ALTER TABLE [dbo].[Payslips] ADD [ProrataWorkedDays] decimal(6,2) NOT NULL CONSTRAINT [DF_Payslips_ProrataWorkedDays] DEFAULT 0;
IF COL_LENGTH('dbo.Payslips', 'ProrataNonWorkedDays') IS NULL
    ALTER TABLE [dbo].[Payslips] ADD [ProrataNonWorkedDays] decimal(6,2) NOT NULL CONSTRAINT [DF_Payslips_ProrataNonWorkedDays] DEFAULT 0;
IF COL_LENGTH('dbo.Payslips', 'ProrataDeductionAmount') IS NULL
    ALTER TABLE [dbo].[Payslips] ADD [ProrataDeductionAmount] decimal(18,3) NOT NULL CONSTRAINT [DF_Payslips_ProrataDeductionAmount] DEFAULT 0;

IF COL_LENGTH('dbo.PayrollYearParameters', 'EnableAutomaticProrata') IS NULL
    ALTER TABLE [dbo].[PayrollYearParameters] ADD [EnableAutomaticProrata] bit NOT NULL CONSTRAINT [DF_PayrollYearParameters_EnableAutomaticProrata] DEFAULT 0;
