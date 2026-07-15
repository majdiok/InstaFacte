using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddCashOperationOriginTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID(N'[ChannelIdentityLinks]', N'U') IS NULL
BEGIN
    CREATE TABLE [ChannelIdentityLinks](
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ChannelType] int NOT NULL,
        [ExternalUserId] nvarchar(128) NOT NULL,
        [ExternalChatId] nvarchar(128) NOT NULL,
        [VerifiedAt] datetime2 NOT NULL,
        [LastSeenAt] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ChannelIdentityLinks] PRIMARY KEY ([Id])
    );
END
""");

            migrationBuilder.Sql("""
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ChannelIdentityLinks_ChannelType_ExternalUserId' AND [object_id] = OBJECT_ID(N'[ChannelIdentityLinks]'))
BEGIN
    CREATE UNIQUE INDEX [IX_ChannelIdentityLinks_ChannelType_ExternalUserId]
    ON [ChannelIdentityLinks] ([ChannelType], [ExternalUserId]);
END
""");

            migrationBuilder.Sql("""
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ChannelIdentityLinks_UserId_ChannelType' AND [object_id] = OBJECT_ID(N'[ChannelIdentityLinks]'))
BEGIN
    CREATE UNIQUE INDEX [IX_ChannelIdentityLinks_UserId_ChannelType]
    ON [ChannelIdentityLinks] ([UserId], [ChannelType]);
END
""");

            migrationBuilder.Sql("""
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ChannelIdentityLinks_IsActive' AND [object_id] = OBJECT_ID(N'[ChannelIdentityLinks]'))
BEGIN
    CREATE INDEX [IX_ChannelIdentityLinks_IsActive]
    ON [ChannelIdentityLinks] ([IsActive]);
END
""");

            migrationBuilder.Sql("""
IF OBJECT_ID(N'[ChannelLinkCodes]', N'U') IS NULL
BEGIN
    CREATE TABLE [ChannelLinkCodes](
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ChannelType] int NOT NULL,
        [CodeHash] nvarchar(128) NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [ConsumedAt] datetime2 NULL,
        [AttemptCount] int NOT NULL CONSTRAINT [DF_ChannelLinkCodes_AttemptCount] DEFAULT 0,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ChannelLinkCodes] PRIMARY KEY ([Id])
    );
END
""");

            migrationBuilder.Sql("""
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ChannelLinkCodes_ChannelType_CodeHash' AND [object_id] = OBJECT_ID(N'[ChannelLinkCodes]'))
BEGIN
    CREATE INDEX [IX_ChannelLinkCodes_ChannelType_CodeHash]
    ON [ChannelLinkCodes] ([ChannelType], [CodeHash]);
END
""");

            migrationBuilder.Sql("""
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ChannelLinkCodes_UserId_ChannelType' AND [object_id] = OBJECT_ID(N'[ChannelLinkCodes]'))
BEGIN
    CREATE INDEX [IX_ChannelLinkCodes_UserId_ChannelType]
    ON [ChannelLinkCodes] ([UserId], [ChannelType]);
END
""");

            migrationBuilder.Sql("""
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ChannelLinkCodes_ExpiresAt' AND [object_id] = OBJECT_ID(N'[ChannelLinkCodes]'))
BEGIN
    CREATE INDEX [IX_ChannelLinkCodes_ExpiresAt]
    ON [ChannelLinkCodes] ([ExpiresAt]);
END
""");

            migrationBuilder.Sql("""
IF OBJECT_ID(N'[ChannelInboundMessageLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [ChannelInboundMessageLogs](
        [Id] uniqueidentifier NOT NULL,
        [ChannelType] int NOT NULL,
        [ExternalMessageId] nvarchar(150) NOT NULL,
        [ExternalUserId] nvarchar(128) NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [TraceId] nvarchar(120) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ChannelInboundMessageLogs] PRIMARY KEY ([Id])
    );
END
""");

            migrationBuilder.Sql("""
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ChannelInboundMessageLogs_ChannelType_ExternalMessageId' AND [object_id] = OBJECT_ID(N'[ChannelInboundMessageLogs]'))
BEGIN
    CREATE UNIQUE INDEX [IX_ChannelInboundMessageLogs_ChannelType_ExternalMessageId]
    ON [ChannelInboundMessageLogs] ([ChannelType], [ExternalMessageId]);
END
""");

            migrationBuilder.Sql("""
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ChannelInboundMessageLogs_UserId' AND [object_id] = OBJECT_ID(N'[ChannelInboundMessageLogs]'))
BEGIN
    CREATE INDEX [IX_ChannelInboundMessageLogs_UserId]
    ON [ChannelInboundMessageLogs] ([UserId]);
END
""");

            migrationBuilder.Sql("""
IF COL_LENGTH('CashExpenses', 'Origin') IS NULL
BEGIN
    ALTER TABLE [CashExpenses] ADD [Origin] int NOT NULL CONSTRAINT [DF_CashExpenses_Origin] DEFAULT 0;
END
""");

            migrationBuilder.Sql("""
IF COL_LENGTH('CashExpenses', 'SourceType') IS NULL
BEGIN
    ALTER TABLE [CashExpenses] ADD [SourceType] nvarchar(50) NULL;
END
""");

            migrationBuilder.Sql("""
IF COL_LENGTH('CashExpenses', 'SourceId') IS NULL
BEGIN
    ALTER TABLE [CashExpenses] ADD [SourceId] uniqueidentifier NULL;
END
""");

            migrationBuilder.Sql("""
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_CashExpenses_SourceType_SourceId' AND [object_id] = OBJECT_ID(N'[CashExpenses]'))
BEGIN
    CREATE INDEX [IX_CashExpenses_SourceType_SourceId]
    ON [CashExpenses] ([SourceType], [SourceId]);
END
""");

            migrationBuilder.Sql("""
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_CashExpenses_Origin_SourceType_SourceId' AND [object_id] = OBJECT_ID(N'[CashExpenses]'))
BEGIN
    CREATE UNIQUE INDEX [IX_CashExpenses_Origin_SourceType_SourceId]
    ON [CashExpenses] ([Origin], [SourceType], [SourceId])
    WHERE [SourceId] IS NOT NULL;
END
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_CashExpenses_Origin_SourceType_SourceId' AND [object_id] = OBJECT_ID(N'[CashExpenses]'))
BEGIN
    DROP INDEX [IX_CashExpenses_Origin_SourceType_SourceId] ON [CashExpenses];
END
""");

            migrationBuilder.Sql("""
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_CashExpenses_SourceType_SourceId' AND [object_id] = OBJECT_ID(N'[CashExpenses]'))
BEGIN
    DROP INDEX [IX_CashExpenses_SourceType_SourceId] ON [CashExpenses];
END
""");

            migrationBuilder.Sql("""
IF COL_LENGTH('CashExpenses', 'SourceId') IS NOT NULL
BEGIN
    ALTER TABLE [CashExpenses] DROP COLUMN [SourceId];
END
""");

            migrationBuilder.Sql("""
IF COL_LENGTH('CashExpenses', 'SourceType') IS NOT NULL
BEGIN
    ALTER TABLE [CashExpenses] DROP COLUMN [SourceType];
END
""");

            migrationBuilder.Sql("""
IF COL_LENGTH('CashExpenses', 'Origin') IS NOT NULL
BEGIN
    ALTER TABLE [CashExpenses] DROP CONSTRAINT IF EXISTS [DF_CashExpenses_Origin];
    ALTER TABLE [CashExpenses] DROP COLUMN [Origin];
END
""");

            migrationBuilder.Sql("""
IF OBJECT_ID(N'[ChannelInboundMessageLogs]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [ChannelInboundMessageLogs];
END
""");

            migrationBuilder.Sql("""
IF OBJECT_ID(N'[ChannelLinkCodes]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [ChannelLinkCodes];
END
""");

            migrationBuilder.Sql("""
IF OBJECT_ID(N'[ChannelIdentityLinks]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [ChannelIdentityLinks];
END
""");
        }
    }
}
