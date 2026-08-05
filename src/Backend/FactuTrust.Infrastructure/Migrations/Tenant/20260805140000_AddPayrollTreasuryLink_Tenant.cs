using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

/// <summary>
/// Lien trésorerie paie : paiements, suivi bulletin, comptes auxiliaires 421.
/// </summary>
[DbContext(typeof(TenantDbContext))]
[Migration("20260805140000_AddPayrollTreasuryLink_Tenant")]
public partial class AddPayrollTreasuryLink_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "PaidAmount",
            table: "Payslips",
            type: "decimal(18,3)",
            precision: 18,
            scale: 3,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<DateTime>(
            name: "PaidAt",
            table: "Payslips",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EmployeeAuxiliaryAccount",
            table: "Payslips",
            type: "nvarchar(10)",
            maxLength: 10,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "PayrollPayments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PayrollRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                AmountCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                Method = table.Column<int>(type: "int", nullable: false),
                BankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                IsCancelled = table.Column<bool>(type: "bit", nullable: false),
                CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CancelledBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Version = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PayrollPayments", x => x.Id);
                table.ForeignKey(
                    name: "FK_PayrollPayments_PayrollRuns_PayrollRunId",
                    column: x => x.PayrollRunId,
                    principalTable: "PayrollRuns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PayrollPaymentLines",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PayrollPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PayslipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                AmountCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                EmployeeAuxiliaryAccount = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PayrollPaymentLines", x => x.Id);
                table.ForeignKey(
                    name: "FK_PayrollPaymentLines_PayrollPayments_PayrollPaymentId",
                    column: x => x.PayrollPaymentId,
                    principalTable: "PayrollPayments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PayrollPayments_PayrollRunId_IsCancelled",
            table: "PayrollPayments",
            columns: new[] { "PayrollRunId", "IsCancelled" });

        migrationBuilder.CreateIndex(
            name: "IX_PayrollPayments_PaymentDate",
            table: "PayrollPayments",
            column: "PaymentDate");

        migrationBuilder.CreateIndex(
            name: "IX_PayrollPaymentLines_PayslipId",
            table: "PayrollPaymentLines",
            column: "PayslipId");

        migrationBuilder.CreateIndex(
            name: "IX_PayrollPaymentLines_EmployeeId",
            table: "PayrollPaymentLines",
            column: "EmployeeId");

        migrationBuilder.Sql("""
            IF NOT EXISTS (SELECT 1 FROM ChartOfAccounts WHERE AccountNumber = '42')
            INSERT INTO ChartOfAccounts (Id, AccountNumber, Label, AccountClass, ParentAccountNumber, NatureType, IsSystem, IsActive, Level, AccountType, IsAuxiliary, AffectationAccountNumber, CreatedAt, UpdatedAt)
            VALUES (NEWID(), '42', 'Personnel', 4, NULL, 1, 1, 1, 2, 0, 0, NULL, GETUTCDATE(), NULL);

            IF NOT EXISTS (SELECT 1 FROM ChartOfAccounts WHERE AccountNumber = '421')
            INSERT INTO ChartOfAccounts (Id, AccountNumber, Label, AccountClass, ParentAccountNumber, NatureType, IsSystem, IsActive, Level, AccountType, IsAuxiliary, AffectationAccountNumber, CreatedAt, UpdatedAt)
            VALUES (NEWID(), '421', 'Rémunérations dues au personnel', 4, '42', 1, 1, 1, 3, 0, 0, NULL, GETUTCDATE(), NULL);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PayrollPaymentLines");
        migrationBuilder.DropTable(name: "PayrollPayments");

        migrationBuilder.DropColumn(name: "PaidAmount", table: "Payslips");
        migrationBuilder.DropColumn(name: "PaidAt", table: "Payslips");
        migrationBuilder.DropColumn(name: "EmployeeAuxiliaryAccount", table: "Payslips");
    }
}
