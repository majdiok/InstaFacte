using System;

using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Journal des correspondances de la renumérotation des comptes de plus de 8 chiffres
    /// (<c>ChartAccountDigitCompactionService</c>).
    ///
    /// <para>
    /// La table n'est <b>pas</b> une condition de garde « déjà appliqué » : la compaction est gardée
    /// par la donnée elle-même (aucun compte non conforme ⇒ aucune action). Elle sert à trois
    /// choses : répondre à « où est passé le compte 4259655554 ? » indéfiniment, garantir
    /// l'injectivité <b>entre passes</b> grâce aux deux index uniques, et fournir la carte inverse
    /// qui rend la renumérotation réversible — contrairement au remap NCT 01, dont les cycles
    /// 421 ↔ 425 imposent une restauration.
    /// </para>
    ///
    /// <para>
    /// Aucun <c>DbSet</c> ni configuration <c>ModelBuilder</c>, comme pour
    /// <c>ChartOfAccountRemapLogs</c> : la table n'est lue et écrite qu'en SQL brut, et l'ajouter au
    /// modèle imposerait de régénérer le snapshot, ce que la convention de migrations manuelle de ce
    /// dossier cherche précisément à éviter.
    /// </para>
    ///
    /// Migration manuelle : le snapshot EF de <c>TenantDbContext</c> comporte des écarts antérieurs
    /// à cette tâche ; <c>dotnet ef migrations add</c> aurait embarqué ces changements non liés.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260903090000_AddChartOfAccountCompactionLogs_Tenant")]
    public partial class AddChartOfAccountCompactionLogs_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChartOfAccountCompactionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MapVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FromAccountNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ToAccountNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RootAccountNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DigitsBefore = table.Column<int>(type: "int", nullable: false),
                    EmployeeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AppliedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartOfAccountCompactionLogs", x => x.Id);
                });

            // Deux salariés ne peuvent jamais aboutir au même compte, ni un ancien numéro être
            // réattribué : les contraintes tranchent avant qu'aucune donnée comptable ne bouge.
            migrationBuilder.CreateIndex(
                name: "IX_ChartOfAccountCompactionLogs_From",
                table: "ChartOfAccountCompactionLogs",
                column: "FromAccountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChartOfAccountCompactionLogs_To",
                table: "ChartOfAccountCompactionLogs",
                column: "ToAccountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChartOfAccountCompactionLogs_Batch",
                table: "ChartOfAccountCompactionLogs",
                column: "BatchId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ChartOfAccountCompactionLogs");
        }
    }
}
