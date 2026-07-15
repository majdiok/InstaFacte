using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddJournalCatalog_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JournalFamilies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalFamilies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Journals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Journals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JournalFamilies_Code",
                table: "JournalFamilies",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Journals_Code",
                table: "Journals",
                column: "Code",
                unique: true);

            // Seed des familles et journaux standards (identité de comportement avec la liste figée).
            var seed = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var famVen = new Guid("11111111-0000-0000-0000-000000000001");
            var famAch = new Guid("11111111-0000-0000-0000-000000000002");
            var famTre = new Guid("11111111-0000-0000-0000-000000000003");
            var famOd = new Guid("11111111-0000-0000-0000-000000000004");
            var famImm = new Guid("11111111-0000-0000-0000-000000000005");
            var famAn = new Guid("11111111-0000-0000-0000-000000000006");

            migrationBuilder.InsertData(
                table: "JournalFamilies",
                columns: new[] { "Id", "Code", "Label", "CreatedAt", "CreatedBy" },
                values: new object[,]
                {
                    { famVen, "VEN", "Ventes", seed, "system" },
                    { famAch, "ACH", "Achats", seed, "system" },
                    { famTre, "TRE", "Trésorerie", seed, "system" },
                    { famOd, "OD", "Opérations diverses", seed, "system" },
                    { famImm, "IMM", "Immobilisations", seed, "system" },
                    { famAn, "AN", "À-Nouveaux", seed, "system" }
                });

            migrationBuilder.InsertData(
                table: "Journals",
                columns: new[] { "Id", "Code", "Label", "FamilyId", "IsSystem", "IsActive", "CreatedAt", "CreatedBy" },
                values: new object[,]
                {
                    { new Guid("22222222-0000-0000-0000-000000000001"), "JV", "Journal des Ventes", famVen, true, true, seed, "system" },
                    { new Guid("22222222-0000-0000-0000-000000000002"), "JA", "Journal des Achats", famAch, true, true, seed, "system" },
                    { new Guid("22222222-0000-0000-0000-000000000003"), "JC", "Journal de Caisse", famTre, true, true, seed, "system" },
                    { new Guid("22222222-0000-0000-0000-000000000004"), "JB", "Journal de Banque", famTre, true, true, seed, "system" },
                    { new Guid("22222222-0000-0000-0000-000000000005"), "JOD", "Journal des Opérations Diverses", famOd, true, true, seed, "system" },
                    { new Guid("22222222-0000-0000-0000-000000000006"), "JIM", "Journal des Immobilisations", famImm, true, true, seed, "system" },
                    { new Guid("22222222-0000-0000-0000-000000000007"), "JAN", "Journal des À-Nouveaux", famAn, true, true, seed, "system" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JournalFamilies");

            migrationBuilder.DropTable(
                name: "Journals");
        }
    }
}
