using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddMonthlyDeclarationV2_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Acomptes",
                table: "VatDeclarations",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DroitTimbre",
                table: "VatDeclarations",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Fodec",
                table: "VatDeclarations",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Foprolos",
                table: "VatDeclarations",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsRectificative",
                table: "VatDeclarations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RevisionNumber",
                table: "VatDeclarations",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "Tcl",
                table: "VatDeclarations",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Tfp",
                table: "VatDeclarations",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "WithholdingTax",
                table: "VatDeclarations",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Acomptes",
                table: "VatDeclarations");

            migrationBuilder.DropColumn(
                name: "DroitTimbre",
                table: "VatDeclarations");

            migrationBuilder.DropColumn(
                name: "Fodec",
                table: "VatDeclarations");

            migrationBuilder.DropColumn(
                name: "Foprolos",
                table: "VatDeclarations");

            migrationBuilder.DropColumn(
                name: "IsRectificative",
                table: "VatDeclarations");

            migrationBuilder.DropColumn(
                name: "RevisionNumber",
                table: "VatDeclarations");

            migrationBuilder.DropColumn(
                name: "Tcl",
                table: "VatDeclarations");

            migrationBuilder.DropColumn(
                name: "Tfp",
                table: "VatDeclarations");

            migrationBuilder.DropColumn(
                name: "WithholdingTax",
                table: "VatDeclarations");
        }
    }
}
