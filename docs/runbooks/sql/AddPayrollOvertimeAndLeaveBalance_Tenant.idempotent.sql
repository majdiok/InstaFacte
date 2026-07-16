BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714205249_AddThirdPartyAccountingProfiles_Tenant'
)
BEGIN
    CREATE TABLE [ThirdPartyAccountingProfiles] (
        [Id] uniqueidentifier NOT NULL,
        [Kind] int NOT NULL,
        [ThirdPartyId] uniqueidentifier NOT NULL,
        [AuxiliaryCode] nvarchar(20) NOT NULL,
        [CollectiveAccountNumber] nvarchar(20) NOT NULL,
        [PaymentTermDays] int NULL,
        [AccountingNotes] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ThirdPartyAccountingProfiles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714205249_AddThirdPartyAccountingProfiles_Tenant'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ThirdPartyAccountingProfiles_AuxiliaryCode] ON [ThirdPartyAccountingProfiles] ([AuxiliaryCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714205249_AddThirdPartyAccountingProfiles_Tenant'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ThirdPartyAccountingProfiles_Kind_ThirdPartyId] ON [ThirdPartyAccountingProfiles] ([Kind], [ThirdPartyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714205249_AddThirdPartyAccountingProfiles_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260714205249_AddThirdPartyAccountingProfiles_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715103807_AddPayrollOvertimeAndLeaveBalance_Tenant'
)
BEGIN
    ALTER TABLE [Employees] ADD [LeaveOpeningBalanceDays] decimal(8,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715103807_AddPayrollOvertimeAndLeaveBalance_Tenant'
)
BEGIN
    CREATE TABLE [LeaveBalanceAccruals] (
        [Id] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [Year] int NOT NULL,
        [Month] int NOT NULL,
        [WorkedDays] decimal(6,2) NOT NULL,
        [AccruedDays] decimal(8,3) NOT NULL,
        [PayrollRunId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_LeaveBalanceAccruals] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715103807_AddPayrollOvertimeAndLeaveBalance_Tenant'
)
BEGIN
    CREATE TABLE [PayrollOvertimeLines] (
        [Id] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [Year] int NOT NULL,
        [Month] int NOT NULL,
        [Hours] decimal(8,2) NOT NULL,
        [RatePercent] decimal(8,2) NOT NULL,
        [ComputedAmount] decimal(18,3) NOT NULL,
        [OverrideAmount] decimal(18,3) NULL,
        [IsOverridden] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_PayrollOvertimeLines] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715103807_AddPayrollOvertimeAndLeaveBalance_Tenant'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LeaveBalanceAccruals_EmployeeId_Year_Month] ON [LeaveBalanceAccruals] ([EmployeeId], [Year], [Month]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715103807_AddPayrollOvertimeAndLeaveBalance_Tenant'
)
BEGIN
    CREATE INDEX [IX_LeaveBalanceAccruals_PayrollRunId] ON [LeaveBalanceAccruals] ([PayrollRunId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715103807_AddPayrollOvertimeAndLeaveBalance_Tenant'
)
BEGIN
    CREATE INDEX [IX_PayrollOvertimeLines_EmployeeId_Year_Month] ON [PayrollOvertimeLines] ([EmployeeId], [Year], [Month]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715103807_AddPayrollOvertimeAndLeaveBalance_Tenant'
)
BEGIN
    CREATE INDEX [IX_PayrollOvertimeLines_Year_Month] ON [PayrollOvertimeLines] ([Year], [Month]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715103807_AddPayrollOvertimeAndLeaveBalance_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260715103807_AddPayrollOvertimeAndLeaveBalance_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

