using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260903190000_AddProjectTaskBillingState_Tenant")]
    public partial class AddProjectTaskBillingState_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.ProjectTasks', N'InvoicedInvoiceId') IS NULL
    ALTER TABLE [ProjectTasks] ADD [InvoicedInvoiceId] uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.ProjectTasks', N'InvoicedBillingMethod') IS NULL
    ALTER TABLE [ProjectTasks] ADD [InvoicedBillingMethod] int NULL;
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_ProjectTasks_InvoicedInvoiceId'
      AND object_id = OBJECT_ID(N'dbo.ProjectTasks'))
    CREATE INDEX [IX_ProjectTasks_InvoicedInvoiceId] ON [ProjectTasks] ([InvoicedInvoiceId])
    WHERE [InvoicedInvoiceId] IS NOT NULL;
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_ProjectTasks_InvoicedInvoiceId'
      AND object_id = OBJECT_ID(N'dbo.ProjectTasks'))
    DROP INDEX [IX_ProjectTasks_InvoicedInvoiceId] ON [ProjectTasks];
IF COL_LENGTH(N'dbo.ProjectTasks', N'InvoicedBillingMethod') IS NOT NULL
    ALTER TABLE [ProjectTasks] DROP COLUMN [InvoicedBillingMethod];
IF COL_LENGTH(N'dbo.ProjectTasks', N'InvoicedInvoiceId') IS NOT NULL
    ALTER TABLE [ProjectTasks] DROP COLUMN [InvoicedInvoiceId];
""");
        }
    }
}
