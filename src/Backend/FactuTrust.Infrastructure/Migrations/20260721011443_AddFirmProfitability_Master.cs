using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFirmProfitability_Master : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "HourlyCostRate",
                table: "FirmCollaboratorProfiles",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FirmCollaboratorRentabilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirmTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollaboratorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollaboratorDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Rentability = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmCollaboratorRentabilities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirmDossierYearBudgets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirmTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirmClientAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    BudgetAnnuel = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmDossierYearBudgets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirmRentabilityYearConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirmTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    GlobalPayrollMass = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalManagers = table.Column<int>(type: "int", nullable: false),
                    GlobalItCharges = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalManagersIt = table.Column<int>(type: "int", nullable: false),
                    GlobalOperatingCharges = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalManagersOperating = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmRentabilityYearConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirmCollaboratorRentabilityLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollaboratorRentabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceCode = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    LineCollaboratorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmCollaboratorRentabilityLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FirmCollaboratorRentabilityLines_FirmCollaboratorRentabilities_CollaboratorRentabilityId",
                        column: x => x.CollaboratorRentabilityId,
                        principalTable: "FirmCollaboratorRentabilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FirmCollaboratorRentabilities_FirmTenantId_CollaboratorUserId_Year",
                table: "FirmCollaboratorRentabilities",
                columns: new[] { "FirmTenantId", "CollaboratorUserId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FirmCollaboratorRentabilityLines_CollaboratorRentabilityId_ReferenceCode_LineCollaboratorUserId",
                table: "FirmCollaboratorRentabilityLines",
                columns: new[] { "CollaboratorRentabilityId", "ReferenceCode", "LineCollaboratorUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_FirmDossierYearBudgets_FirmClientAssignmentId_Year",
                table: "FirmDossierYearBudgets",
                columns: new[] { "FirmClientAssignmentId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FirmDossierYearBudgets_FirmTenantId_Year",
                table: "FirmDossierYearBudgets",
                columns: new[] { "FirmTenantId", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_FirmRentabilityYearConfigs_FirmTenantId_Year",
                table: "FirmRentabilityYearConfigs",
                columns: new[] { "FirmTenantId", "Year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FirmCollaboratorRentabilityLines");

            migrationBuilder.DropTable(
                name: "FirmDossierYearBudgets");

            migrationBuilder.DropTable(
                name: "FirmRentabilityYearConfigs");

            migrationBuilder.DropTable(
                name: "FirmCollaboratorRentabilities");

            migrationBuilder.DropColumn(
                name: "HourlyCostRate",
                table: "FirmCollaboratorProfiles");
        }
    }
}
