using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddJournalEntryTemplates_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JournalEntryTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    JournalCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    LabelTemplate = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UsageCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntryTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JournalEntryTemplateLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalEntryTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LineLabelTemplate = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FixedDebit = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    FixedCredit = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntryTemplateLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalEntryTemplateLines_JournalEntryTemplates_JournalEntryTemplateId",
                        column: x => x.JournalEntryTemplateId,
                        principalTable: "JournalEntryTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryTemplateLines_JournalEntryTemplateId",
                table: "JournalEntryTemplateLines",
                column: "JournalEntryTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryTemplates_IsActive",
                table: "JournalEntryTemplates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryTemplates_Name",
                table: "JournalEntryTemplates",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JournalEntryTemplateLines");

            migrationBuilder.DropTable(
                name: "JournalEntryTemplates");
        }
    }
}
