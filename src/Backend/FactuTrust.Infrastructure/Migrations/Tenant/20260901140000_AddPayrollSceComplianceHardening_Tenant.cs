using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Durcissement de conformité SCE de la paie (Phase-3 §3.3 + Phase-4 WS-4). Migration additive et
    /// rétro-compatible : toutes les nouvelles colonnes sont non-nullables avec une valeur par défaut
    /// usine, de sorte que les tenants/exercices existants conservent le comportement historique.
    ///
    /// Colonnes ajoutées :
    /// <list type="bullet">
    /// <item><c>PayrollYearParameters.PayrollTaxBaseMode</c> (int, défaut 0 = Legacy) — R-24 :
    /// sélectionne l'assiette des taxes patronales (TFP/FOPROLOS). Legacy conserve l'assiette
    /// CNSSable historique ; TotalGros applique l'assiette légale (rémunération brute totale). Les
    /// exercices persistés gardent Legacy (défaut colonne).</item>
    /// <item><c>Payslips.SmigAnnualDeductionAmount</c> (decimal(18,3), défaut 0) — R-12 : montant de
    /// la déduction annuelle 500 TND (SMIG/SMAG) effectivement appliqué sur le mois, tracé sur le
    /// bulletin pour audit et réconciliation IRPP annuelle.</item>
    /// <item><c>Payslips.HasPartialDeductions</c> (bit, défaut 0) — R-22 : signale qu'au moins une
    /// ligne de retenue a été réduite/évitée faute de budget disponible.</item>
    /// <item><c>Payslips.PartialDeductionCarryOver</c> (decimal(18,3), défaut 0) — R-22 : report
    /// cumulé des retenues partielles (avances/salaire) à reprendre sur un cycle ultérieur.</item>
    /// <item><c>EmployeeAdvances.SettledAmount</c> (decimal(18,3), défaut 0) — R-22 : montant
    /// effectivement imputé lors d'un règlement partiel d'avance (le solde restant est reporté).</item>
    /// </list>
    ///
    /// Index ajoutés :
    /// <list type="bullet">
    /// <item><c>IX_Payslips_PayrollRunId_EmployeeId</c> UNIQUE — R-16/M2 : un bulletin au plus par
    /// (cycle, salarié) ; garantit qu'un recalcul concurrent ne duplique pas les bulletins.</item>
    /// <item><c>IX_JournalEntries_SourceEntityType_SourceEntityId</c> UNIQUE FILTRÉ — R-16/M2 :
    /// remplace l'index non unique existant (créé en <c>AddAccountingModule</c>). Garantit une seule
    /// écriture sourcing ACTIVE par entité source, limitée aux types de comptabilisation « aller »
    /// de la paie (<c>PayrollRun</c>, <c>PayrollPayment</c>, <c>CnssContributionPayment</c>). Le
    /// filtre exclut les écritures extournées (<c>IsReversed = 1</c>) ET les écritures d'annulation
    /// (<c>*Cancelled</c>) : celles-ci s'accumulent légitimement sur un cycle rouvert plusieurs
    /// fois (chaque réouverture ajoute une extourne <c>PayrollRunCancelled</c> IsReversed = 0 pour
    /// le même runId), donc les exclure préserve le cycle reopen→revalidation.</item>
    /// </list>
    ///
    /// <c>PayrollRuns.Version</c> est marqué <c>IsConcurrencyToken</c> (R-16/M2) : annotation de
    /// modèle uniquement (la colonne <c>Version</c> int existe déjà via <c>AggregateRoot</c>) — aucune
    /// modification de schéma n'est requise, le jeton se traduit par une clause <c>WHERE Version</c>
    /// sur les <c>UPDATE</c>/<c>DELETE</c>.
    ///
    /// Migration manuelle (colonnes + index ciblés) : le snapshot EF de <c>TenantDbContext</c> comporte
    /// déjà, pour d'autres tables, des colonnes hors-modèle antérieures à cette tâche ; générer via
    /// <c>dotnet ef migrations add</c> aurait entraîné ces changements non liés. Les blocs d'entité
    /// correspondants du snapshot sont mis à jour en conséquence.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260901140000_AddPayrollSceComplianceHardening_Tenant")]
    public partial class AddPayrollSceComplianceHardening_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // R-24 : assiette des taxes patronales (Legacy = assiette CNSSable historique).
            migrationBuilder.AddColumn<int>(
                name: "PayrollTaxBaseMode",
                table: "PayrollYearParameters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // R-12 : déduction annuelle 500 TND (SMIG/SMAG) appliquée sur le mois, tracée sur le bulletin.
            migrationBuilder.AddColumn<decimal>(
                name: "SmigAnnualDeductionAmount",
                table: "Payslips",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            // R-22 : signalement de retenue partielle + report cumulé.
            migrationBuilder.AddColumn<bool>(
                name: "HasPartialDeductions",
                table: "Payslips",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PartialDeductionCarryOver",
                table: "Payslips",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            // R-22 : imputation partielle d'avance (solde reporté).
            migrationBuilder.AddColumn<decimal>(
                name: "SettledAmount",
                table: "EmployeeAdvances",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            // R-16/M2 : un bulletin au plus par (cycle, salarié).
            migrationBuilder.CreateIndex(
                name: "IX_Payslips_PayrollRunId_EmployeeId",
                table: "Payslips",
                columns: new[] { "PayrollRunId", "EmployeeId" },
                unique: true);

            // R-16/M2 : une seule écriture sourcing active par entité source (types « aller » paie).
            // Remplace l'index non unique créé en AddAccountingModule.
            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_SourceEntityType_SourceEntityId",
                table: "JournalEntries");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_SourceEntityType_SourceEntityId",
                table: "JournalEntries",
                columns: new[] { "SourceEntityType", "SourceEntityId" },
                unique: true,
                filter: "[IsReversed] = 0 AND [SourceEntityType] IN (N'PayrollRun', N'PayrollPayment', N'CnssContributionPayment')");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restitue l'index non unique d'origine sur JournalEntries.
            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_SourceEntityType_SourceEntityId",
                table: "JournalEntries");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_SourceEntityType_SourceEntityId",
                table: "JournalEntries",
                columns: new[] { "SourceEntityType", "SourceEntityId" });

            migrationBuilder.DropIndex(
                name: "IX_Payslips_PayrollRunId_EmployeeId",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "SettledAmount",
                table: "EmployeeAdvances");

            migrationBuilder.DropColumn(
                name: "PartialDeductionCarryOver",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "HasPartialDeductions",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "SmigAnnualDeductionAmount",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "PayrollTaxBaseMode",
                table: "PayrollYearParameters");
        }
    }
}
