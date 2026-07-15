using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddJournalEntryPieceInfo_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PieceDate",
                table: "JournalEntries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PieceRef",
                table: "JournalEntries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_PieceRef",
                table: "JournalEntries",
                column: "PieceRef");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_PieceRef",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "PieceDate",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "PieceRef",
                table: "JournalEntries");
        }
    }
}
