using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddTaxes_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Taxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TaxType = table.Column<int>(type: "int", nullable: false),
                    ValueType = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ApplicableContext = table.Column<int>(type: "int", nullable: false),
                    IsAppliedToProducts = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Taxes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Taxes_DisplayOrder",
                table: "Taxes",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_Taxes_IsActive",
                table: "Taxes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Taxes_TaxType",
                table: "Taxes",
                column: "TaxType");

            // TVA système (0, 7, 13, 19 %) + timbre fiscal — identifiants stables pour référence éventuelle
            migrationBuilder.Sql("""
                INSERT INTO [Taxes] ([Id],[Name],[TaxType],[ValueType],[Value],[ApplicableContext],[IsAppliedToProducts],[IsActive],[IsSystem],[DisplayOrder],[CreatedAt],[UpdatedAt],[CreatedBy],[UpdatedBy],[Version])
                VALUES
                ('11111111-1111-4111-8111-111111110001', N'TVA 0 %', 0, 0, 0, 0, 0, 1, 1, 0, SYSUTCDATETIME(), NULL, NULL, NULL, 1),
                ('11111111-1111-4111-8111-111111110007', N'TVA 7 %', 0, 0, 7, 0, 0, 1, 1, 1, SYSUTCDATETIME(), NULL, NULL, NULL, 1),
                ('11111111-1111-4111-8111-111111110013', N'TVA 13 %', 0, 0, 13, 0, 0, 1, 1, 2, SYSUTCDATETIME(), NULL, NULL, NULL, 1),
                ('11111111-1111-4111-8111-111111110019', N'TVA 19 %', 0, 0, 19, 0, 1, 1, 1, 3, SYSUTCDATETIME(), NULL, NULL, NULL, 1),
                ('22222222-2222-4222-8222-222222220001', N'Timbre fiscal', 1, 1, 1.000, 0, 0, 1, 1, 10, SYSUTCDATETIME(), NULL, NULL, NULL, 1);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Taxes");
        }
    }
}
