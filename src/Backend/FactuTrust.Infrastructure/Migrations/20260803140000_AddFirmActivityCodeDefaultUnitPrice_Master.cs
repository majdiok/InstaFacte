using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MasterDbContext))]
    [Migration("20260803140000_AddFirmActivityCodeDefaultUnitPrice_Master")]
    public partial class AddFirmActivityCodeDefaultUnitPrice_Master : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'dbo.FirmActivityCodes', N'DefaultUnitPrice') IS NULL
BEGIN
    ALTER TABLE [FirmActivityCodes] ADD [DefaultUnitPrice] decimal(18,3) NULL;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'dbo.FirmActivityCodes', N'DefaultUnitPrice') IS NOT NULL
BEGIN
    ALTER TABLE [FirmActivityCodes] DROP COLUMN [DefaultUnitPrice];
END
");
        }
    }
}
