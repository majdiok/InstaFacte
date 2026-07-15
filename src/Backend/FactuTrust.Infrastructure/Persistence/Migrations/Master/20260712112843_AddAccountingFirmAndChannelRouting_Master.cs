using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Persistence.Migrations.Master
{
    /// <inheritdoc />
    public partial class AddAccountingFirmAndChannelRouting_Master : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AccountingFirmProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Governorate = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsPublicInDirectory = table.Column<bool>(type: "bit", nullable: false),
                    ProfessionalRegistrationNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContactPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingFirmProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChannelExternalRoutes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelType = table.Column<int>(type: "int", nullable: false),
                    ExternalUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelExternalRoutes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChannelLinkCodePointers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelType = table.Column<int>(type: "int", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelLinkCodePointers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirmClientAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirmTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmClientAssignments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Kind",
                table: "Tenants",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingFirmProfiles_IsPublicInDirectory_City",
                table: "AccountingFirmProfiles",
                columns: new[] { "IsPublicInDirectory", "City" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingFirmProfiles_TenantId",
                table: "AccountingFirmProfiles",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChannelExternalRoutes_ChannelType_ExternalUserId",
                table: "ChannelExternalRoutes",
                columns: new[] { "ChannelType", "ExternalUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChannelExternalRoutes_TenantId_ChannelType_IsActive",
                table: "ChannelExternalRoutes",
                columns: new[] { "TenantId", "ChannelType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelExternalRoutes_UserId_ChannelType",
                table: "ChannelExternalRoutes",
                columns: new[] { "UserId", "ChannelType" });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelLinkCodePointers_ChannelType_CodeHash",
                table: "ChannelLinkCodePointers",
                columns: new[] { "ChannelType", "CodeHash" });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelLinkCodePointers_ExpiresAt",
                table: "ChannelLinkCodePointers",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_FirmClientAssignments_CompanyTenantId_FirmTenantId_Status",
                table: "FirmClientAssignments",
                columns: new[] { "CompanyTenantId", "FirmTenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FirmClientAssignments_FirmTenantId",
                table: "FirmClientAssignments",
                column: "FirmTenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountingFirmProfiles");

            migrationBuilder.DropTable(
                name: "ChannelExternalRoutes");

            migrationBuilder.DropTable(
                name: "ChannelLinkCodePointers");

            migrationBuilder.DropTable(
                name: "FirmClientAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_Kind",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Tenants");
        }
    }
}
