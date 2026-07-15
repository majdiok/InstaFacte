using System;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

/// <summary>
/// Adds the AiExportAudits table used by the PowerPoint export feature (and future AI export
/// formats) to record audit trails per tenant. Strictly additive: no existing tables are altered.
/// </summary>
[DbContext(typeof(TenantDbContext))]
[Migration("20260521180000_AddAiExportAudit_Tenant")]
public partial class AddAiExportAudit_Tenant : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AiExportAudits",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Format = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Template = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                ResponseCount = table.Column<int>(type: "int", nullable: false),
                SlideCount = table.Column<int>(type: "int", nullable: false),
                SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                DurationMs = table.Column<int>(type: "int", nullable: false),
                StoragePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                Success = table.Column<bool>(type: "bit", nullable: false),
                FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                ConversationIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                MessageIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AiExportAudits", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AiExportAudits_UserId",
            table: "AiExportAudits",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_AiExportAudits_GeneratedAt",
            table: "AiExportAudits",
            column: "GeneratedAt");

        migrationBuilder.CreateIndex(
            name: "IX_AiExportAudits_UserId_GeneratedAt",
            table: "AiExportAudits",
            columns: new[] { "UserId", "GeneratedAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AiExportAudits");
    }
}
