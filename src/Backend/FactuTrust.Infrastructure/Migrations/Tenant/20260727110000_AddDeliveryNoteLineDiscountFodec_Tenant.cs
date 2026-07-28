using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Vague 0 — correctif 3 : remise et FODEC sur les lignes de bon de livraison.
    ///
    /// La ligne de BL ne portait ni remise ni FODEC : la facture générée depuis le BL naissait
    /// donc sans la remise négociée à la livraison, et les totaux du bon remis au client
    /// sous-estimaient la facture à venir.
    ///
    /// Seuls les PARAMÈTRES sont persistés (remise en %, assujettissement FODEC, taux FODEC) ;
    /// les montants restent des propriétés dérivées, comme le reste de DeliveryNoteLine.
    ///
    /// Migration strictement additive : DiscountPercent naît NULL et IsFodecApplicable naît
    /// false, donc tous les bons de livraison existants conservent des totaux inchangés.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260727110000_AddDeliveryNoteLineDiscountFodec_Tenant")]
    public partial class AddDeliveryNoteLineDiscountFodec_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                table: "DeliveryNoteLines",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFodecApplicable",
                table: "DeliveryNoteLines",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "FodecRatePercent",
                table: "DeliveryNoteLines",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 1.0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "DeliveryNoteLines");

            migrationBuilder.DropColumn(
                name: "IsFodecApplicable",
                table: "DeliveryNoteLines");

            migrationBuilder.DropColumn(
                name: "FodecRatePercent",
                table: "DeliveryNoteLines");
        }
    }
}
