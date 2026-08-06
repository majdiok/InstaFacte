-- Idempotent: déclarations nominatives parents à charge (non-cumul art. 40 IRPP)

IF OBJECT_ID(N'dbo.EmployeeDependentParents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeeDependentParents
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EmployeeDependentParents PRIMARY KEY,
        EmployeeId UNIQUEIDENTIFIER NOT NULL,
        ParentCin NVARCHAR(20) NOT NULL,
        Kinship INT NOT NULL,
        FirstName NVARCHAR(100) NULL,
        LastName NVARCHAR(100) NULL,
        StartDate DATETIME2 NOT NULL,
        EndDate DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NULL,
        CreatedBy NVARCHAR(MAX) NULL,
        UpdatedBy NVARCHAR(MAX) NULL,
        Version INT NOT NULL,
        CONSTRAINT FK_EmployeeDependentParents_Employees_EmployeeId
            FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees (Id) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_EmployeeDependentParents_EmployeeId'
      AND object_id = OBJECT_ID(N'dbo.EmployeeDependentParents')
)
BEGIN
    CREATE INDEX IX_EmployeeDependentParents_EmployeeId
        ON dbo.EmployeeDependentParents (EmployeeId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_EmployeeDependentParents_ParentCin_Active'
      AND object_id = OBJECT_ID(N'dbo.EmployeeDependentParents')
)
BEGIN
    CREATE UNIQUE INDEX IX_EmployeeDependentParents_ParentCin_Active
        ON dbo.EmployeeDependentParents (ParentCin)
        WHERE EndDate IS NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807010000_AddEmployeeDependentParents_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260807010000_AddEmployeeDependentParents_Tenant', N'8.0.0');
END
GO
