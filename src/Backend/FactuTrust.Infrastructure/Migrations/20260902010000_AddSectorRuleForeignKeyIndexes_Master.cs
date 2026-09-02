using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations;

/// <summary>
/// Ajoute les index de clé étrangère attendus par la convention EF après la déclaration des
/// relations sector-rules dans <c>MasterDbContext</c> (correctif du tri topologique des INSERT,
/// cf. la violation de FK SQL 547 au premier seed du catalogue sectoriel).
/// <para>
/// Deux des cinq colonnes de FK sont déjà couvertes et ne sont donc pas retraitées ici :
/// <c>SectorSegmentDomains.SegmentId</c> (colonne de tête de
/// <c>UX_SectorSegmentDomains_Segment_Domain</c>) et <c>SectorDataTemplateItems.TemplateId</c>
/// (<c>IX_SectorDataTemplateItems_TemplateId</c>). Les trois restantes ne le sont pas :
/// <c>UX_SectorModuleRules_Kind_Segment_Domain_Module</c> commence par <c>RuleKind</c>, donc ni
/// <c>SegmentId</c> ni <c>DomainId</c> n'ont d'index de tête.
/// </para>
/// SQL idempotent écrit à la main, suivant le motif <c>AddSectorRuleTables_Master</c>
/// (pas de fichier Designer).
/// </summary>
[DbContext(typeof(MasterDbContext))]
[Migration("20260902010000_AddSectorRuleForeignKeyIndexes_Master")]
public partial class AddSectorRuleForeignKeyIndexes_Master : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SectorSegmentDomains_DomainId' AND object_id = OBJECT_ID(N'dbo.SectorSegmentDomains'))
    CREATE INDEX [IX_SectorSegmentDomains_DomainId] ON [SectorSegmentDomains] ([DomainId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SectorModuleRules_SegmentId' AND object_id = OBJECT_ID(N'dbo.SectorModuleRules'))
    CREATE INDEX [IX_SectorModuleRules_SegmentId] ON [SectorModuleRules] ([SegmentId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SectorModuleRules_DomainId' AND object_id = OBJECT_ID(N'dbo.SectorModuleRules'))
    CREATE INDEX [IX_SectorModuleRules_DomainId] ON [SectorModuleRules] ([DomainId]);
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SectorModuleRules_DomainId' AND object_id = OBJECT_ID(N'dbo.SectorModuleRules'))
    DROP INDEX [IX_SectorModuleRules_DomainId] ON [SectorModuleRules];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SectorModuleRules_SegmentId' AND object_id = OBJECT_ID(N'dbo.SectorModuleRules'))
    DROP INDEX [IX_SectorModuleRules_SegmentId] ON [SectorModuleRules];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SectorSegmentDomains_DomainId' AND object_id = OBJECT_ID(N'dbo.SectorSegmentDomains'))
    DROP INDEX [IX_SectorSegmentDomains_DomainId] ON [SectorSegmentDomains];
""");
    }
}
