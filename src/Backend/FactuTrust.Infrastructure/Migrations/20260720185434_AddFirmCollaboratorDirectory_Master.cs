using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFirmCollaboratorDirectory_Master : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FirmCollaboratorLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChildUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsSecondManager = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmCollaboratorLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FirmCollaboratorLinks_Users_ChildUserId",
                        column: x => x.ChildUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FirmCollaboratorLinks_Users_ParentUserId",
                        column: x => x.ParentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FirmCollaboratorProfiles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Civility = table.Column<int>(type: "int", nullable: false),
                    Qualification = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PhoneLandline = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    UseFirmAddress = table.Column<bool>(type: "bit", nullable: false),
                    AddressLine = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CniFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    CniContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CniUploadedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmCollaboratorProfiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_FirmCollaboratorProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FirmCollaboratorLinks_ChildUserId",
                table: "FirmCollaboratorLinks",
                column: "ChildUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FirmCollaboratorLinks_ParentUserId_ChildUserId",
                table: "FirmCollaboratorLinks",
                columns: new[] { "ParentUserId", "ChildUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FirmCollaboratorLinks");

            migrationBuilder.DropTable(
                name: "FirmCollaboratorProfiles");
        }
    }
}
