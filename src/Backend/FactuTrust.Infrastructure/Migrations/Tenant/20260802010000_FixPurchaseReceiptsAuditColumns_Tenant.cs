using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Adds missing audit / concurrency columns on PurchaseReceipts tables
    /// when the initial migration was applied without them.
    /// Idempotent: safe if columns already exist.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260802010000_FixPurchaseReceiptsAuditColumns_Tenant")]
    public partial class FixPurchaseReceiptsAuditColumns_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            AddColumnIfMissing(migrationBuilder, "PurchaseReceipts", "Version",
                "ALTER TABLE [PurchaseReceipts] ADD [Version] int NOT NULL CONSTRAINT [DF_PurchaseReceipts_Version] DEFAULT 1;");

            AddColumnIfMissing(migrationBuilder, "PurchaseReceipts", "CreatedBy",
                "ALTER TABLE [PurchaseReceipts] ADD [CreatedBy] nvarchar(max) NULL;");

            AddColumnIfMissing(migrationBuilder, "PurchaseReceipts", "UpdatedBy",
                "ALTER TABLE [PurchaseReceipts] ADD [UpdatedBy] nvarchar(max) NULL;");

            AddColumnIfMissing(migrationBuilder, "PurchaseReceiptLines", "CreatedBy",
                "ALTER TABLE [PurchaseReceiptLines] ADD [CreatedBy] nvarchar(max) NULL;");

            AddColumnIfMissing(migrationBuilder, "PurchaseReceiptLines", "UpdatedBy",
                "ALTER TABLE [PurchaseReceiptLines] ADD [UpdatedBy] nvarchar(max) NULL;");

            AddColumnIfMissing(migrationBuilder, "PurchaseReceiptAttachments", "CreatedBy",
                "ALTER TABLE [PurchaseReceiptAttachments] ADD [CreatedBy] nvarchar(max) NULL;");

            AddColumnIfMissing(migrationBuilder, "PurchaseReceiptAttachments", "UpdatedBy",
                "ALTER TABLE [PurchaseReceiptAttachments] ADD [UpdatedBy] nvarchar(max) NULL;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty — audit columns should not be dropped once present.
        }

        private static void AddColumnIfMissing(
            MigrationBuilder migrationBuilder,
            string tableName,
            string columnName,
            string alterSql)
        {
            migrationBuilder.Sql($@"
                IF EXISTS (SELECT * FROM sys.tables WHERE name = N'{tableName}')
                BEGIN
                    IF NOT EXISTS (
                        SELECT * FROM sys.columns
                        WHERE object_id = OBJECT_ID(N'[{tableName}]') AND name = N'{columnName}')
                    BEGIN
                        {alterSql}
                    END
                END
            ");
        }
    }
}
