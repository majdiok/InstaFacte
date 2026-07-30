using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Vague 1, lot 6 — plafond d'encours et délai de règlement habituel du client.
    ///
    /// Strictement additive : deux colonnes nullables. Un client sans plafond n'est jamais en
    /// dépassement, et le plafond ne bloque rien de toute façon — il alimente une alerte.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260730220000_AddClientCreditTerms_Tenant")]
    public partial class AddClientCreditTerms_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CreditLimit",
                table: "Clients",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultPaymentTermDays",
                table: "Clients",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DefaultPaymentTermDays", table: "Clients");
            migrationBuilder.DropColumn(name: "CreditLimit", table: "Clients");
        }
    }
}
