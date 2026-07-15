using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddAccountingModule_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountingPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChartOfAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    AccountClass = table.Column<int>(type: "int", nullable: false),
                    ParentAccountNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    NatureType = table.Column<int>(type: "int", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartOfAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JournalEntrySequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    LastSequence = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntrySequences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LetteringGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    LetteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LetteringGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VatDeclarations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    CollectedVat19 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CollectedVat19Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CollectedVat13 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CollectedVat13Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CollectedVat7 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CollectedVat7Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DeductibleVatGoods = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DeductibleVatGoodsCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DeductibleVatAssets = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DeductibleVatAssetsCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PreviousCredit = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    PreviousCreditCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    VatDue = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    VatDueCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CreditToCarry = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CreditToCarryCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VatDeclarations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JournalEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryNumber = table.Column<int>(type: "int", nullable: false),
                    JournalCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SourceEntityType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    SourceEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsAutoGenerated = table.Column<bool>(type: "bit", nullable: false),
                    AccountingPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsReversed = table.Column<bool>(type: "bit", nullable: false),
                    ReversedByEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversesEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalEntries_AccountingPeriods_AccountingPeriodId",
                        column: x => x.AccountingPeriodId,
                        principalTable: "AccountingPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LetteringGroupMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LetteringGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalEntryLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LetteringGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LetteringGroupMembers_LetteringGroups_LetteringGroupId",
                        column: x => x.LetteringGroupId,
                        principalTable: "LetteringGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JournalEntryLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DebitAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DebitCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CreditCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    LetteringCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    ThirdPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ThirdPartyKind = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntryLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalEntryLines_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingPeriods_FiscalYear_Month",
                table: "AccountingPeriods",
                columns: new[] { "FiscalYear", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChartOfAccounts_AccountNumber",
                table: "ChartOfAccounts",
                column: "AccountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_AccountingPeriodId",
                table: "JournalEntries",
                column: "AccountingPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_JournalCode_EntryNumber_EntryDate",
                table: "JournalEntries",
                columns: new[] { "JournalCode", "EntryNumber", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_SourceEntityType_SourceEntityId",
                table: "JournalEntries",
                columns: new[] { "SourceEntityType", "SourceEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLines_AccountNumber",
                table: "JournalEntryLines",
                column: "AccountNumber");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLines_JournalEntryId",
                table: "JournalEntryLines",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntrySequences_JournalCode_FiscalYear",
                table: "JournalEntrySequences",
                columns: new[] { "JournalCode", "FiscalYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LetteringGroupMembers_JournalEntryLineId",
                table: "LetteringGroupMembers",
                column: "JournalEntryLineId");

            migrationBuilder.CreateIndex(
                name: "IX_LetteringGroupMembers_LetteringGroupId",
                table: "LetteringGroupMembers",
                column: "LetteringGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_VatDeclarations_Year_Month",
                table: "VatDeclarations",
                columns: new[] { "Year", "Month" },
                unique: true);

            var t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            void Row(string id, string num, string label, int cls, string parent, int nature, int level)
            {
                migrationBuilder.InsertData(
                    table: "ChartOfAccounts",
                    columns: new[]
                    {
                        "Id", "AccountNumber", "Label", "AccountClass", "ParentAccountNumber", "NatureType", "IsSystem",
                        "IsActive", "Level", "CreatedAt", "UpdatedAt", "CreatedBy", "UpdatedBy"
                    },
                    values: new object[]
                    {
                        Guid.Parse(id), num, label, cls, parent, nature, true, true, level, t, null, "system", null
                    });
            }

            Row("11111111-1111-1111-1111-111111111001", "401", "Fournisseurs d'exploitation", 4, null, 1, 3);
            Row("11111111-1111-1111-1111-111111111002", "4011", "Fournisseurs - achats", 4, "401", 1, 4);
            Row("11111111-1111-1111-1111-111111111003", "411", "Clients", 4, null, 0, 3);
            Row("11111111-1111-1111-1111-111111111004", "4111", "Clients - ventes", 4, "411", 0, 4);
            // NatureType 0 = Débit (Actif), 1 = Crédit (Passif).
            // 4341 et 43667 sont des créances sur l'État (Actif) → nature Débit.
            Row("11111111-1111-1111-1111-111111111005", "4341", "Retenue à la source", 4, null, 0, 4);
            Row("11111111-1111-1111-1111-111111111006", "43651", "TVA à payer", 4, null, 1, 5);
            Row("11111111-1111-1111-1111-111111111007", "43666", "TVA déductible sur biens et services", 4, null, 0, 5);
            Row("11111111-1111-1111-1111-111111111008", "43662", "TVA déductible sur immobilisations", 4, null, 0, 5);
            Row("11111111-1111-1111-1111-111111111009", "43667", "Crédit de TVA à reporter", 4, null, 0, 5);
            Row("11111111-1111-1111-1111-111111111010", "43671", "TVA collectée", 4, null, 1, 5);
            Row("11111111-1111-1111-1111-111111111011", "436711", "TVA collectée sur débits", 4, "43671", 1, 6);
            Row("11111111-1111-1111-1111-111111111012", "436712", "TVA collectée sur encaissements", 4, "43671", 1, 6);
            Row("11111111-1111-1111-1111-111111111013", "4368", "Taxes sur le CA à régulariser", 4, null, 1, 4);
            Row("11111111-1111-1111-1111-111111111014", "532", "Banques", 5, null, 0, 3);
            Row("11111111-1111-1111-1111-111111111015", "5321", "Banques - comptes en dinars", 5, "532", 0, 4);
            Row("11111111-1111-1111-1111-111111111016", "5324", "Banques - comptes en devises", 5, "532", 0, 4);
            Row("11111111-1111-1111-1111-111111111017", "541", "Caisse siège social", 5, null, 0, 3);
            Row("11111111-1111-1111-1111-111111111018", "5411", "Caisse en dinars", 5, "541", 0, 4);
            Row("11111111-1111-1111-1111-111111111019", "607", "Achats de marchandises", 6, null, 0, 3);
            Row("11111111-1111-1111-1111-111111111020", "701", "Ventes de produits finis", 7, null, 1, 3);
            Row("11111111-1111-1111-1111-111111111021", "705", "Prestations de services", 7, null, 1, 3);
            Row("11111111-1111-1111-1111-111111111022", "707", "Ventes de marchandises", 7, null, 1, 3);
            Row("11111111-1111-1111-1111-111111111023", "6611", "Taxe de formation professionnelle (TFP)", 6, null, 0, 4);
            Row("11111111-1111-1111-1111-111111111024", "6612", "FOPROLOS", 6, null, 0, 4);
            Row("11111111-1111-1111-1111-111111111025", "6654", "Droits d'enregistrement et de timbre", 6, null, 0, 4);
            Row("11111111-1111-1111-1111-111111111026", "691", "Impôts sur les bénéfices", 6, null, 0, 3);
            Row("11111111-1111-1111-1111-111111111027", "45311", "CNSS", 4, null, 1, 5);
            Row("11111111-1111-1111-1111-111111111028", "101", "Capital social", 1, null, 1, 3);
            Row("11111111-1111-1111-1111-111111111029", "131", "Résultat bénéficiaire", 1, null, 1, 3);
            Row("11111111-1111-1111-1111-111111000030", "10", "Capital et réserves assimilées", 1, null, 1, 2);
            Row("11111111-1111-1111-1111-111111000031", "102", "Capital appelé non versé", 1, "10", 1, 3);
            Row("11111111-1111-1111-1111-111111000032", "105", "Primes liées au capital", 1, "10", 1, 3);
            Row("11111111-1111-1111-1111-111111000033", "106", "Écarts de réévaluation", 1, "10", 1, 3);
            Row("11111111-1111-1111-1111-111111000034", "108", "Comptes de l'exploitant", 1, "10", 1, 3);
            Row("11111111-1111-1111-1111-111111000035", "109", "Actionnaires : capital souscrit non appelé", 1, "10", 1, 3);
            Row("11111111-1111-1111-1111-111111000036", "11", "Réserves", 1, null, 1, 2);
            Row("11111111-1111-1111-1111-111111000037", "12", "Report à nouveau", 1, null, 1, 2);
            Row("11111111-1111-1111-1111-111111000038", "13", "Résultat net de l'exercice", 1, null, 1, 2);
            Row("11111111-1111-1111-1111-111111000039", "135", "Résultat net : perte", 1, "13", 1, 3);
            Row("11111111-1111-1111-1111-111111000040", "14", "Subventions d'investissement", 1, null, 1, 2);
            Row("11111111-1111-1111-1111-111111000041", "15", "Provisions réglementées", 1, null, 1, 2);
            Row("11111111-1111-1111-1111-111111000042", "16", "Emprunts et dettes assimilées", 1, null, 1, 2);
            Row("11111111-1111-1111-1111-111111000043", "17", "Dettes de crédit-bail et assimilées", 1, null, 1, 2);
            Row("11111111-1111-1111-1111-111111000044", "18", "Comptes de liaison des établissements", 1, null, 1, 2);
            Row("11111111-1111-1111-1111-111111000045", "19", "Provisions pour risques et charges", 1, null, 1, 2);
            Row("11111111-1111-1111-1111-111111000046", "20", "Immobilisations incorporelles", 2, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000047", "21", "Immobilisations corporelles", 2, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000048", "211", "Terrains", 2, "21", 0, 3);
            Row("11111111-1111-1111-1111-111111000049", "212", "Constructions", 2, "21", 0, 3);
            Row("11111111-1111-1111-1111-111111000050", "213", "Installations techniques", 2, "21", 0, 3);
            Row("11111111-1111-1111-1111-111111000051", "218", "Autres immobilisations corporelles", 2, "21", 0, 3);
            Row("11111111-1111-1111-1111-111111000052", "22", "Immobilisations mises en concession", 2, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000053", "23", "Immobilisations en cours", 2, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000054", "231", "Immobilisations corporelles en cours", 2, "23", 0, 3);
            Row("11111111-1111-1111-1111-111111000055", "232", "Immobilisations incorporelles en cours", 2, "23", 0, 3);
            Row("11111111-1111-1111-1111-111111000056", "233", "Immobilisations financières en cours", 2, "23", 0, 3);
            Row("11111111-1111-1111-1111-111111000057", "235", "Immobilisations incorporelles", 2, "23", 0, 3);
            Row("11111111-1111-1111-1111-111111000058", "2351", "Frais de développement", 2, "235", 0, 4);
            Row("11111111-1111-1111-1111-111111000059", "237", "Avances et acomptes sur immobilisations", 2, "23", 0, 3);
            Row("11111111-1111-1111-1111-111111000060", "238", "Avances et acomptes versés sur commandes", 2, "23", 0, 3);
            Row("11111111-1111-1111-1111-111111000061", "24", "Immobilisations financières", 2, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000062", "25", "Titres immobilisés", 2, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000063", "251", "Titres du portefeuille d'immobilisation", 2, "25", 0, 3);
            Row("11111111-1111-1111-1111-111111000064", "252", "Titres immobilisés de l'activité de portefeuille", 2, "25", 0, 3);
            Row("11111111-1111-1111-1111-111111000065", "258", "Titres immobilisés autres", 2, "25", 0, 3);
            Row("11111111-1111-1111-1111-111111000066", "26", "Participations et créances rattachées", 2, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000067", "27", "Autres immobilisations financières", 2, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000068", "28", "Amortissements des immobilisations", 2, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000069", "281", "Amortissements des immobilisations corporelles", 2, "28", 0, 3);
            Row("11111111-1111-1111-1111-111111000070", "29", "Provisions pour dépréciation des immobilisations", 2, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000071", "31", "Matières premières et fournitures", 3, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000072", "311", "Matières premières", 3, "31", 0, 3);
            Row("11111111-1111-1111-1111-111111000073", "312", "Matières et fournitures consommables", 3, "31", 0, 3);
            Row("11111111-1111-1111-1111-111111000074", "313", "Emballages", 3, "31", 0, 3);
            Row("11111111-1111-1111-1111-111111000075", "32", "Autres approvisionnements", 3, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000076", "33", "En-cours de production de biens", 3, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000077", "34", "En-cours de production de services", 3, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000078", "35", "Stocks de produits", 3, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000079", "36", "Stocks provenant d'immobilisations", 3, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000080", "37", "Stocks de marchandises", 3, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000081", "38", "Provisions pour dépréciation des stocks", 3, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000082", "39", "Provisions pour dépréciation des en-cours", 3, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000083", "40", "Fournisseurs et comptes rattachés", 4, null, 1, 2);
            Row("11111111-1111-1111-1111-111111000084", "403", "Fournisseurs - effets à payer", 4, "40", 1, 3);
            Row("11111111-1111-1111-1111-111111000085", "404", "Fournisseurs d'immobilisations", 4, "40", 1, 3);
            Row("11111111-1111-1111-1111-111111000086", "405", "Fournisseurs de biens et services", 4, "40", 1, 3);
            Row("11111111-1111-1111-1111-111111000087", "406", "Fournisseurs - factures non parvenues", 4, "40", 1, 3);
            Row("11111111-1111-1111-1111-111111000088", "407", "Fournisseurs - autres avoirs", 4, "40", 1, 3);
            Row("11111111-1111-1111-1111-111111000089", "408", "Fournisseurs - autres dettes", 4, "40", 1, 3);
            Row("11111111-1111-1111-1111-111111000090", "409", "Fournisseurs - rabais, remises, ristournes", 4, "40", 1, 3);
            Row("11111111-1111-1111-1111-111111000091", "41", "Clients et comptes rattachés", 4, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000092", "412", "Clients - effets à recevoir", 4, "41", 0, 3);
            Row("11111111-1111-1111-1111-111111000093", "413", "Clients - autres avoirs", 4, "41", 0, 3);
            Row("11111111-1111-1111-1111-111111000094", "414", "Clients - créances douteuses", 4, "41", 0, 3);
            Row("11111111-1111-1111-1111-111111000095", "415", "Clients - autres créances", 4, "41", 0, 3);
            Row("11111111-1111-1111-1111-111111000096", "416", "Clients - factures à établir", 4, "41", 0, 3);
            Row("11111111-1111-1111-1111-111111000097", "417", "Clients - produits à recevoir", 4, "41", 0, 3);
            Row("11111111-1111-1111-1111-111111000098", "418", "Clients - autres avoirs à recevoir", 4, "41", 0, 3);
            Row("11111111-1111-1111-1111-111111000099", "42", "Personnel et comptes rattachés", 4, null, 1, 2);
            Row("11111111-1111-1111-1111-111111000100", "43", "Sécurité sociale et autres organismes sociaux", 4, null, 1, 2);
            Row("11111111-1111-1111-1111-111111000101", "431", "Sécurité sociale", 4, "43", 1, 3);
            Row("11111111-1111-1111-1111-111111000102", "432", "Autres organismes sociaux", 4, "43", 1, 3);
            Row("11111111-1111-1111-1111-111111000103", "433", "Caisse de retraite", 4, "43", 1, 3);
            Row("11111111-1111-1111-1111-111111000104", "435", "Charges sociales à payer", 4, "43", 1, 3);
            Row("11111111-1111-1111-1111-111111000105", "437", "Autres charges sociales", 4, "43", 1, 3);
            Row("11111111-1111-1111-1111-111111000106", "438", "Organismes sociaux - autres", 4, "43", 1, 3);
            Row("11111111-1111-1111-1111-111111000107", "44", "État et autres collectivités publiques", 4, null, 1, 2);
            Row("11111111-1111-1111-1111-111111000108", "441", "État - subventions à recevoir", 4, "44", 1, 3);
            Row("11111111-1111-1111-1111-111111000109", "442", "État - impôts et taxes recouvrables", 4, "44", 1, 3);
            Row("11111111-1111-1111-1111-111111000110", "443", "État - TVA due", 4, "44", 1, 3);
            Row("11111111-1111-1111-1111-111111000111", "444", "État - autres impôts", 4, "44", 1, 3);
            Row("11111111-1111-1111-1111-111111000112", "445", "État - autres créances", 4, "44", 1, 3);
            Row("11111111-1111-1111-1111-111111000113", "446", "État - autres dettes", 4, "44", 1, 3);
            Row("11111111-1111-1111-1111-111111000114", "447", "État - autres comptes", 4, "44", 1, 3);
            Row("11111111-1111-1111-1111-111111000115", "448", "État - charges à payer", 4, "44", 1, 3);
            Row("11111111-1111-1111-1111-111111000116", "45", "Groupe et associés", 4, null, 1, 2);
            Row("11111111-1111-1111-1111-111111000117", "451", "Groupe - comptes courants", 4, "45", 1, 3);
            Row("11111111-1111-1111-1111-111111000118", "452", "Associés - comptes courants", 4, "45", 1, 3);
            Row("11111111-1111-1111-1111-111111000119", "455", "Associés - opérations courantes", 4, "45", 1, 3);
            Row("11111111-1111-1111-1111-111111000120", "456", "Associés - dividendes à payer", 4, "45", 1, 3);
            Row("11111111-1111-1111-1111-111111000121", "46", "Débiteurs et créditeurs divers", 4, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000122", "47", "Comptes de régularisation", 4, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000123", "48", "Comptes de répartition périodique des charges", 4, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000124", "49", "Provisions pour dépréciation des comptes de tiers", 4, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000125", "51", "Valeurs mobilières de placement", 5, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000126", "511", "Titres du portefeuille de placement", 5, "51", 0, 3);
            Row("11111111-1111-1111-1111-111111000127", "512", "Titres à court terme", 5, "51", 0, 3);
            Row("11111111-1111-1111-1111-111111000128", "52", "Instruments de trésorerie", 5, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000129", "53", "Banques, établissements financiers et caisses", 5, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000130", "531", "Caisse siège social", 5, "53", 0, 3);
            Row("11111111-1111-1111-1111-111111000131", "533", "Caisse succursales", 5, "53", 0, 3);
            Row("11111111-1111-1111-1111-111111000132", "534", "Régies d'avances et d'accréditifs", 5, "53", 0, 3);
            Row("11111111-1111-1111-1111-111111000133", "535", "Virements internes", 5, "53", 0, 3);
            Row("11111111-1111-1111-1111-111111000134", "536", "Chèques postaux", 5, "53", 0, 3);
            Row("11111111-1111-1111-1111-111111000135", "60", "Achats", 6, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000136", "601", "Achats stockés - Matières premières", 6, "60", 0, 3);
            Row("11111111-1111-1111-1111-111111000137", "602", "Achats stockés - Autres approvisionnements", 6, "60", 0, 3);
            Row("11111111-1111-1111-1111-111111000138", "603", "Variations des stocks", 6, "60", 0, 3);
            Row("11111111-1111-1111-1111-111111000139", "604", "Achats d'études et prestations de services", 6, "60", 0, 3);
            Row("11111111-1111-1111-1111-111111000140", "605", "Achats de matériel, équipements et travaux", 6, "60", 0, 3);
            Row("11111111-1111-1111-1111-111111000141", "606", "Achats non stockés de matières et fournitures", 6, "60", 0, 3);
            Row("11111111-1111-1111-1111-111111000142", "608", "Frais accessoires sur achats", 6, "60", 0, 3);
            Row("11111111-1111-1111-1111-111111000143", "609", "Rabais, remises et ristournes obtenus sur achats", 6, "60", 0, 3);
            Row("11111111-1111-1111-1111-111111000144", "61", "Services extérieurs", 6, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000145", "611", "Sous-traitance générale", 6, "61", 0, 3);
            Row("11111111-1111-1111-1111-111111000146", "612", "Redevances de crédit-bail", 6, "61", 0, 3);
            Row("11111111-1111-1111-1111-111111000147", "613", "Locations", 6, "61", 0, 3);
            Row("11111111-1111-1111-1111-111111000148", "614", "Charges locatives et de copropriété", 6, "61", 0, 3);
            Row("11111111-1111-1111-1111-111111000149", "615", "Entretien et réparations", 6, "61", 0, 3);
            Row("11111111-1111-1111-1111-111111000150", "616", "Primes d'assurances", 6, "61", 0, 3);
            Row("11111111-1111-1111-1111-111111000151", "617", "Études et recherches", 6, "61", 0, 3);
            Row("11111111-1111-1111-1111-111111000152", "618", "Divers", 6, "61", 0, 3);
            Row("11111111-1111-1111-1111-111111000153", "62", "Autres services extérieurs", 6, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000154", "63", "Impôts, taxes et versements assimilés", 6, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000155", "64", "Charges de personnel", 6, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000156", "651", "Redevances pour concessions, brevets, licences", 6, null, 0, 3);
            Row("11111111-1111-1111-1111-111111000157", "652", "Jetons de présence", 6, null, 0, 3);
            Row("11111111-1111-1111-1111-111111000158", "653", "Rémunérations d'intermédiaires et honoraires", 6, null, 0, 3);
            Row("11111111-1111-1111-1111-111111000159", "654", "Pertes sur créances irrécouvrables", 6, null, 0, 3);
            Row("11111111-1111-1111-1111-111111000160", "655", "Quotes-parts de résultat sur opérations faites en commun", 6, null, 0, 3);
            Row("11111111-1111-1111-1111-111111000161", "656", "Charges de personnel externalisé", 6, null, 0, 3);
            Row("11111111-1111-1111-1111-111111000162", "657", "Autres charges de personnel", 6, null, 0, 3);
            Row("11111111-1111-1111-1111-111111000163", "658", "Charges diverses de gestion courante", 6, null, 0, 3);
            Row("11111111-1111-1111-1111-111111000164", "659", "Charges exceptionnelles", 6, null, 0, 3);
            Row("11111111-1111-1111-1111-111111000165", "66", "Charges financières", 6, null, 0, 2);
            Row("11111111-1111-1111-1111-111111000166", "661", "Charges d'intérêts", 6, "66", 0, 3);
            Row("11111111-1111-1111-1111-111111000167", "6613", "Pertes de change", 6, "661", 0, 4);
            Row("11111111-1111-1111-1111-111111000168", "6614", "Escomptes accordés", 6, "661", 0, 4);
            Row("11111111-1111-1111-1111-111111000169", "6615", "Charges assimilées", 6, "661", 0, 4);
            Row("11111111-1111-1111-1111-111111000170", "662", "Pertes sur créances liées à des participations", 6, "66", 0, 3);
            Row("11111111-1111-1111-1111-111111000171", "663", "Pertes sur titres de placement", 6, "66", 0, 3);
            Row("11111111-1111-1111-1111-111111000172", "664", "Pertes sur instruments de trésorerie", 6, "66", 0, 3);
            Row("11111111-1111-1111-1111-111111000173", "665", "Charges exceptionnelles financières", 6, "66", 0, 3);
            Row("11111111-1111-1111-1111-111111000174", "6651", "Pénalités et amendes", 6, "665", 0, 4);
            Row("11111111-1111-1111-1111-111111000175", "6652", "Divers", 6, "665", 0, 4);
            Row("11111111-1111-1111-1111-111111000176", "6653", "Charges sur opérations de gestion", 6, "665", 0, 4);
            Row("11111111-1111-1111-1111-111111000177", "666", "Dotations aux amortissements financiers", 6, "66", 0, 3);
            Row("11111111-1111-1111-1111-111111000178", "667", "Dotations aux provisions financières", 6, "66", 0, 3);
            Row("11111111-1111-1111-1111-111111000179", "668", "Autres charges financières", 6, "66", 0, 3);
            Row("11111111-1111-1111-1111-111111000180", "669", "Charges financières de gestion courante", 6, "66", 0, 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChartOfAccounts");

            migrationBuilder.DropTable(
                name: "JournalEntryLines");

            migrationBuilder.DropTable(
                name: "JournalEntrySequences");

            migrationBuilder.DropTable(
                name: "LetteringGroupMembers");

            migrationBuilder.DropTable(
                name: "VatDeclarations");

            migrationBuilder.DropTable(
                name: "JournalEntries");

            migrationBuilder.DropTable(
                name: "LetteringGroups");

            migrationBuilder.DropTable(
                name: "AccountingPeriods");
        }
    }
}
