using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260906120000_SplitProjectMemberSalesRate_Tenant")]
    public partial class SplitProjectMemberSalesRate_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.ProjectMembers', N'SalesRate') IS NULL
    ALTER TABLE [ProjectMembers] ADD [SalesRate] decimal(18,3) NULL;
""");

            migrationBuilder.Sql("""
UPDATE [ProjectMembers]
SET [SalesRate] = [DailyRate]
WHERE [SalesRate] IS NULL AND [DailyRate] IS NOT NULL AND [DailyRate] > 0;

UPDATE [ProjectMembers]
SET [SalesRate] = [HourlyCost] * 8
WHERE [SalesRate] IS NULL AND [HourlyCost] IS NOT NULL AND [HourlyCost] > 0;

UPDATE [ProjectMembers]
SET [HourlyCost] = ROUND([DailyRate] / 8, 3)
WHERE ([HourlyCost] IS NULL OR [HourlyCost] <= 0)
  AND [DailyRate] IS NOT NULL AND [DailyRate] > 0;
""");

            migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.ProjectMembers', N'DailyRate') IS NOT NULL
    ALTER TABLE [ProjectMembers] DROP COLUMN [DailyRate];
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.ProjectMembers', N'DailyRate') IS NULL
    ALTER TABLE [ProjectMembers] ADD [DailyRate] decimal(18,3) NULL;
""");

            migrationBuilder.Sql("""
UPDATE [ProjectMembers]
SET [DailyRate] = [SalesRate]
WHERE [SalesRate] IS NOT NULL AND [SalesRate] > 0;
""");

            migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.ProjectMembers', N'SalesRate') IS NOT NULL
    ALTER TABLE [ProjectMembers] DROP COLUMN [SalesRate];
""");
        }
    }
}
