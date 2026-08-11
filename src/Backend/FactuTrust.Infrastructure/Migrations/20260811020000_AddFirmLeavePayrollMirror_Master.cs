using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations
{
    /// <summary>
    /// Rattache les congés du cabinet à la paie : mapping par type et suivi du report par demande.
    /// </summary>
    /// <remarks>
    /// Non-régression : toutes les colonnes ajoutées sont nulles ou fausses par défaut, donc aucun
    /// congé existant n'est réputé reporté et aucun type ne produit d'effet paie tant que le
    /// mapping n'est pas posé. Le backfill ne vise que les sept types système, reconnus par leur
    /// code : un type créé par un cabinet garde un impact indéterminé, qu'il devra trancher.
    /// </remarks>
    [DbContext(typeof(MasterDbContext))]
    [Migration("20260811020000_AddFirmLeavePayrollMirror_Master")]
    public partial class AddFirmLeavePayrollMirror_Master : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PayrollLeaveType",
                table: "FirmLeaveTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CountsAsAbsence",
                table: "FirmLeaveTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PayrollLeaveRequestId",
                table: "FirmLeaveRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayrollMirrorState",
                table: "FirmLeaveRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PayrollMirrorMessage",
                table: "FirmLeaveRequests",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PayrollMirroredAt",
                table: "FirmLeaveRequests",
                type: "datetime2",
                nullable: true);

            // Traduction des types système vers LeaveType (Paid=0, Unpaid=1, Sick=2, Recovery=7, Other=99).
            // Formation et télétravail restent sans effet : l'une est déjà couverte par le taux de
            // productivité de l'exercice, l'autre est du temps travaillé.
            migrationBuilder.Sql(@"
UPDATE [FirmLeaveTypes] SET [PayrollLeaveType] = 0,  [CountsAsAbsence] = 1 WHERE [Code] = 'PAID'   AND [IsSystem] = 1;
UPDATE [FirmLeaveTypes] SET [PayrollLeaveType] = 7,  [CountsAsAbsence] = 1 WHERE [Code] = 'RTT'    AND [IsSystem] = 1;
UPDATE [FirmLeaveTypes] SET [PayrollLeaveType] = 2,  [CountsAsAbsence] = 1 WHERE [Code] = 'SICK'   AND [IsSystem] = 1;
UPDATE [FirmLeaveTypes] SET [PayrollLeaveType] = 1,  [CountsAsAbsence] = 1 WHERE [Code] = 'UNPAID' AND [IsSystem] = 1;
UPDATE [FirmLeaveTypes] SET [PayrollLeaveType] = 99, [CountsAsAbsence] = 0 WHERE [Code] = 'OTHER'  AND [IsSystem] = 1;
");

            migrationBuilder.CreateIndex(
                name: "IX_FirmLeaveRequests_FirmTenantId_PayrollMirrorState",
                table: "FirmLeaveRequests",
                columns: new[] { "FirmTenantId", "PayrollMirrorState" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FirmLeaveRequests_FirmTenantId_PayrollMirrorState",
                table: "FirmLeaveRequests");

            migrationBuilder.DropColumn(name: "PayrollMirroredAt", table: "FirmLeaveRequests");
            migrationBuilder.DropColumn(name: "PayrollMirrorMessage", table: "FirmLeaveRequests");
            migrationBuilder.DropColumn(name: "PayrollMirrorState", table: "FirmLeaveRequests");
            migrationBuilder.DropColumn(name: "PayrollLeaveRequestId", table: "FirmLeaveRequests");
            migrationBuilder.DropColumn(name: "CountsAsAbsence", table: "FirmLeaveTypes");
            migrationBuilder.DropColumn(name: "PayrollLeaveType", table: "FirmLeaveTypes");
        }
    }
}
