using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260905180000_AddProjectBillableTimesheetsFlags_Tenant")]
    public partial class AddProjectBillableTimesheetsFlags_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.Projects', N'IsBillable') IS NULL
    ALTER TABLE [Projects] ADD [IsBillable] bit NOT NULL CONSTRAINT [DF_Projects_IsBillable] DEFAULT (1);
IF COL_LENGTH(N'dbo.Projects', N'TimesheetsEnabled') IS NULL
    ALTER TABLE [Projects] ADD [TimesheetsEnabled] bit NOT NULL CONSTRAINT [DF_Projects_TimesheetsEnabled] DEFAULT (1);
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.Projects', N'TimesheetsEnabled') IS NOT NULL
    ALTER TABLE [Projects] DROP CONSTRAINT [DF_Projects_TimesheetsEnabled];
IF COL_LENGTH(N'dbo.Projects', N'TimesheetsEnabled') IS NOT NULL
    ALTER TABLE [Projects] DROP COLUMN [TimesheetsEnabled];
IF COL_LENGTH(N'dbo.Projects', N'IsBillable') IS NOT NULL
    ALTER TABLE [Projects] DROP CONSTRAINT [DF_Projects_IsBillable];
IF COL_LENGTH(N'dbo.Projects', N'IsBillable') IS NOT NULL
    ALTER TABLE [Projects] DROP COLUMN [IsBillable];
""");
        }
    }
}
