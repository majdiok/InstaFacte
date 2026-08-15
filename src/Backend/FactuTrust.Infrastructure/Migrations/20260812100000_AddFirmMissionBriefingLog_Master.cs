using System;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations
{
    /// <summary>
    /// Trace d'envoi du brief quotidien « Chef de mission » (agent IA cabinet).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Non-régression : table nouvelle, aucune donnée ni aucun code antérieur n'est touché. Elle
    /// reste vide tant que <c>Features:AccountingFirms:FirmAgentDailyBriefingEnabled</c> est faux.
    /// </para>
    /// <para>
    /// L'index unique (FirmTenantId, RecipientUserId, BriefingDate) EST l'anti-doublon du brief :
    /// un redéclenchement du job Hangfire échoue à l'insertion au lieu d'envoyer un second e-mail.
    /// </para>
    /// <para>
    /// Écrite à la main, comme les autres migrations cabinet de ce dépôt
    /// (<c>AddFirmLeavePayrollMirror_Master</c>, <c>AddFirmProductiveHoursMode_Master</c>) : le
    /// <c>MasterDbContextModelSnapshot</c> n'y est volontairement pas régénéré. Le laisser faire
    /// par <c>dotnet ef migrations add</c> rejouerait les colonnes de ces migrations-là, absentes
    /// du snapshot mais bien présentes en base.
    /// </para>
    /// </remarks>
    [DbContext(typeof(MasterDbContext))]
    [Migration("20260812100000_AddFirmMissionBriefingLog_Master")]
    public partial class AddFirmMissionBriefingLog_Master : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FirmMissionBriefingLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirmTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BriefingDate = table.Column<DateTime>(type: "date", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    OverdueCount = table.Column<int>(type: "int", nullable: false),
                    PartialRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmMissionBriefingLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FirmMissionBriefingLogs_FirmTenantId_RecipientUserId_BriefingDate",
                table: "FirmMissionBriefingLogs",
                columns: new[] { "FirmTenantId", "RecipientUserId", "BriefingDate" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "FirmMissionBriefingLogs");
        }
    }
}
