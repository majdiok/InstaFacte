using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Persistence.Migrations.Master
{
    /// <inheritdoc />
    public partial class AddFirmGovernance_Master : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FirmExpenseNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirmTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirmClientAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LegalRepresentativeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RepresentativeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    PeriodMonth = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalToReimburse = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    MixedCharges = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    OperatingExpenses = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    MileageAllowance = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    SalesAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmExpenseNotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirmTimeSheetEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirmTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FirmClientAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientCompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WorkDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Hours = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    ActivityCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsBillable = table.Column<bool>(type: "bit", nullable: false),
                    IsValidated = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmTimeSheetEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FiscalCalendarRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObligationType = table.Column<int>(type: "int", nullable: false),
                    ApplicableTaxRegime = table.Column<int>(type: "int", nullable: true),
                    DueDayOfMonth = table.Column<int>(type: "int", nullable: false),
                    MonthsAfterPeriod = table.Column<int>(type: "int", nullable: false),
                    ShiftWeekendsAndHolidays = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalCalendarRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LegalRepresentatives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermanentFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Cin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Nationality = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CnssNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Role = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    HasProSpace = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalRepresentatives", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PermanentFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirmClientAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirmTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    WizardStep = table.Column<int>(type: "int", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Nif = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RneIdentifier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LegalForm = table.Column<int>(type: "int", nullable: true),
                    IncorporationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ShareCapital = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "TND"),
                    Street = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Governorate = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    TaxOffice = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TaxRegime = table.Column<int>(type: "int", nullable: true),
                    HasTaxCertificate = table.Column<bool>(type: "bit", nullable: false),
                    FiscalYearStartMonth = table.Column<int>(type: "int", nullable: true),
                    FiscalYearEndMonth = table.Column<int>(type: "int", nullable: true),
                    CurrentLegalAct = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MissionStatus = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsDigitized = table.Column<bool>(type: "bit", nullable: false),
                    MissionResigned = table.Column<bool>(type: "bit", nullable: false),
                    ResignationFiscalYear = table.Column<int>(type: "int", nullable: true),
                    ResignationNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LabCompleted = table.Column<bool>(type: "bit", nullable: false),
                    MissionAccepted = table.Column<bool>(type: "bit", nullable: false),
                    BillingNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AssignedAccountantUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedAccountantName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SyncedToTenantAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermanentFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Shareholders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermanentFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsLegalEntity = table.Column<bool>(type: "bit", nullable: false),
                    CinOrNif = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ShareCount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    SharePercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shareholders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FirmExpenseNotes_FirmTenantId_FirmClientAssignmentId_PeriodYear_PeriodMonth",
                table: "FirmExpenseNotes",
                columns: new[] { "FirmTenantId", "FirmClientAssignmentId", "PeriodYear", "PeriodMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FirmTimeSheetEntries_FirmTenantId_WorkDate",
                table: "FirmTimeSheetEntries",
                columns: new[] { "FirmTenantId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FiscalCalendarRules_ObligationType_ApplicableTaxRegime",
                table: "FiscalCalendarRules",
                columns: new[] { "ObligationType", "ApplicableTaxRegime" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalRepresentatives_PermanentFileId",
                table: "LegalRepresentatives",
                column: "PermanentFileId");

            migrationBuilder.CreateIndex(
                name: "IX_PermanentFiles_FirmClientAssignmentId",
                table: "PermanentFiles",
                column: "FirmClientAssignmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermanentFiles_FirmTenantId",
                table: "PermanentFiles",
                column: "FirmTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Shareholders_PermanentFileId",
                table: "Shareholders",
                column: "PermanentFileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FirmExpenseNotes");

            migrationBuilder.DropTable(
                name: "FirmTimeSheetEntries");

            migrationBuilder.DropTable(
                name: "FiscalCalendarRules");

            migrationBuilder.DropTable(
                name: "LegalRepresentatives");

            migrationBuilder.DropTable(
                name: "PermanentFiles");

            migrationBuilder.DropTable(
                name: "Shareholders");
        }
    }
}
