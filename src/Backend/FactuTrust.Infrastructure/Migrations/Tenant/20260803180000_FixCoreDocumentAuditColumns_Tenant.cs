using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Adds missing audit / concurrency columns on core sales document tables
    /// when the tenant schema was created or restored without them.
    /// Idempotent: safe if columns already exist.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260803180000_FixCoreDocumentAuditColumns_Tenant")]
    public partial class FixCoreDocumentAuditColumns_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // P0 — POS / wizard submit (AggregateRoot)
            AddAggregateRootAuditColumns(migrationBuilder, "Invoices");
            AddAggregateRootAuditColumns(migrationBuilder, "Clients");

            // P1 — child / draft entities (Entity only)
            AddEntityAuditColumns(migrationBuilder, "InvoiceLines");
            AddEntityAuditColumns(migrationBuilder, "InvoiceDrafts");

            // P2 — other core sales documents (AggregateRoot)
            AddAggregateRootAuditColumns(migrationBuilder, "Quotes");
            AddAggregateRootAuditColumns(migrationBuilder, "SalesOrders");
            AddAggregateRootAuditColumns(migrationBuilder, "DeliveryNotes");
            AddAggregateRootAuditColumns(migrationBuilder, "Products");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty — audit columns should not be dropped once present.
        }

        private static void AddAggregateRootAuditColumns(MigrationBuilder migrationBuilder, string tableName)
        {
            AddColumnIfMissing(migrationBuilder, tableName, "Version",
                $"ALTER TABLE [{tableName}] ADD [Version] int NOT NULL CONSTRAINT [DF_{tableName}_Version] DEFAULT 1;");

            AddColumnIfMissing(migrationBuilder, tableName, "CreatedBy",
                $"ALTER TABLE [{tableName}] ADD [CreatedBy] nvarchar(max) NULL;");

            AddColumnIfMissing(migrationBuilder, tableName, "UpdatedBy",
                $"ALTER TABLE [{tableName}] ADD [UpdatedBy] nvarchar(max) NULL;");
        }

        private static void AddEntityAuditColumns(MigrationBuilder migrationBuilder, string tableName)
        {
            AddColumnIfMissing(migrationBuilder, tableName, "CreatedBy",
                $"ALTER TABLE [{tableName}] ADD [CreatedBy] nvarchar(max) NULL;");

            AddColumnIfMissing(migrationBuilder, tableName, "UpdatedBy",
                $"ALTER TABLE [{tableName}] ADD [UpdatedBy] nvarchar(max) NULL;");
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
