using System;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Vague 1, lot 5 tranche 5A — tarification : grilles, prix négociés, affectation client.
    ///
    /// Trois tables nouvelles (donc aucun risque sur l'existant) et une colonne additive sur
    /// <c>Clients</c> :
    ///  - <c>PriceLists</c> / <c>PriceListItems</c> : grilles tarifaires et leurs prix par produit ;
    ///  - <c>ClientProductPrices</c> : prix négocié ponctuel qui prime sur toute grille ;
    ///  - <c>Clients.PriceListId</c> nullable : grille affectée au client (null = tarif catalogue).
    ///
    /// Les prix HT sont des <c>Money</c> possédés : montant en decimal(18,3) + devise nvarchar(3),
    /// convention du contexte. Le prix figé sur les documents émis n'est pas touché : ces tables ne
    /// font qu'alimenter le résolveur au moment de créer une ligne.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260730100000_AddPricing_Tenant")]
    public partial class AddPricing_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PriceLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValidUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriceListItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PriceListId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitPriceHT = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPriceHTCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceListItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceListItems_PriceLists_PriceListId",
                        column: x => x.PriceListId,
                        principalTable: "PriceLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientProductPrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitPriceHT = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPriceHTCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValidUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientProductPrices", x => x.Id);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "PriceListId",
                table: "Clients",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceLists_IsActive",
                table: "PriceLists",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PriceListItems_PriceListId_ProductId",
                table: "PriceListItems",
                columns: new[] { "PriceListId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientProductPrices_ClientId_ProductId",
                table: "ClientProductPrices",
                columns: new[] { "ClientId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_PriceListId",
                table: "Clients",
                column: "PriceListId",
                filter: "[PriceListId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Clients_PriceListId", table: "Clients");
            migrationBuilder.DropColumn(name: "PriceListId", table: "Clients");
            migrationBuilder.DropTable(name: "ClientProductPrices");
            migrationBuilder.DropTable(name: "PriceListItems");
            migrationBuilder.DropTable(name: "PriceLists");
        }
    }
}
