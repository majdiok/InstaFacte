using System;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Vague 1, lot 5 tranche 5B — paliers quantitatifs des grilles tarifaires.
    ///
    /// Une table nouvelle uniquement, donc aucun risque sur l'existant : les prix déjà en place
    /// restent des prix de base sans palier, et se résolvent exactement comme avant.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260730160000_AddPriceListItemTiers_Tenant")]
    public partial class AddPriceListItemTiers_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PriceListItemTiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PriceListItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MinQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPriceHT = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPriceHTCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceListItemTiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceListItemTiers_PriceListItems_PriceListItemId",
                        column: x => x.PriceListItemId,
                        principalTable: "PriceListItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PriceListItemTiers_PriceListItemId_MinQuantity",
                table: "PriceListItemTiers",
                columns: new[] { "PriceListItemId", "MinQuantity" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PriceListItemTiers");
        }
    }
}
