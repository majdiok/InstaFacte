using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations;

/// <summary>
/// Plan §3.1 — adds the <c>SectorTaxRegimeSuggestions</c> master table (segment → suggested tax
/// regime rows with a French note; informational, admin-editable superseder of
/// <c>UsualTaxRegimeCatalog</c>). Hand-written idempotent SQL, following the
/// <c>AddModuleGrantAuditEntries_Master</c> pattern (no Designer file, no snapshot update).
/// </summary>
[DbContext(typeof(MasterDbContext))]
[Migration("20260901180000_AddSectorTaxRegimeSuggestions_Master")]
public partial class AddSectorTaxRegimeSuggestions_Master : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.SectorTaxRegimeSuggestions', N'U') IS NULL
BEGIN
    CREATE TABLE [SectorTaxRegimeSuggestions] (
        [Id] uniqueidentifier NOT NULL,
        [SegmentCode] nvarchar(50) NOT NULL,
        [Regime] int NOT NULL,
        [NoteFr] nvarchar(500) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_SectorTaxRegimeSuggestions_IsActive] DEFAULT 1,
        [IsManagedByCatalog] bit NOT NULL CONSTRAINT [DF_SectorTaxRegimeSuggestions_IsManagedByCatalog] DEFAULT 1,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(450) NULL,
        [UpdatedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_SectorTaxRegimeSuggestions] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [UX_SectorTaxRegimeSuggestions_Segment_Regime]
        ON [SectorTaxRegimeSuggestions] ([SegmentCode], [Regime]);
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.SectorTaxRegimeSuggestions', N'U') IS NOT NULL
BEGIN
    DROP TABLE [SectorTaxRegimeSuggestions];
END
""");
    }
}
