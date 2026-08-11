-- Idempotent : report des congés cabinet vers la paie interne (base MASTER).
-- Correspond à la migration 20260811020000_AddFirmLeavePayrollMirror_Master.
--
-- À exécuter sur la base Master uniquement. Toutes les colonnes ajoutées sont nulles ou fausses
-- par défaut : aucun congé existant n'est réputé reporté, aucun type ne produit d'effet paie tant
-- que le mapping n'est pas posé.

IF COL_LENGTH('FirmLeaveTypes', 'PayrollLeaveType') IS NULL
BEGIN
    ALTER TABLE FirmLeaveTypes ADD PayrollLeaveType int NULL;
END
GO

IF COL_LENGTH('FirmLeaveTypes', 'CountsAsAbsence') IS NULL
BEGIN
    ALTER TABLE FirmLeaveTypes ADD CountsAsAbsence bit NOT NULL CONSTRAINT DF_FirmLeaveTypes_CountsAsAbsence DEFAULT 0;
END
GO

IF COL_LENGTH('FirmLeaveRequests', 'PayrollLeaveRequestId') IS NULL
BEGIN
    ALTER TABLE FirmLeaveRequests ADD PayrollLeaveRequestId uniqueidentifier NULL;
END
GO

IF COL_LENGTH('FirmLeaveRequests', 'PayrollMirrorState') IS NULL
BEGIN
    ALTER TABLE FirmLeaveRequests ADD PayrollMirrorState int NOT NULL CONSTRAINT DF_FirmLeaveRequests_PayrollMirrorState DEFAULT 0;
END
GO

IF COL_LENGTH('FirmLeaveRequests', 'PayrollMirrorMessage') IS NULL
BEGIN
    ALTER TABLE FirmLeaveRequests ADD PayrollMirrorMessage nvarchar(400) NULL;
END
GO

IF COL_LENGTH('FirmLeaveRequests', 'PayrollMirroredAt') IS NULL
BEGIN
    ALTER TABLE FirmLeaveRequests ADD PayrollMirroredAt datetime2 NULL;
END
GO

-- Traduction des types système vers LeaveType (Paid=0, Unpaid=1, Sick=2, Recovery=7, Other=99).
-- Formation et télétravail restent sans effet : l'une est déjà couverte par le taux de
-- productivité de l'exercice, l'autre est du temps travaillé.
-- Ne vise que IsSystem = 1 : un type créé par un cabinet garde un impact indéterminé.
UPDATE FirmLeaveTypes SET PayrollLeaveType = 0,  CountsAsAbsence = 1 WHERE Code = 'PAID'   AND IsSystem = 1 AND PayrollLeaveType IS NULL;
GO
UPDATE FirmLeaveTypes SET PayrollLeaveType = 7,  CountsAsAbsence = 1 WHERE Code = 'RTT'    AND IsSystem = 1 AND PayrollLeaveType IS NULL;
GO
UPDATE FirmLeaveTypes SET PayrollLeaveType = 2,  CountsAsAbsence = 1 WHERE Code = 'SICK'   AND IsSystem = 1 AND PayrollLeaveType IS NULL;
GO
UPDATE FirmLeaveTypes SET PayrollLeaveType = 1,  CountsAsAbsence = 1 WHERE Code = 'UNPAID' AND IsSystem = 1 AND PayrollLeaveType IS NULL;
GO
UPDATE FirmLeaveTypes SET PayrollLeaveType = 99, CountsAsAbsence = 0 WHERE Code = 'OTHER'  AND IsSystem = 1 AND PayrollLeaveType IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FirmLeaveRequests_FirmTenantId_PayrollMirrorState' AND object_id = OBJECT_ID('FirmLeaveRequests'))
BEGIN
    CREATE INDEX IX_FirmLeaveRequests_FirmTenantId_PayrollMirrorState
        ON FirmLeaveRequests (FirmTenantId, PayrollMirrorState);
END
GO

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE MigrationId = '20260811020000_AddFirmLeavePayrollMirror_Master')
BEGIN
    INSERT INTO [__EFMigrationsHistory] (MigrationId, ProductVersion)
    VALUES ('20260811020000_AddFirmLeavePayrollMirror_Master', '8.0.11');
END
GO
