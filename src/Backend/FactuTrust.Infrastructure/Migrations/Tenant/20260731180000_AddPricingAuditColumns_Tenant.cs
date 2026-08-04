using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Adds missing audit / concurrency columns on pricing tables
    /// (CreatedBy / UpdatedBy / Version) when the initial pricing migrations
    /// created the tables without Entity / AggregateRoot columns.
    /// Idempotent: safe if columns already exist.
    /// Required for POS / wizard submit (PriceResolver reads ClientProductPrices).
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260731180000_AddPricingAuditColumns_Tenant")]
    public partial class AddPricingAuditColumns_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AggregateRoot tables
            AddAggregateRootAuditColumns(migrationBuilder, "PriceLists");
            AddAggregateRootAuditColumns(migrationBuilder, "ClientProductPrices");
            AddAggregateRootAuditColumns(migrationBuilder, "Promotions");
            AddAggregateRootAuditColumns(migrationBuilder, "PaymentTermTemplates");

            // Entity tables
            AddEntityAuditColumns(migrationBuilder, "PriceListItems");
            AddEntityAuditColumns(migrationBuilder, "PriceListItemTiers");
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
