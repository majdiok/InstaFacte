using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddThirdPartyAccountingProfiles_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ThirdPartyAccountingProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    ThirdPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuxiliaryCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CollectiveAccountNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaymentTermDays = table.Column<int>(type: "int", nullable: true),
                    AccountingNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThirdPartyAccountingProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ThirdPartyAccountingProfiles_AuxiliaryCode",
                table: "ThirdPartyAccountingProfiles",
                column: "AuxiliaryCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThirdPartyAccountingProfiles_Kind_ThirdPartyId",
                table: "ThirdPartyAccountingProfiles",
                columns: new[] { "Kind", "ThirdPartyId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ThirdPartyAccountingProfiles");
        }
    }
}
