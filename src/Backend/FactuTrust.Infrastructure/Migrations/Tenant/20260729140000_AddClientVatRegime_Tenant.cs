using System;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Vague 1, lot 7 — régime de TVA du client et attestation de suspension.
    ///
    /// Jusqu'ici exonération, suspension et export étaient tous amalgamés dans « 0 % » :
    /// impossible de distinguer un client exportateur d'un client exonéré, ni de justifier une
    /// suspension (art. 11 du code de la TVA) en contrôle. Le régime devient un attribut du
    /// CLIENT — le taux de ligne (VatRate) reste inchangé.
    ///
    /// Migration strictement additive :
    ///  - VatRegime int NOT NULL default 0 (Normal) — les clients existants gardent exactement
    ///    leur comportement de facturation ;
    ///  - trois colonnes nullables pour l'attestation de suspension (type possédé) ;
    ///  - index filtré écartant les assujettis ordinaires (l'immense majorité).
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260729140000_AddClientVatRegime_Tenant")]
    public partial class AddClientVatRegime_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VatRegime",
                table: "Clients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "VatExemptionCertificateNumber",
                table: "Clients",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VatExemptionValidFrom",
                table: "Clients",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VatExemptionValidUntil",
                table: "Clients",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_VatRegime",
                table: "Clients",
                column: "VatRegime",
                filter: "[VatRegime] <> 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Clients_VatRegime", table: "Clients");
            migrationBuilder.DropColumn(name: "VatExemptionValidUntil", table: "Clients");
            migrationBuilder.DropColumn(name: "VatExemptionValidFrom", table: "Clients");
            migrationBuilder.DropColumn(name: "VatExemptionCertificateNumber", table: "Clients");
            migrationBuilder.DropColumn(name: "VatRegime", table: "Clients");
        }
    }
}
