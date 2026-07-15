using System;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260315000000_AddInventoryReferenceAndSequence")]
    public partial class AddInventoryReferenceAndSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Reference",
                table: "PhysicalInventories",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InventoryNumberSequences",
                columns: table => new
                {
                    Year = table.Column<int>(type: "int", nullable: false),
                    LastSequence = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryNumberSequences", x => x.Year);
                });

            // Backfill Reference for existing PhysicalInventories (ordered by CreatedAt, Id)
            migrationBuilder.Sql(@"
                WITH Ordered AS (
                    SELECT Id, ROW_NUMBER() OVER (ORDER BY CreatedAt, Id) AS Rn
                    FROM PhysicalInventories
                    WHERE Reference IS NULL
                )
                UPDATE p
                SET p.Reference = 'INVE-' + RIGHT('000000' + CAST(o.Rn AS NVARCHAR(10)), 6)
                FROM PhysicalInventories p
                INNER JOIN Ordered o ON p.Id = o.Id
            ");

            migrationBuilder.AlterColumn<string>(
                name: "Reference",
                table: "PhysicalInventories",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalInventories_Reference",
                table: "PhysicalInventories",
                column: "Reference");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PhysicalInventories_Reference",
                table: "PhysicalInventories");

            migrationBuilder.DropTable(
                name: "InventoryNumberSequences");

            migrationBuilder.DropColumn(
                name: "Reference",
                table: "PhysicalInventories");
        }
    }
}
