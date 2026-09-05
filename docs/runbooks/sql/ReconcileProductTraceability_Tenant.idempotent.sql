-- Reconcile product traceability fields (idempotent).
--
-- Normalizes inconsistent TrackingMode / PickingPolicy / CostingMethod combinations
-- stored before ProductTraceabilityRules enforcement.
--
-- Enum values:
--   TrackingMode: 0=None, 1=Lot, 2=Serial
--   PickingPolicy: 0=None, 1=Fefo, 2=FifoPhysical, 3=Manual
--   CostingMethod: 0=Average (CMUP), 1=Fifo, 2=Lifo
--
-- Run on EACH tenant database. Review the SELECT reports before COMMIT in production.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;
GO

-- Report: rows that will be changed
SELECT
    p.[Id],
    p.[Code],
    p.[Name],
    p.[IsStockManaged],
    p.[TrackingMode],
    p.[PickingPolicy],
    p.[HasExpiryTracking],
    p.[CostingMethod],
    p.[ExpiryAlertDays]
FROM [dbo].[Products] p
WHERE
    p.[PickingPolicy] = 3
    OR (p.[TrackingMode] = 0 AND (p.[PickingPolicy] <> 0 OR p.[HasExpiryTracking] = 1 OR p.[ExpiryAlertDays] IS NOT NULL))
    OR (p.[TrackingMode] = 2 AND p.[PickingPolicy] <> 0)
    OR (p.[PickingPolicy] = 1 AND p.[HasExpiryTracking] = 0)
    OR (p.[IsStockManaged] = 0 AND (
        p.[TrackingMode] <> 0 OR p.[PickingPolicy] <> 0 OR p.[HasExpiryTracking] = 1
        OR p.[CostingMethod] <> 0 OR p.[ExpiryAlertDays] IS NOT NULL));
GO

-- Manual (3) -> None (0)
UPDATE [dbo].[Products]
SET [PickingPolicy] = 0
WHERE [PickingPolicy] = 3;
GO

-- No tracking: reset picking / expiry
UPDATE [dbo].[Products]
SET
    [PickingPolicy] = 0,
    [HasExpiryTracking] = 0,
    [ExpiryAlertDays] = NULL
WHERE [TrackingMode] = 0
  AND ([PickingPolicy] <> 0 OR [HasExpiryTracking] = 1 OR [ExpiryAlertDays] IS NOT NULL);
GO

-- Serial: no picking policy
UPDATE [dbo].[Products]
SET [PickingPolicy] = 0
WHERE [TrackingMode] = 2 AND [PickingPolicy] <> 0;
GO

-- FEFO without expiry -> FIFO physical
UPDATE [dbo].[Products]
SET [PickingPolicy] = 2
WHERE [PickingPolicy] = 1 AND [HasExpiryTracking] = 0;
GO

-- Stock not managed: reset traceability to defaults
UPDATE [dbo].[Products]
SET
    [TrackingMode] = 0,
    [PickingPolicy] = 0,
    [HasExpiryTracking] = 0,
    [CostingMethod] = 0,
    [ExpiryAlertDays] = NULL
WHERE [IsStockManaged] = 0
  AND (
    [TrackingMode] <> 0 OR [PickingPolicy] <> 0 OR [HasExpiryTracking] = 1
    OR [CostingMethod] <> 0 OR [ExpiryAlertDays] IS NOT NULL);
GO

COMMIT TRANSACTION;
GO
