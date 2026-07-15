using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

/// <summary>
/// Supprime les tables de certificats RS persistés et la colonne de liaison sur les factures fournisseurs.
/// La déclaration TEJ est désormais construite à partir des FF soldées avec retenue (PaidAt).
/// </summary>
[DbContext(typeof(TenantDbContext))]
[Migration("20260404000000_RemoveWithholdingTaxCertificates_Tenant")]
public partial class RemoveWithholdingTaxCertificates_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "WithholdingTaxCertificateLines");

        migrationBuilder.DropTable(
            name: "WithholdingTaxCertificates");

        migrationBuilder.DropColumn(
            name: "WithholdingCertificateId",
            table: "SupplierInvoices");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "WithholdingCertificateId",
            table: "SupplierInvoices",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "WithholdingTaxCertificates",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BeneficiaryActivity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                BeneficiaryAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                BeneficiaryCategory = table.Column<int>(type: "int", nullable: false),
                BeneficiaryEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                BeneficiaryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                BeneficiaryName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                BeneficiaryPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                BeneficiaryType = table.Column<int>(type: "int", nullable: false),
                BillingYear = table.Column<int>(type: "int", nullable: false),
                CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CertificateNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                CountryCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeclarantCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                HasCNPC = table.Column<bool>(type: "bit", nullable: false),
                HasPriseEnCharge = table.Column<bool>(type: "bit", nullable: false),
                IdentificationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                IdentificationType = table.Column<int>(type: "int", nullable: false),
                IsResident = table.Column<bool>(type: "bit", nullable: false),
                PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                SignatureHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                SourceInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SourcePaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SourceSupplierInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
                SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                TejSubmissionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                TotalAmountHT = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                TotalAmountTTC = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                TotalAmountTVA = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                TotalAmountWithheld = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                TotalNetPaid = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ValidatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ValidatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                Version = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WithholdingTaxCertificates", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "WithholdingTaxCertificateLines",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CertificateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AdditionalTaxesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                AmountHT = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                AmountTTC = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                AmountTTCForeignCurrency = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                AmountTVA = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                AmountWithheld = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                AmountWithheldForeignCurrency = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                ExchangeRate = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                NetAmountPaid = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                NetAmountPaidForeignCurrency = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                OperationCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                VatRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                WithholdingRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                WithholdingTaxTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WithholdingTaxCertificateLines", x => x.Id);
                table.ForeignKey(
                    name: "FK_WithholdingTaxCertificateLines_WithholdingTaxCertificates_CertificateId",
                    column: x => x.CertificateId,
                    principalTable: "WithholdingTaxCertificates",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_WithholdingTaxCertificateLines_CertificateId",
            table: "WithholdingTaxCertificateLines",
            column: "CertificateId");

        migrationBuilder.CreateIndex(
            name: "IX_WithholdingTaxCertificateLines_WithholdingTaxTypeId",
            table: "WithholdingTaxCertificateLines",
            column: "WithholdingTaxTypeId");

        migrationBuilder.CreateIndex(
            name: "IX_WithholdingTaxCertificates_BeneficiaryId",
            table: "WithholdingTaxCertificates",
            column: "BeneficiaryId");

        migrationBuilder.CreateIndex(
            name: "IX_WithholdingTaxCertificates_CertificateNumber",
            table: "WithholdingTaxCertificates",
            column: "CertificateNumber");

        migrationBuilder.CreateIndex(
            name: "IX_WithholdingTaxCertificates_DeclarantCompanyId",
            table: "WithholdingTaxCertificates",
            column: "DeclarantCompanyId");

        migrationBuilder.CreateIndex(
            name: "IX_WithholdingTaxCertificates_PaymentDate",
            table: "WithholdingTaxCertificates",
            column: "PaymentDate");

        migrationBuilder.CreateIndex(
            name: "IX_WithholdingTaxCertificates_PaymentDate_Status",
            table: "WithholdingTaxCertificates",
            columns: new[] { "PaymentDate", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_WithholdingTaxCertificates_SourceSupplierInvoiceId",
            table: "WithholdingTaxCertificates",
            column: "SourceSupplierInvoiceId");

        migrationBuilder.CreateIndex(
            name: "IX_WithholdingTaxCertificates_Status",
            table: "WithholdingTaxCertificates",
            column: "Status");
    }
}
