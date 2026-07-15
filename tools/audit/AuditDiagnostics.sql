-- Diagnostic queries for FactuTrust audit hash chain (SQL Server, table AuditLogs).
-- Run against a tenant database snapshot (read-only recommended).

-- 1) Distinct PreviousHash values that appear on more than one row (excluding GENESIS) — suggests concurrent LogAsync before the fix.
SELECT PreviousHash, COUNT(*) AS Cnt
FROM AuditLogs
WHERE PreviousHash <> N'GENESIS'
GROUP BY PreviousHash
HAVING COUNT(*) > 1
ORDER BY Cnt DESC;

-- 2) Full row count
SELECT COUNT(*) AS TotalAuditRows FROM AuditLogs;

-- 3) Inspect one entry by Id (replace @Id)
/*
DECLARE @Id UNIQUEIDENTIFIER = 'dcaa19c1-3d98-46dd-b222-55c7b4c1eb66';
SELECT Id, CreatedAt, PreviousHash, Hash, [Action], EntityType, EntityId
FROM AuditLogs
WHERE Id = @Id;
*/
