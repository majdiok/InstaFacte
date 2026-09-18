-- Studio IA 4.7 « v1.1 » — D5 (historique des enregistrements).
-- Jumeau idempotent de la migration tenant 20260918100000_AddAuditLogsEntityHistoryIndex_Tenant.
-- Couvre : WHERE EntityType = … AND EntityId = … ORDER BY CreatedAt DESC
-- (GET api/studio/records/{entityKey}/{id}/history). Additif pur, rejouable.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_EntityHistory' AND object_id = OBJECT_ID(N'[dbo].[AuditLogs]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditLogs_EntityHistory]
        ON [dbo].[AuditLogs] ([EntityType], [EntityId], [CreatedAt]);
END;
GO
