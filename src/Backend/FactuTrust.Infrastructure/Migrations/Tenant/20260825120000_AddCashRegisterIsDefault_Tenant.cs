using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

/// <summary>
/// N caisses / entrepôt : une seule caisse défaut active.
/// Migration manuelle (snapshot EF n'inclut pas les tables POS).
/// </summary>
[DbContext(typeof(TenantDbContext))]
[Migration("20260825120000_AddCashRegisterIsDefault_Tenant")]
public partial class AddCashRegisterIsDefault_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[CashRegisters]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.CashRegisters', N'IsDefault') IS NULL
BEGIN
    ALTER TABLE [dbo].[CashRegisters]
        ADD [IsDefault] bit NOT NULL CONSTRAINT [DF_CashRegisters_IsDefault] DEFAULT (0);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[CashRegisters]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.CashRegisters', N'IsDefault') IS NOT NULL
BEGIN
    UPDATE [dbo].[CashRegisters] SET [IsDefault] = 0;

    ;WITH ranked AS (
        SELECT [Id],
               ROW_NUMBER() OVER (
                   PARTITION BY [WarehouseId]
                   ORDER BY CASE WHEN [IsActive] = 1 THEN 0 ELSE 1 END, [Code]
               ) AS [rn]
        FROM [dbo].[CashRegisters]
    )
    UPDATE r
    SET r.[IsDefault] = 1
    FROM [dbo].[CashRegisters] r
    INNER JOIN ranked x ON r.[Id] = x.[Id]
    WHERE x.[rn] = 1;
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[CashRegisters]', N'U') IS NOT NULL
AND NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_CashRegisters_WarehouseId_Default'
      AND object_id = OBJECT_ID(N'[dbo].[CashRegisters]')
)
BEGIN
    CREATE UNIQUE INDEX [IX_CashRegisters_WarehouseId_Default]
        ON [dbo].[CashRegisters] ([WarehouseId])
        WHERE [IsDefault] = 1 AND [IsActive] = 1;
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_CashRegisters_WarehouseId_Default'
      AND object_id = OBJECT_ID(N'[dbo].[CashRegisters]')
)
    DROP INDEX [IX_CashRegisters_WarehouseId_Default] ON [dbo].[CashRegisters];
""");

        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.CashRegisters', N'IsDefault') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[CashRegisters] DROP CONSTRAINT [DF_CashRegisters_IsDefault];
    ALTER TABLE [dbo].[CashRegisters] DROP COLUMN [IsDefault];
END
""");
    }
}
