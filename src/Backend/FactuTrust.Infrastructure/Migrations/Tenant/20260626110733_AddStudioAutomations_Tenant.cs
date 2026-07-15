using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddStudioAutomations_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomAutomationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AutomationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RunAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RunBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomAutomationRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomEntityAutomations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Trigger = table.Column<int>(type: "int", nullable: false),
                    ActionKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MappingJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConditionJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RunOnce = table.Column<bool>(type: "bit", nullable: false),
                    ScheduleJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomEntityAutomations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomAutomationRuns_TenantId_AutomationId_RecordId",
                table: "CustomAutomationRuns",
                columns: new[] { "TenantId", "AutomationId", "RecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomEntityAutomations_TenantId_EntityDefinitionId",
                table: "CustomEntityAutomations",
                columns: new[] { "TenantId", "EntityDefinitionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomAutomationRuns");

            migrationBuilder.DropTable(
                name: "CustomEntityAutomations");
        }
    }
}
