-- Idempotent master: portal bind on Identity users.
IF COL_LENGTH(N'dbo.Users', N'PortalClientId') IS NULL
    ALTER TABLE [Users] ADD [PortalClientId] uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.Users', N'PortalInviteTokenHash') IS NULL
    ALTER TABLE [Users] ADD [PortalInviteTokenHash] nvarchar(64) NULL;
IF COL_LENGTH(N'dbo.Users', N'PortalInviteExpiresAt') IS NULL
    ALTER TABLE [Users] ADD [PortalInviteExpiresAt] datetime2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_TenantId_PortalClientId' AND object_id = OBJECT_ID(N'dbo.Users'))
    CREATE INDEX [IX_Users_TenantId_PortalClientId] ON [Users] ([TenantId], [PortalClientId]);
