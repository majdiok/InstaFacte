using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddStorefrontPublishing_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OriginStorefrontOrderId",
                table: "Quotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPubliclyListed",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "StorefrontOutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AggregateType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AggregateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceVersion = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorefrontOutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_OriginStorefrontOrderId",
                table: "Quotes",
                column: "OriginStorefrontOrderId",
                filter: "[OriginStorefrontOrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsPubliclyListed",
                table: "Products",
                column: "IsPubliclyListed");

            migrationBuilder.CreateIndex(
                name: "IX_StorefrontOutbox_AggregateId",
                table: "StorefrontOutboxMessages",
                column: "AggregateId");

            migrationBuilder.CreateIndex(
                name: "IX_StorefrontOutbox_Pending",
                table: "StorefrontOutboxMessages",
                columns: new[] { "ProcessedAt", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StorefrontOutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_OriginStorefrontOrderId",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_Products_IsPubliclyListed",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "OriginStorefrontOrderId",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "IsPubliclyListed",
                table: "Products");
        }
    }
}
