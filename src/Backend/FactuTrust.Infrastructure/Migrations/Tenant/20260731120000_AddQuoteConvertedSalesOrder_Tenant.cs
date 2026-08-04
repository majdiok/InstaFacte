using System;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

/// <summary>
/// Verrouillage du devis transformé en commande client (<c>Quote.ConvertedSalesOrderId</c>),
/// miroir exact de <c>ConvertedInvoiceId</c> : un devis accepté ne peut donner lieu qu'à UN
/// seul document de destination — facture OU commande — pour empêcher toute double
/// facturation par conversion concurrente.
/// <para>
/// Migration STRICTEMENT ADDITIVE : une colonne nullable et un index unique filtré, aucune
/// modification d'une table existante (aucun AlterTable / DropColumn). Écrite à la main comme
/// les migrations tenant récentes — le snapshot du modèle étant désynchronisé,
/// <c>dotnet ef migrations add</c> ne doit PAS être utilisé (il tenterait de recréer les
/// tables absentes du snapshot).
/// </para>
/// <para>
/// La colonne est nullable et l'unicité est filtrée sur les valeurs non nulles : les devis
/// existants restent donc rigoureusement inchangés.
/// </para>
/// </summary>
[DbContext(typeof(TenantDbContext))]
[Migration("20260731120000_AddQuoteConvertedSalesOrder_Tenant")]
public partial class AddQuoteConvertedSalesOrder_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ConvertedSalesOrderId",
            table: "Quotes",
            type: "uniqueidentifier",
            nullable: true);

        // Miroir de IX_Quotes_ConvertedInvoiceId : l'index unique filtré est le dernier
        // rempart contre une double conversion du même devis en commande client.
        migrationBuilder.CreateIndex(
            name: "IX_Quotes_ConvertedSalesOrderId",
            table: "Quotes",
            column: "ConvertedSalesOrderId",
            unique: true,
            filter: "[ConvertedSalesOrderId] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Quotes_ConvertedSalesOrderId", table: "Quotes");
        migrationBuilder.DropColumn(name: "ConvertedSalesOrderId", table: "Quotes");
    }
}
