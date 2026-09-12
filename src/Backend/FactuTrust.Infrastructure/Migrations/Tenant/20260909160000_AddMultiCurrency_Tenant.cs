using System;

using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Socle multi-devises : catalogue des devises, table des taux de change, et devise de
    /// transaction sur les écritures.
    ///
    /// <para>
    /// <b>Strictement additif.</b> Les montants comptables continuent d'être portés par
    /// <c>DebitAmount</c> / <c>CreditAmount</c> en devise de tenue, et les colonnes existantes
    /// <c>DebitCurrency</c> / <c>CreditCurrency</c> conservent leur valeur <c>'TND'</c>. Aucune
    /// reprise de données : les colonnes ajoutées prennent une valeur par défaut qui décrit
    /// exactement l'existant (écriture en devise de tenue, taux 1, aucun montant en devise).
    /// </para>
    ///
    /// <para>
    /// <c>DebitAmountInCurrency</c> / <c>CreditAmountInCurrency</c> ne sont significatives que
    /// lorsque <c>JournalEntries.CurrencyCode</c> diffère de la devise de tenue. Elles restent donc
    /// à 0 sur tout l'historique, sans qu'aucun <c>UPDATE</c> de masse ne soit nécessaire.
    /// </para>
    ///
    /// <para>
    /// La devise de tenue est semée ici (TND) : l'invariant « exactement une devise fonctionnelle »
    /// est porté par un index unique filtré, il doit donc être satisfait dès la migration.
    /// </para>
    ///
    /// Migration manuelle : le snapshot EF de <c>TenantDbContext</c> est désynchronisé de
    /// l'historique (cf. <c>docs/backend-tenant-migrations.md</c>) ; <c>dotnet ef migrations add</c>
    /// embarquerait l'arriéré des autres modules.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260909160000_AddMultiCurrency_Tenant")]
    public partial class AddMultiCurrency_Tenant : Migration
    {
        private static readonly Guid FunctionalCurrencyId = new("b7d1f3a2-6c4e-4a19-9f52-3d8e1c0b7a41");

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DecimalPlaces = table.Column<int>(type: "int", nullable: false),
                    RatePeriodicity = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsFunctional = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CurrencyExchangeRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: true),
                    Rate = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrencyExchangeRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurrencyExchangeRates_Currencies_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Devise de transaction de l'écriture. La valeur par défaut décrit l'existant : toutes
            // les écritures déjà comptabilisées sont en devise de tenue, au taux 1.
            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "JournalEntries",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "TND");

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "JournalEntries",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<bool>(
                name: "ExchangeRateOverridden",
                table: "JournalEntries",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "DebitAmountInCurrency",
                table: "JournalEntryLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditAmountInCurrency",
                table: "JournalEntryLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            // Amorçage de la devise de tenue AVANT l'index unique filtré qui en garantit l'unicité.
            //
            // INSERT en SQL brut et non migrationBuilder.InsertData : cette dernière résout les types
            // de colonnes depuis le snapshot EF, que la convention de migrations manuelles de ce
            // dossier ne régénère pas (cf. docs/backend-tenant-migrations.md). Currencies n'y figure
            // donc pas, et InsertData échouerait avec « There is no entity type mapped to the table
            // 'Currencies' ». Le garde NOT EXISTS rend en outre l'opération rejouable.
            migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM dbo.Currencies WHERE IsFunctional = 1)
BEGIN
    INSERT INTO dbo.Currencies (Id, Code, Label, DecimalPlaces, RatePeriodicity, IsActive, IsFunctional, CreatedAt)
    VALUES ('{FunctionalCurrencyId}', N'TND', N'Dinar Tunisien', 3, 0, 1, 1, SYSUTCDATETIME());
END");

            // Index créés dans des batches distincts des ALTER TABLE ci-dessus : SQL Server refuse
            // de référencer dans le même batch une colonne ajoutée par ce batch.
            migrationBuilder.CreateIndex(
                name: "IX_Currencies_Code",
                table: "Currencies",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_IsFunctional",
                table: "Currencies",
                column: "IsFunctional",
                unique: true,
                filter: "[IsFunctional] = 1");

            // Month est NULL pour un taux fixe couvrant tout l'exercice. SQL Server traite les NULL
            // comme distincts dans un index unique : l'unicité du taux fixe exige donc son propre
            // index filtré.
            migrationBuilder.CreateIndex(
                name: "IX_CurrencyExchangeRates_Currency_Year_Month",
                table: "CurrencyExchangeRates",
                columns: new[] { "CurrencyId", "FiscalYear", "Month" },
                unique: true,
                filter: "[Month] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyExchangeRates_Currency_Year_Fixed",
                table: "CurrencyExchangeRates",
                columns: new[] { "CurrencyId", "FiscalYear" },
                unique: true,
                filter: "[Month] IS NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CurrencyExchangeRates_Currency_Year_Fixed",
                table: "CurrencyExchangeRates");

            migrationBuilder.DropIndex(
                name: "IX_CurrencyExchangeRates_Currency_Year_Month",
                table: "CurrencyExchangeRates");

            migrationBuilder.DropIndex(
                name: "IX_Currencies_IsFunctional",
                table: "Currencies");

            migrationBuilder.DropIndex(
                name: "IX_Currencies_Code",
                table: "Currencies");

            migrationBuilder.DropColumn(name: "CreditAmountInCurrency", table: "JournalEntryLines");
            migrationBuilder.DropColumn(name: "DebitAmountInCurrency", table: "JournalEntryLines");
            migrationBuilder.DropColumn(name: "ExchangeRateOverridden", table: "JournalEntries");
            migrationBuilder.DropColumn(name: "ExchangeRate", table: "JournalEntries");
            migrationBuilder.DropColumn(name: "CurrencyCode", table: "JournalEntries");

            migrationBuilder.DropTable(name: "CurrencyExchangeRates");
            migrationBuilder.DropTable(name: "Currencies");
        }
    }
}
