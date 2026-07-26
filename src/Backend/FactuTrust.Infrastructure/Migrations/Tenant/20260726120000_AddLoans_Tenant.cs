using System;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

/// <summary>
/// Emprunts et échéanciers (tableau d'amortissement d'emprunt).
/// <para>
/// Migration STRICTEMENT ADDITIVE : deux tables neuves, aucune modification d'une table existante
/// (aucun AlterTable / AddColumn / DropColumn). Écrite à la main comme les migrations tenant
/// récentes — le snapshot du modèle étant désynchronisé, <c>dotnet ef migrations add</c> ne doit
/// PAS être utilisé (il tenterait de recréer les tables absentes du snapshot).
/// </para>
/// </summary>
[DbContext(typeof(TenantDbContext))]
[Migration("20260726120000_AddLoans_Tenant")]
public partial class AddLoans_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Loans",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LoanNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Label = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                LenderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Principal = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                AnnualRatePercent = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                InstallmentCount = table.Column<int>(type: "int", nullable: false),
                Periodicity = table.Column<int>(type: "int", nullable: false),
                Method = table.Column<int>(type: "int", nullable: false),
                LoanAccountNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                InterestAccountNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                BankAccountNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                Version = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Loans", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "LoanScheduleLines",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                InstallmentNumber = table.Column<int>(type: "int", nullable: false),
                DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                OpeningBalance = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                InterestAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                PrincipalAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                InstallmentAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                ClosingBalance = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LoanScheduleLines", x => x.Id);
                table.ForeignKey(
                    name: "FK_LoanScheduleLines_Loans_LoanId",
                    column: x => x.LoanId,
                    principalTable: "Loans",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Loans_LoanNumber",
            table: "Loans",
            column: "LoanNumber",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Loans_Status",
            table: "Loans",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_Loans_StartDate",
            table: "Loans",
            column: "StartDate");

        migrationBuilder.CreateIndex(
            name: "IX_LoanScheduleLines_LoanId_InstallmentNumber",
            table: "LoanScheduleLines",
            columns: new[] { "LoanId", "InstallmentNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_LoanScheduleLines_DueDate",
            table: "LoanScheduleLines",
            column: "DueDate");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Ordre inverse : les lignes (porteuses de la clé étrangère) avant l'en-tête.
        migrationBuilder.DropTable(name: "LoanScheduleLines");
        migrationBuilder.DropTable(name: "Loans");
    }
}
