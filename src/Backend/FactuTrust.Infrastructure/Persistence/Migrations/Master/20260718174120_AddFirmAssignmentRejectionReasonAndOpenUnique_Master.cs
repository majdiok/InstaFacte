using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Persistence.Migrations.Master
{
    /// <inheritdoc />
    public partial class AddFirmAssignmentRejectionReasonAndOpenUnique_Master : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "FirmClientAssignments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            // Neutralise les doublons « ouverts » éventuels avant de poser l'index unique :
            // pour chaque société, seule la ligne ouverte la plus récente est conservée,
            // les plus anciennes passent en CancelledByCompany (5).
            migrationBuilder.Sql(@"
WITH OpenDuplicates AS (
    SELECT Id,
           ROW_NUMBER() OVER (
               PARTITION BY CompanyTenantId
               ORDER BY RequestedAt DESC, Id DESC) AS RowNum
    FROM FirmClientAssignments
    WHERE [Status] IN (0, 1)
)
UPDATE a
SET a.[Status] = 5,
    a.RevokedAt = SYSUTCDATETIME()
FROM FirmClientAssignments a
INNER JOIN OpenDuplicates d ON d.Id = a.Id
WHERE d.RowNum > 1;
");

            migrationBuilder.CreateIndex(
                name: "IX_FirmClientAssignments_CompanyTenantId_Open",
                table: "FirmClientAssignments",
                column: "CompanyTenantId",
                unique: true,
                filter: "[Status] IN (0, 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FirmClientAssignments_CompanyTenantId_Open",
                table: "FirmClientAssignments");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "FirmClientAssignments");
        }
    }
}
