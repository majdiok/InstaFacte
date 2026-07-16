BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    CREATE TABLE [EmployeeAdvances] (
        [Id] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [Date] datetime2 NOT NULL,
        [Amount] decimal(18,3) NOT NULL,
        [Reason] nvarchar(500) NULL,
        [IsSettled] bit NOT NULL,
        [SettledInPayrollRunId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_EmployeeAdvances] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    CREATE TABLE [Employees] (
        [Id] uniqueidentifier NOT NULL,
        [EmployeeNumber] nvarchar(50) NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [Cin] nvarchar(20) NULL,
        [CnssNumber] nvarchar(30) NULL,
        [DateOfBirth] datetime2 NULL,
        [HireDate] datetime2 NOT NULL,
        [TerminationDate] datetime2 NULL,
        [MaritalStatus] int NOT NULL,
        [IsHeadOfFamily] bit NOT NULL,
        [DependentChildren] int NOT NULL,
        [AddressStreet] nvarchar(200) NULL,
        [AddressStreetLine2] nvarchar(200) NULL,
        [AddressCity] nvarchar(100) NULL,
        [AddressPostalCode] nvarchar(10) NULL,
        [AddressGovernorate] nvarchar(100) NULL,
        [AddressCountry] nvarchar(100) NULL,
        [Email] nvarchar(256) NULL,
        [Phone] nvarchar(20) NULL,
        [PhoneCountryCode] nvarchar(5) NULL,
        [PhoneLocalNumber] nvarchar(15) NULL,
        [Rib] nvarchar(40) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_Employees] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    CREATE TABLE [LeaveRequests] (
        [Id] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [Type] int NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [Days] decimal(6,2) NOT NULL,
        [Reason] nvarchar(500) NULL,
        [IsApproved] bit NOT NULL,
        [ApprovedAt] datetime2 NULL,
        [ApprovedBy] nvarchar(450) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_LeaveRequests] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    CREATE TABLE [PayrollRuns] (
        [Id] uniqueidentifier NOT NULL,
        [Year] int NOT NULL,
        [Month] int NOT NULL,
        [Label] nvarchar(150) NOT NULL,
        [Status] int NOT NULL,
        [ParametersFiscalYear] int NOT NULL,
        [CalculatedAt] datetime2 NULL,
        [ValidatedAt] datetime2 NULL,
        [ValidatedBy] nvarchar(450) NULL,
        [ClosedAt] datetime2 NULL,
        [TotalGross] decimal(18,3) NOT NULL,
        [TotalCnssEmployee] decimal(18,3) NOT NULL,
        [TotalIrpp] decimal(18,3) NOT NULL,
        [TotalCss] decimal(18,3) NOT NULL,
        [TotalNet] decimal(18,3) NOT NULL,
        [TotalCnssEmployer] decimal(18,3) NOT NULL,
        [TotalTfp] decimal(18,3) NOT NULL,
        [TotalFoprolos] decimal(18,3) NOT NULL,
        [TotalWorkAccident] decimal(18,3) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_PayrollRuns] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    CREATE TABLE [PayrollYearParameters] (
        [Id] uniqueidentifier NOT NULL,
        [FiscalYear] int NOT NULL,
        [CnssEmployeeRate] decimal(8,4) NOT NULL,
        [CnssEmployerRate] decimal(8,4) NOT NULL,
        [CssRate] decimal(8,4) NOT NULL,
        [CssAnnualExemptionThreshold] decimal(18,3) NOT NULL,
        [ProfessionalExpensesRate] decimal(8,4) NOT NULL,
        [ProfessionalExpensesAnnualCap] decimal(18,3) NOT NULL,
        [HeadOfFamilyAnnualDeduction] decimal(18,3) NOT NULL,
        [ChildAnnualDeduction] decimal(18,3) NOT NULL,
        [MaxDeductibleChildren] int NOT NULL,
        [TfpRateIndustry] decimal(8,4) NOT NULL,
        [TfpRateOther] decimal(8,4) NOT NULL,
        [FoprolosRate] decimal(8,4) NOT NULL,
        [MonthlySmig] decimal(18,3) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_PayrollYearParameters] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    CREATE TABLE [EmploymentContracts] (
        [Id] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [Type] int NOT NULL,
        [Regime] int NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NULL,
        [BaseSalary] decimal(18,3) NOT NULL,
        [WorkAccidentRate] decimal(8,4) NOT NULL,
        [JobTitle] nvarchar(150) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_EmploymentContracts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmploymentContracts_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    CREATE TABLE [Payslips] (
        [Id] uniqueidentifier NOT NULL,
        [PayrollRunId] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [EmployeeName] nvarchar(200) NOT NULL,
        [EmployeeNumber] nvarchar(50) NOT NULL,
        [CnssNumber] nvarchar(30) NULL,
        [Year] int NOT NULL,
        [Month] int NOT NULL,
        [GrossSalary] decimal(18,3) NOT NULL,
        [CnssableGross] decimal(18,3) NOT NULL,
        [CnssEmployee] decimal(18,3) NOT NULL,
        [TaxableBaseAfterCnss] decimal(18,3) NOT NULL,
        [ProfessionalExpenses] decimal(18,3) NOT NULL,
        [FamilyDeductions] decimal(18,3) NOT NULL,
        [MonthlyNetTaxable] decimal(18,3) NOT NULL,
        [AnnualNetTaxable] decimal(18,3) NOT NULL,
        [Irpp] decimal(18,3) NOT NULL,
        [Css] decimal(18,3) NOT NULL,
        [OtherDeductions] decimal(18,3) NOT NULL,
        [NonTaxableAllowances] decimal(18,3) NOT NULL,
        [NetSalary] decimal(18,3) NOT NULL,
        [CnssEmployer] decimal(18,3) NOT NULL,
        [WorkAccidentContribution] decimal(18,3) NOT NULL,
        [Tfp] decimal(18,3) NOT NULL,
        [Foprolos] decimal(18,3) NOT NULL,
        [AppliedCnssEmployeeRate] decimal(8,4) NOT NULL,
        [AppliedCnssEmployerRate] decimal(8,4) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_Payslips] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payslips_PayrollRuns_PayrollRunId] FOREIGN KEY ([PayrollRunId]) REFERENCES [PayrollRuns] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    CREATE TABLE [PayrollIrppBrackets] (
        [Id] uniqueidentifier NOT NULL,
        [PayrollYearParametersId] uniqueidentifier NOT NULL,
        [LowerBound] decimal(18,3) NOT NULL,
        [Rate] decimal(8,4) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PayrollIrppBrackets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PayrollIrppBrackets_PayrollYearParameters_PayrollYearParametersId] FOREIGN KEY ([PayrollYearParametersId]) REFERENCES [PayrollYearParameters] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    CREATE TABLE [ContractAllowances] (
        [Id] uniqueidentifier NOT NULL,
        [ContractId] uniqueidentifier NOT NULL,
        [Label] nvarchar(150) NOT NULL,
        [Amount] decimal(18,3) NOT NULL,
        [Taxable] bit NOT NULL,
        [SubjectToCnss] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ContractAllowances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ContractAllowances_EmploymentContracts_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [EmploymentContracts] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    CREATE TABLE [PayslipLines] (
        [Id] uniqueidentifier NOT NULL,
        [PayslipId] uniqueidentifier NOT NULL,
        [Order] int NOT NULL,
        [Label] nvarchar(200) NOT NULL,
        [Kind] int NOT NULL,
        [Base] decimal(18,3) NULL,
        [Rate] decimal(8,4) NULL,
        [Amount] decimal(18,3) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PayslipLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PayslipLines_Payslips_PayslipId] FOREIGN KEY ([PayslipId]) REFERENCES [Payslips] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_ContractAllowances_ContractId] ON [ContractAllowances] ([ContractId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_EmployeeAdvances_EmployeeId_IsSettled] ON [EmployeeAdvances] ([EmployeeId], [IsSettled]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_Employees_CnssNumber] ON [Employees] ([CnssNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Employees_EmployeeNumber] ON [Employees] ([EmployeeNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_Employees_IsActive] ON [Employees] ([IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_EmploymentContracts_EmployeeId] ON [EmploymentContracts] ([EmployeeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_LeaveRequests_EmployeeId] ON [LeaveRequests] ([EmployeeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_LeaveRequests_StartDate_EndDate] ON [LeaveRequests] ([StartDate], [EndDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_PayrollIrppBrackets_PayrollYearParametersId] ON [PayrollIrppBrackets] ([PayrollYearParametersId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_PayrollRuns_Status] ON [PayrollRuns] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PayrollRuns_Year_Month] ON [PayrollRuns] ([Year], [Month]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PayrollYearParameters_FiscalYear] ON [PayrollYearParameters] ([FiscalYear]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_PayslipLines_PayslipId] ON [PayslipLines] ([PayslipId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_Payslips_EmployeeId_Year_Month] ON [Payslips] ([EmployeeId], [Year], [Month]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_Payslips_PayrollRunId] ON [Payslips] ([PayrollRunId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714145906_AddPayrollModule_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260714145906_AddPayrollModule_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

