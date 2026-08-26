using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations;

[DbContext(typeof(MasterDbContext))]
[Migration("20260825193000_AddExchangeRequestComments_Master")]
public partial class AddExchangeRequestComments_Master : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.ExchangeRequestComments', N'U') IS NULL
BEGIN
    CREATE TABLE [ExchangeRequestComments] (
        [Id] uniqueidentifier NOT NULL,
        [ThreadId] uniqueidentifier NOT NULL,
        [RequestId] uniqueidentifier NOT NULL,
        [AuthorUserId] uniqueidentifier NOT NULL,
        [AuthorTenantId] uniqueidentifier NOT NULL,
        [AuthorDisplayName] nvarchar(200) NOT NULL,
        [Body] nvarchar(4000) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ExchangeRequestComments] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_ExchangeRequestComments_RequestId_CreatedAt]
        ON [ExchangeRequestComments] ([RequestId], [CreatedAt]);
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.ExchangeRequestComments', N'U') IS NOT NULL
    DROP TABLE [ExchangeRequestComments];
""");
    }
}
