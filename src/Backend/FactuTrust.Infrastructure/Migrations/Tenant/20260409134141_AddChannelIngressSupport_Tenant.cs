using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddChannelIngressSupport_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChannelIdentityLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelType = table.Column<int>(type: "int", nullable: false),
                    ExternalUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExternalChatId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelIdentityLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChannelInboundMessageLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelType = table.Column<int>(type: "int", nullable: false),
                    ExternalMessageId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ExternalUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TraceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelInboundMessageLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChannelLinkCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelType = table.Column<int>(type: "int", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelLinkCodes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelIdentityLinks_ChannelType_ExternalUserId",
                table: "ChannelIdentityLinks",
                columns: new[] { "ChannelType", "ExternalUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChannelIdentityLinks_IsActive",
                table: "ChannelIdentityLinks",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelIdentityLinks_UserId_ChannelType",
                table: "ChannelIdentityLinks",
                columns: new[] { "UserId", "ChannelType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChannelInboundMessageLogs_ChannelType_ExternalMessageId",
                table: "ChannelInboundMessageLogs",
                columns: new[] { "ChannelType", "ExternalMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChannelInboundMessageLogs_UserId",
                table: "ChannelInboundMessageLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelLinkCodes_ChannelType_CodeHash",
                table: "ChannelLinkCodes",
                columns: new[] { "ChannelType", "CodeHash" });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelLinkCodes_ExpiresAt",
                table: "ChannelLinkCodes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelLinkCodes_UserId_ChannelType",
                table: "ChannelLinkCodes",
                columns: new[] { "UserId", "ChannelType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChannelLinkCodes_UserId_ChannelType",
                table: "ChannelLinkCodes");

            migrationBuilder.DropIndex(
                name: "IX_ChannelLinkCodes_ExpiresAt",
                table: "ChannelLinkCodes");

            migrationBuilder.DropIndex(
                name: "IX_ChannelLinkCodes_ChannelType_CodeHash",
                table: "ChannelLinkCodes");

            migrationBuilder.DropIndex(
                name: "IX_ChannelInboundMessageLogs_UserId",
                table: "ChannelInboundMessageLogs");

            migrationBuilder.DropIndex(
                name: "IX_ChannelInboundMessageLogs_ChannelType_ExternalMessageId",
                table: "ChannelInboundMessageLogs");

            migrationBuilder.DropIndex(
                name: "IX_ChannelIdentityLinks_UserId_ChannelType",
                table: "ChannelIdentityLinks");

            migrationBuilder.DropIndex(
                name: "IX_ChannelIdentityLinks_IsActive",
                table: "ChannelIdentityLinks");

            migrationBuilder.DropIndex(
                name: "IX_ChannelIdentityLinks_ChannelType_ExternalUserId",
                table: "ChannelIdentityLinks");

            migrationBuilder.DropTable(
                name: "ChannelLinkCodes");

            migrationBuilder.DropTable(
                name: "ChannelInboundMessageLogs");

            migrationBuilder.DropTable(
                name: "ChannelIdentityLinks");
        }
    }
}
