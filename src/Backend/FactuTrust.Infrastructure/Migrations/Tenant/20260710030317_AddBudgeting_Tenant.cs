using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddBudgeting_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BudgetPosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    AccountPrefixes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetPosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BudgetYears",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ValidatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValidatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetYears", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BudgetLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BudgetPostId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetLines_BudgetPosts_BudgetPostId",
                        column: x => x.BudgetPostId,
                        principalTable: "BudgetPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLines_BudgetPostId_FiscalYear_Version_Month",
                table: "BudgetLines",
                columns: new[] { "BudgetPostId", "FiscalYear", "Version", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLines_FiscalYear_Version",
                table: "BudgetLines",
                columns: new[] { "FiscalYear", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPosts_Code",
                table: "BudgetPosts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetYears_FiscalYear",
                table: "BudgetYears",
                column: "FiscalYear",
                unique: true);

            // Postes budgétaires standards SCE (Kind : 0 = charges, 1 = produits) — modifiables/désactivables.
            var seed = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            migrationBuilder.InsertData(
                table: "BudgetPosts",
                columns: new[] { "Id", "Code", "Label", "Kind", "AccountPrefixes", "IsActive", "DisplayOrder", "CreatedAt", "CreatedBy" },
                values: new object[,]
                {
                    { new Guid("44444444-0000-0000-0000-000000000001"), "60", "Achats consommés", 0, "60", true, 10, seed, "system" },
                    { new Guid("44444444-0000-0000-0000-000000000002"), "61", "Services extérieurs", 0, "61", true, 20, seed, "system" },
                    { new Guid("44444444-0000-0000-0000-000000000003"), "62", "Autres services extérieurs", 0, "62", true, 30, seed, "system" },
                    { new Guid("44444444-0000-0000-0000-000000000004"), "63", "Charges diverses ordinaires", 0, "63", true, 40, seed, "system" },
                    { new Guid("44444444-0000-0000-0000-000000000005"), "64", "Charges de personnel", 0, "64", true, 50, seed, "system" },
                    { new Guid("44444444-0000-0000-0000-000000000006"), "65", "Charges financières", 0, "65", true, 60, seed, "system" },
                    { new Guid("44444444-0000-0000-0000-000000000007"), "66", "Impôts, taxes et versements assimilés", 0, "66", true, 70, seed, "system" },
                    { new Guid("44444444-0000-0000-0000-000000000008"), "67", "Pertes extraordinaires", 0, "67", true, 80, seed, "system" },
                    { new Guid("44444444-0000-0000-0000-000000000009"), "68", "Dotations aux amortissements et provisions", 0, "68", true, 90, seed, "system" },
                    { new Guid("44444444-0000-0000-0000-000000000010"), "70", "Ventes et produits des activités", 1, "70", true, 100, seed, "system" },
                    { new Guid("44444444-0000-0000-0000-000000000011"), "71", "Production stockée", 1, "71", true, 110, seed, "system" },
                    { new Guid("44444444-0000-0000-0000-000000000012"), "72", "Production immobilisée", 1, "72", true, 120, seed, "system" },
                    { new Guid("44444444-0000-0000-0000-000000000013"), "73", "Produits divers ordinaires", 1, "73", true, 130, seed, "system" },
                    { new Guid("44444444-0000-0000-0000-000000000014"), "75", "Produits financiers", 1, "75", true, 140, seed, "system" },
                    { new Guid("44444444-0000-0000-0000-000000000015"), "77", "Gains extraordinaires", 1, "77", true, 150, seed, "system" },
                    { new Guid("44444444-0000-0000-0000-000000000016"), "78", "Reprises sur amortissements et provisions", 1, "78", true, 160, seed, "system" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BudgetLines");

            migrationBuilder.DropTable(
                name: "BudgetYears");

            migrationBuilder.DropTable(
                name: "BudgetPosts");
        }
    }
}
