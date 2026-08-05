using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MasterDbContext))]
    [Migration("20260805020000_AddExchangePerfIndexes_Master")]
    public partial class AddExchangePerfIndexes_Master : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ExchangeMessageReads_UserId_MessageId' AND object_id = OBJECT_ID(N'dbo.ExchangeMessageReads'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ExchangeMessageReads_UserId_MessageId]
        ON [dbo].[ExchangeMessageReads] ([UserId], [MessageId]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ExchangeThreads_CompanyTenantId_LastActivityAt' AND object_id = OBJECT_ID(N'dbo.ExchangeThreads'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ExchangeThreads_CompanyTenantId_LastActivityAt]
        ON [dbo].[ExchangeThreads] ([CompanyTenantId], [LastActivityAt] DESC)
        INCLUDE ([FirmClientAssignmentId]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ExchangeMessages_ThreadId_Visibility_SentAt' AND object_id = OBJECT_ID(N'dbo.ExchangeMessages'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ExchangeMessages_ThreadId_Visibility_SentAt]
        ON [dbo].[ExchangeMessages] ([ThreadId], [Visibility], [SentAt]);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ExchangeMessageReads_UserId_MessageId' AND object_id = OBJECT_ID(N'dbo.ExchangeMessageReads'))
    DROP INDEX [IX_ExchangeMessageReads_UserId_MessageId] ON [dbo].[ExchangeMessageReads];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ExchangeThreads_CompanyTenantId_LastActivityAt' AND object_id = OBJECT_ID(N'dbo.ExchangeThreads'))
    DROP INDEX [IX_ExchangeThreads_CompanyTenantId_LastActivityAt] ON [dbo].[ExchangeThreads];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ExchangeMessages_ThreadId_Visibility_SentAt' AND object_id = OBJECT_ID(N'dbo.ExchangeMessages'))
    DROP INDEX [IX_ExchangeMessages_ThreadId_Visibility_SentAt] ON [dbo].[ExchangeMessages];
");
        }
    }
}
