using System;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

[DbContext(typeof(TenantDbContext))]
[Migration("20260407120000_AddAiConversations_Tenant")]
public partial class AddAiConversations_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Conversations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                LastMessageAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                SelectedModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Conversations", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ConversationMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Role = table.Column<int>(type: "int", nullable: false),
                Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                SortOrder = table.Column<int>(type: "int", nullable: false),
                ToolName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ToolCallId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ConversationMessages", x => x.Id);
                table.ForeignKey(
                    name: "FK_ConversationMessages_Conversations_ConversationId",
                    column: x => x.ConversationId,
                    principalTable: "Conversations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Conversations_UserId",
            table: "Conversations",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_Conversations_LastMessageAt",
            table: "Conversations",
            column: "LastMessageAt");

        migrationBuilder.CreateIndex(
            name: "IX_ConversationMessages_ConversationId_SortOrder",
            table: "ConversationMessages",
            columns: new[] { "ConversationId", "SortOrder" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ConversationMessages");
        migrationBuilder.DropTable(name: "Conversations");
    }
}
