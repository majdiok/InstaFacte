using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260315100000_UniqueInventoryReference")]
    public partial class UniqueInventoryReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix duplicate References before creating unique index: keep first per Reference, renumber rest.
            migrationBuilder.Sql(@"
                ;WITH DupRefs AS (
                    SELECT Reference FROM PhysicalInventories GROUP BY Reference HAVING COUNT(*) > 1
                ),
                DuplicateRows AS (
                    SELECT p.Id, p.Reference,
                           ROW_NUMBER() OVER (PARTITION BY p.Reference ORDER BY p.CreatedAt, p.Id) AS rn
                    FROM PhysicalInventories p
                    INNER JOIN DupRefs d ON p.Reference = d.Reference
                ),
                MaxSeq AS (
                    SELECT ISNULL(MAX(TRY_CAST(SUBSTRING(Reference, 6, 6) AS INT)), 0) AS M FROM PhysicalInventories
                ),
                Numbered AS (
                    SELECT dr.Id, ms.M + ROW_NUMBER() OVER (ORDER BY dr.Id) AS NewSeq
                    FROM DuplicateRows dr
                    CROSS JOIN MaxSeq ms
                    WHERE dr.rn > 1
                )
                UPDATE p
                SET p.Reference = 'INVE-' + RIGHT('000000' + CAST(n.NewSeq AS NVARCHAR(10)), 6)
                FROM PhysicalInventories p
                INNER JOIN Numbered n ON p.Id = n.Id
            ");

            migrationBuilder.DropIndex(
                name: "IX_PhysicalInventories_Reference",
                table: "PhysicalInventories");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalInventories_Reference",
                table: "PhysicalInventories",
                column: "Reference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PhysicalInventories_Reference",
                table: "PhysicalInventories");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalInventories_Reference",
                table: "PhysicalInventories",
                column: "Reference");
        }
    }
}
