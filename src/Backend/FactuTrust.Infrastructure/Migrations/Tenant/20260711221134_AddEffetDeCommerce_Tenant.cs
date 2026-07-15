using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddEffetDeCommerce_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EffetDueDate",
                table: "SupplierPayments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EffetSettledAt",
                table: "SupplierPayments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EffetStatus",
                table: "SupplierPayments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "SupplierInvoices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EffetDueDate",
                table: "Payments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EffetSettledAt",
                table: "Payments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EffetStatus",
                table: "Payments",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EffetDueDate",
                table: "SupplierPayments");

            migrationBuilder.DropColumn(
                name: "EffetSettledAt",
                table: "SupplierPayments");

            migrationBuilder.DropColumn(
                name: "EffetStatus",
                table: "SupplierPayments");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "SupplierInvoices");

            migrationBuilder.DropColumn(
                name: "EffetDueDate",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "EffetSettledAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "EffetStatus",
                table: "Payments");
        }
    }
}
