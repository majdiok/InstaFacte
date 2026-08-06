using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Bordereau CNSS : matricule employeur sur la société et suivi des versements mensuels.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260808120000_AddCnssContributionPayment_Tenant")]
    public partial class AddCnssContributionPayment_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CnssEmployerNumber",
                table: "Companies",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CnssContributionPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    PayrollRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TotalCnssEmployee = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalCnssEmployer = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalWorkAccident = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalDue = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
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
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CnssContributionPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CnssContributionPayments_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CnssContributionPayments_PayrollRunId",
                table: "CnssContributionPayments",
                column: "PayrollRunId");

            migrationBuilder.CreateIndex(
                name: "IX_CnssContributionPayments_Year_Month",
                table: "CnssContributionPayments",
                columns: new[] { "Year", "Month" },
                unique: true,
                filter: "[IsCancelled] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CnssContributionPayments_Year_Month_IsCancelled",
                table: "CnssContributionPayments",
                columns: new[] { "Year", "Month", "IsCancelled" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CnssContributionPayments");

            migrationBuilder.DropColumn(
                name: "CnssEmployerNumber",
                table: "Companies");
        }
    }
}
