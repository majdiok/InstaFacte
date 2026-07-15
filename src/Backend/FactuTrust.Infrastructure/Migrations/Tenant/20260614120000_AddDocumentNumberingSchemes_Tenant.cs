using System;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260614120000_AddDocumentNumberingSchemes_Tenant")]
    public partial class AddDocumentNumberingSchemes_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentNumberingSchemes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<int>(type: "int", nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    StartNumber = table.Column<int>(type: "int", nullable: false),
                    CurrentSequence = table.Column<int>(type: "int", nullable: false),
                    FormatBlocksJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsFormatLocked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_DocumentNumberingSchemes", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_DocumentNumberingSchemes_TenantId_DocumentType_FiscalYear",
                table: "DocumentNumberingSchemes",
                columns: new[] { "TenantId", "DocumentType", "FiscalYear" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "DocumentNumberingSchemes");
    }
}