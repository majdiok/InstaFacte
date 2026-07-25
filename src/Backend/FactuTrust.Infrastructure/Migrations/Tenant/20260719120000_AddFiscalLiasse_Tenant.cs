using System;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

[DbContext(typeof(TenantDbContext))]
[Migration("20260719120000_AddFiscalLiasse_Tenant")]
public partial class AddFiscalLiasse_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "IncomeTaxYearParameters",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FiscalYear = table.Column<int>(type: "int", nullable: false),
                IsStandardRate = table.Column<decimal>(type: "decimal(9,5)", precision: 9, scale: 5, nullable: false),
                IsReducedRate = table.Column<decimal>(type: "decimal(9,5)", precision: 9, scale: 5, nullable: false),
                IsSectorRate = table.Column<decimal>(type: "decimal(9,5)", precision: 9, scale: 5, nullable: false),
                MinTaxRate = table.Column<decimal>(type: "decimal(9,5)", precision: 9, scale: 5, nullable: false),
                MinTaxReducedRate = table.Column<decimal>(type: "decimal(9,5)", precision: 9, scale: 5, nullable: false),
                MinTaxFloorTnd = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                CssApplies = table.Column<bool>(type: "bit", nullable: false),
                CssRate = table.Column<decimal>(type: "decimal(9,5)", precision: 9, scale: 5, nullable: false),
                CssFloorTnd = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                AcompteRate = table.Column<decimal>(type: "decimal(9,5)", precision: 9, scale: 5, nullable: false),
                AcompteCount = table.Column<int>(type: "int", nullable: false),
                DeficitCarryForwardYears = table.Column<int>(type: "int", nullable: false),
                IrppBracketsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IncomeTaxYearParameters", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "FiscalResultDeclarations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FiscalYear = table.Column<int>(type: "int", nullable: false),
                TaxpayerKind = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                AccountingResult = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                AppliedIsRate = table.Column<decimal>(type: "decimal(9,5)", precision: 9, scale: 5, nullable: false),
                LocalTurnoverTtc = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                AcomptesPaid = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                WithholdingSuffered = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                PriorTaxCredit = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                FinalizedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                FinalizedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                Version = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FiscalResultDeclarations", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "FiscalAdjustmentLines",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FiscalResultDeclarationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Kind = table.Column<int>(type: "int", nullable: false),
                CatalogCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                Label = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                IsAutoSuggested = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FiscalAdjustmentLines", x => x.Id);
                table.ForeignKey(
                    name: "FK_FiscalAdjustmentLines_FiscalResultDeclarations_FiscalResultDeclarationId",
                    column: x => x.FiscalResultDeclarationId,
                    principalTable: "FiscalResultDeclarations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "FiscalCarryForwardItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FiscalResultDeclarationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Kind = table.Column<int>(type: "int", nullable: false),
                OriginYear = table.Column<int>(type: "int", nullable: false),
                InitialAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                ImputedThisYear = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                ExpiryYear = table.Column<int>(type: "int", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FiscalCarryForwardItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_FiscalCarryForwardItems_FiscalResultDeclarations_FiscalResultDeclarationId",
                    column: x => x.FiscalResultDeclarationId,
                    principalTable: "FiscalResultDeclarations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_IncomeTaxYearParameters_FiscalYear",
            table: "IncomeTaxYearParameters",
            column: "FiscalYear",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_FiscalResultDeclarations_FiscalYear",
            table: "FiscalResultDeclarations",
            column: "FiscalYear",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_FiscalAdjustmentLines_FiscalResultDeclarationId",
            table: "FiscalAdjustmentLines",
            column: "FiscalResultDeclarationId");

        migrationBuilder.CreateIndex(
            name: "IX_FiscalCarryForwardItems_FiscalResultDeclarationId",
            table: "FiscalCarryForwardItems",
            column: "FiscalResultDeclarationId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FiscalAdjustmentLines");
        migrationBuilder.DropTable(name: "FiscalCarryForwardItems");
        migrationBuilder.DropTable(name: "FiscalResultDeclarations");
        migrationBuilder.DropTable(name: "IncomeTaxYearParameters");
    }
}
