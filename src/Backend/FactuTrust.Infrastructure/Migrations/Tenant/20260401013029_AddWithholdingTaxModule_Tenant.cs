using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddWithholdingTaxModule_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Activity",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "Suppliers",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "Suppliers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultWithholdingRate",
                table: "Suppliers",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultWithholdingTaxTypeId",
                table: "Suppliers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsResident",
                table: "Suppliers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSubjectToWithholding",
                table: "Suppliers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TejIdentificationType",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSubjectToWithholding",
                table: "SupplierInvoices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "NetAmountAfterWithholding",
                table: "SupplierInvoices",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WithholdingAmount",
                table: "SupplierInvoices",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WithholdingCertificateId",
                table: "SupplierInvoices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WithholdingRate",
                table: "SupplierInvoices",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WithholdingTaxTypeId",
                table: "SupplierInvoices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstablishmentCode",
                table: "Companies",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TejAdherentSince",
                table: "Companies",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TejCategory",
                table: "Companies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Activity",
                table: "Clients",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "Clients",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "Clients",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsResident",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "TejIdentificationType",
                table: "Clients",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WithholdingTaxCertificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CertificateNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DeclarantCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BeneficiaryType = table.Column<int>(type: "int", nullable: false),
                    BeneficiaryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BeneficiaryName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IdentificationType = table.Column<int>(type: "int", nullable: false),
                    IdentificationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BeneficiaryCategory = table.Column<int>(type: "int", nullable: false),
                    IsResident = table.Column<bool>(type: "bit", nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BeneficiaryAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BeneficiaryEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    BeneficiaryPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BeneficiaryActivity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BillingYear = table.Column<int>(type: "int", nullable: false),
                    HasCNPC = table.Column<bool>(type: "bit", nullable: false),
                    HasPriseEnCharge = table.Column<bool>(type: "bit", nullable: false),
                    TotalAmountHT = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalAmountTVA = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalAmountTTC = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalAmountWithheld = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalNetPaid = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TejSubmissionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceSupplierInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourcePaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ValidatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValidatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SignatureHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WithholdingTaxCertificates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WithholdingTaxTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    LabelAr = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DefaultRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ArticleReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApplicableToResident = table.Column<bool>(type: "bit", nullable: false),
                    ApplicableToNonResident = table.Column<bool>(type: "bit", nullable: false),
                    MinimumThreshold = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WithholdingTaxTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WithholdingTaxCertificateLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CertificateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WithholdingTaxTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AmountHT = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    VatRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    AmountTVA = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AmountTTC = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    WithholdingRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    AmountWithheld = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    NetAmountPaid = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    AmountWithheldForeignCurrency = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    AmountTTCForeignCurrency = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    NetAmountPaidForeignCurrency = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    AdditionalTaxesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
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

            migrationBuilder.CreateIndex(
                name: "IX_WithholdingTaxTypes_Category",
                table: "WithholdingTaxTypes",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_WithholdingTaxTypes_Code",
                table: "WithholdingTaxTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WithholdingTaxTypes_DisplayOrder",
                table: "WithholdingTaxTypes",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_WithholdingTaxTypes_IsActive",
                table: "WithholdingTaxTypes",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WithholdingTaxCertificateLines");

            migrationBuilder.DropTable(
                name: "WithholdingTaxTypes");

            migrationBuilder.DropTable(
                name: "WithholdingTaxCertificates");

            migrationBuilder.DropColumn(
                name: "Activity",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "DefaultWithholdingRate",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "DefaultWithholdingTaxTypeId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IsResident",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IsSubjectToWithholding",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TejIdentificationType",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IsSubjectToWithholding",
                table: "SupplierInvoices");

            migrationBuilder.DropColumn(
                name: "NetAmountAfterWithholding",
                table: "SupplierInvoices");

            migrationBuilder.DropColumn(
                name: "WithholdingAmount",
                table: "SupplierInvoices");

            migrationBuilder.DropColumn(
                name: "WithholdingCertificateId",
                table: "SupplierInvoices");

            migrationBuilder.DropColumn(
                name: "WithholdingRate",
                table: "SupplierInvoices");

            migrationBuilder.DropColumn(
                name: "WithholdingTaxTypeId",
                table: "SupplierInvoices");

            migrationBuilder.DropColumn(
                name: "EstablishmentCode",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "TejAdherentSince",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "TejCategory",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "Activity",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "IsResident",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "TejIdentificationType",
                table: "Clients");
        }
    }
}
