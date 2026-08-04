using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

/// <summary>
/// Ajoute ActivityCode (snapshot chaîne) sur les lignes facture / devis honoraires.
/// Migration manuelle (scaffold EF bloqué par snapshot PriceList existant).
/// </summary>
[DbContext(typeof(TenantDbContext))]
[Migration("20260802140000_AddHonorairesLineActivityCode_Tenant")]
public partial class AddHonorairesLineActivityCode_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.HonorairesInvoiceLines', N'ActivityCode') IS NULL
BEGIN
    ALTER TABLE [HonorairesInvoiceLines] ADD [ActivityCode] nvarchar(50) NULL;
END
""");

        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.HonorairesQuoteLines', N'ActivityCode') IS NULL
BEGIN
    ALTER TABLE [HonorairesQuoteLines] ADD [ActivityCode] nvarchar(50) NULL;
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.HonorairesInvoiceLines', N'ActivityCode') IS NOT NULL
BEGIN
    ALTER TABLE [HonorairesInvoiceLines] DROP COLUMN [ActivityCode];
END
""");

        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.HonorairesQuoteLines', N'ActivityCode') IS NOT NULL
BEGIN
    ALTER TABLE [HonorairesQuoteLines] DROP COLUMN [ActivityCode];
END
""");
    }
}
