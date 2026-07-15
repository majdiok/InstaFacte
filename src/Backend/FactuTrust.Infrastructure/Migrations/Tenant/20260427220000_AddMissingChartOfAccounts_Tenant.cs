using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Ajoute au plan comptable les comptes requis par le moteur comptable et absents du seed initial.
    ///
    /// Comptes ajoutés :
    ///  - 4478 « Timbre fiscal » : utilisé par GenerateInvoiceSaleEntryAsync. Sans ce compte,
    ///    ValidateAccountsExistAsync rejetait l'écriture de vente entière (section Produits vide).
    ///  - 44560-44567 : sous-comptes de retenue à la source utilisés par
    ///    WithholdingAccountForOperationCode / GenerateSupplierInvoiceWithholdingEntryAsync.
    ///
    /// Cette migration était auparavant dépourvue des attributs [DbContext] / [Migration] :
    /// EF Core ne la découvrait pas et elle ne s'exécutait jamais. Les attributs sont rétablis
    /// et les insertions rendues idempotentes (INSERT conditionnel) pour les tenants où ces
    /// comptes ont déjà été auto-créés par le moteur comptable.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260427220000_AddMissingChartOfAccounts_Tenant")]
    public partial class AddMissingChartOfAccounts_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Insertion idempotente : ne crée le compte que s'il n'existe pas déjà
            // (cas des tenants où le moteur comptable l'a auto-créé avant cette migration).
            void Row(string id, string num, string label, int cls, string parent, int nature, int level)
            {
                var safeLabel = label.Replace("'", "''");
                migrationBuilder.Sql(
                    $"IF NOT EXISTS (SELECT 1 FROM [ChartOfAccounts] WHERE [AccountNumber] = N'{num}') " +
                    "INSERT INTO [ChartOfAccounts] " +
                    "([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], " +
                    "[IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]) " +
                    $"VALUES ('{id}', N'{num}', N'{safeLabel}', {cls}, N'{parent}', {nature}, " +
                    $"1, 1, {level}, '2026-01-01T00:00:00', NULL, N'system', NULL);");
            }

            // Compte de timbre fiscal sur ventes (NatureType 1 = Crédit / Passif).
            Row("11111111-1111-1111-1111-111111000200", "4478", "Autres impôts, taxes et versements assimilés", 4, "447", 1, 4);

            // Sous-comptes de retenue à la source (RS) — dettes envers l'État (NatureType 1 = Crédit).
            Row("11111111-1111-1111-1111-111111000201", "44560", "RS - Honoraires, commissions, courtages (régime commun)", 4, "445", 1, 5);
            Row("11111111-1111-1111-1111-111111000202", "44561", "RS - Commissions et courtages", 4, "445", 1, 5);
            Row("11111111-1111-1111-1111-111111000203", "44562", "RS - Loyers", 4, "445", 1, 5);
            Row("11111111-1111-1111-1111-111111000204", "44563", "RS - Rémunérations servies aux non-résidents", 4, "445", 1, 5);
            Row("11111111-1111-1111-1111-111111000205", "44564", "RS - Rémunérations non commerciales", 4, "445", 1, 5);
            Row("11111111-1111-1111-1111-111111000206", "44565", "RS - Honoraires servis aux personnes morales", 4, "445", 1, 5);
            Row("11111111-1111-1111-1111-111111000207", "44566", "RS - Montants >= 1000 DT TTC", 4, "445", 1, 5);
            Row("11111111-1111-1111-1111-111111000208", "44567", "RS - Plus-values immobilières", 4, "445", 1, 5);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var accounts = new[] { "4478", "44560", "44561", "44562", "44563", "44564", "44565", "44566", "44567" };
            foreach (var acc in accounts)
            {
                migrationBuilder.DeleteData(
                    table: "ChartOfAccounts",
                    keyColumn: "AccountNumber",
                    keyValue: acc);
            }
        }
    }
}
