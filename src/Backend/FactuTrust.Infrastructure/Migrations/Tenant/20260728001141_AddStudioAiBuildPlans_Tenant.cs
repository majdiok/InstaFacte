using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Plans Studio IA « aperçu → confirmation » (StudioAiBuildPlans). Migration volontairement
    /// limitée à cette seule table : les tables fiscales/emprunts/NCT que le scaffolding EF avait
    /// re-émises (snapshot en retard sur les migrations écrites à la main) sont créées par leurs
    /// propres migrations (AddFiscalLiasse, AddLoans, AddNctNoteOverrides). Le snapshot régénéré
    /// avec cette migration résorbe cette dérive.
    /// </summary>
    public partial class AddStudioAiBuildPlans_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudioAiBuildPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    SpecJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SummaryJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudioAiBuildPlans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudioAiBuildPlans_TenantId_Status_CreatedAt",
                table: "StudioAiBuildPlans",
                columns: new[] { "TenantId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudioAiBuildPlans");
        }
    }
}
