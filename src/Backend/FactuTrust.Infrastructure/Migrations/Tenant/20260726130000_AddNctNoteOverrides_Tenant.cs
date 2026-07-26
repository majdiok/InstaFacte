using System;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

/// <summary>
/// Personnalisation des notes annexes NCT par exercice (titre, texte narratif, masquage).
/// <para>
/// Migration STRICTEMENT ADDITIVE : une table neuve, aucune modification d'une table existante
/// (aucun AlterTable / AddColumn / DropColumn). Écrite à la main comme les migrations tenant
/// récentes — le snapshot du modèle étant désynchronisé, <c>dotnet ef migrations add</c> ne doit
/// PAS être utilisé (il tenterait de recréer les tables absentes du snapshot).
/// </para>
/// <para>
/// Sans aucune ligne dans cette table, la liasse NCT reste rigoureusement identique : la
/// superposition est neutre par construction.
/// </para>
/// </summary>
[DbContext(typeof(TenantDbContext))]
[Migration("20260726130000_AddNctNoteOverrides_Tenant")]
public partial class AddNctNoteOverrides_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "NctNoteOverrides",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FiscalYear = table.Column<int>(type: "int", nullable: false),
                NoteNumber = table.Column<int>(type: "int", nullable: false),
                CustomTitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                CustomDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                IsHidden = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NctNoteOverrides", x => x.Id);
            });

        // Clé métier : une seule personnalisation par note et par exercice.
        migrationBuilder.CreateIndex(
            name: "IX_NctNoteOverrides_FiscalYear_NoteNumber",
            table: "NctNoteOverrides",
            columns: new[] { "FiscalYear", "NoteNumber" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "NctNoteOverrides");
    }
}
