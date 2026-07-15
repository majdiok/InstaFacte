using System;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

/// <inheritdoc />
[DbContext(typeof(TenantDbContext))]
[Migration("20260401022000_AddTejXmlExportLogs_Tenant")]
public partial class AddTejXmlExportLogs_Tenant : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TejXmlExportLogs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Year = table.Column<int>(type: "int", nullable: false),
                Month = table.Column<int>(type: "int", nullable: false),
                SubmissionType = table.Column<int>(type: "int", nullable: false),
                FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                Sha256Hex = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                CertificateCount = table.Column<int>(type: "int", nullable: false),
                IsValid = table.Column<bool>(type: "bit", nullable: false),
                ValidationErrorSummary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                ExportedByEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TejXmlExportLogs", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_TejXmlExportLogs_CreatedAt",
            table: "TejXmlExportLogs",
            column: "CreatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_TejXmlExportLogs_Year_Month",
            table: "TejXmlExportLogs",
            columns: new[] { "Year", "Month" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "TejXmlExportLogs");
    }
}
