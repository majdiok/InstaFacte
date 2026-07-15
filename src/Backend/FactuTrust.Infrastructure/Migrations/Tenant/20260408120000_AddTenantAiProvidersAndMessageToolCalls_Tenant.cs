using System;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

[DbContext(typeof(TenantDbContext))]
[Migration("20260408120000_AddTenantAiProvidersAndMessageToolCalls_Tenant")]
public partial class AddTenantAiProvidersAndMessageToolCalls_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "SelectedModel",
            table: "Conversations",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(100)",
            oldMaxLength: 100,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "ToolCallId",
            table: "ConversationMessages",
            type: "nvarchar(128)",
            maxLength: 128,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(50)",
            oldMaxLength: 50,
            oldNullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ToolCallsJson",
            table: "ConversationMessages",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "TenantAiProviders",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProviderKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                BaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                EncryptedApiKey = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                LastValidatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TenantAiProviders", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_TenantAiProviders_ProviderKey",
            table: "TenantAiProviders",
            column: "ProviderKey",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "TenantAiProviders");

        migrationBuilder.DropColumn(
            name: "ToolCallsJson",
            table: "ConversationMessages");

        migrationBuilder.AlterColumn<string>(
            name: "ToolCallId",
            table: "ConversationMessages",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(128)",
            oldMaxLength: 128,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "SelectedModel",
            table: "Conversations",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(500)",
            oldMaxLength: 500,
            oldNullable: true);
    }
}
