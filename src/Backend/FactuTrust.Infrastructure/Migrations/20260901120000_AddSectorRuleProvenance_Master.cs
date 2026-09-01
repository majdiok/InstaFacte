using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations;

/// <summary>
/// Review R2 hardening — adds <c>IsManagedByCatalog</c> (bit NOT NULL DEFAULT 1) to the eight
/// sector-rule tables and a filtered unique index on <c>SectorDataTemplateItems</c>
/// (TemplateId, ItemKind, SortOrder) WHERE IsActive = 1.
/// <para>
/// The provenance flag marks rows that were seeded/updated by the catalog so that the startup
/// reconciliation (<c>SectorRuleSeeder.ReconcileOnStartupAsync</c>) can safely
/// (a) deactivate/remove catalog-owned rows that no longer exist in the catalog, and
/// (b) leave admin-authored rows untouched instead of silently resetting them.
/// </para>
/// <para>
/// The unique index is filtered on <c>IsActive = 1</c> (not a plain unique index) because both the
/// catalog reconciliation and the admin "replace template items" path soft-deactivate obsolete rows
/// (IsActive = 0) rather than deleting them — so the same natural key may legitimately appear on
/// several inactive rows. The filter therefore guarantees at most one <em>active</em> row per key.
/// </para>
/// Hand-written idempotent SQL following the <c>AddSectorRuleTables_Master</c> pattern (no Designer).
/// </summary>
[DbContext(typeof(MasterDbContext))]
[Migration("20260901120000_AddSectorRuleProvenance_Master")]
public partial class AddSectorRuleProvenance_Master : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
DECLARE @TableName nvarchar(256);

-- 1. Add IsManagedByCatalog to every sector-rule table (idempotent).
DECLARE table_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT name FROM (VALUES
        (N'SectorSegments'),
        (N'SectorDomains'),
        (N'SectorSegmentDomains'),
        (N'SectorModuleRules'),
        (N'SectorModuleDependencies'),
        (N'SectorDefaultSettings'),
        (N'SectorDataTemplates'),
        (N'SectorDataTemplateItems')
    ) AS t(name);

OPEN table_cursor;
FETCH NEXT FROM table_cursor INTO @TableName;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'dbo.' + QUOTENAME(@TableName), N'U') IS NOT NULL
        AND NOT EXISTS (
            SELECT 1 FROM sys.columns
            WHERE object_id = OBJECT_ID(N'dbo.' + QUOTENAME(@TableName)) AND name = 'IsManagedByCatalog')
    BEGIN
        DECLARE @sql nvarchar(max) = N'ALTER TABLE ' + QUOTENAME(@TableName)
            + N' ADD [IsManagedByCatalog] bit NOT NULL CONSTRAINT [DF_' + @TableName + N'_IsManagedByCatalog] DEFAULT 1';
        EXEC sp_executesql @sql;
    END
    FETCH NEXT FROM table_cursor INTO @TableName;
END;
CLOSE table_cursor;
DEALLOCATE table_cursor;

-- 2. De-dupe SectorDataTemplateItems so at most one ACTIVE row per natural key remains,
--    then create the filtered unique index enforcing that invariant going forward.
IF OBJECT_ID(N'dbo.SectorDataTemplateItems', N'U') IS NOT NULL
BEGIN
    ;WITH Dupes AS (
        SELECT Id,
               ROW_NUMBER() OVER (PARTITION BY TemplateId, ItemKind, SortOrder ORDER BY Id) AS rn
        FROM [SectorDataTemplateItems]
        WHERE IsActive = 1
    )
    UPDATE t
    SET t.IsActive = 0
    FROM [SectorDataTemplateItems] t
    INNER JOIN Dupes d ON d.Id = t.Id
    WHERE d.rn > 1;

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.SectorDataTemplateItems')
          AND name = 'UX_SectorDataTemplateItems_Template_Kind_SortOrder_Active')
    BEGIN
        CREATE UNIQUE INDEX [UX_SectorDataTemplateItems_Template_Kind_SortOrder_Active]
            ON [SectorDataTemplateItems] ([TemplateId], [ItemKind], [SortOrder])
            WHERE [IsActive] = 1;
    END
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
DECLARE @TableName nvarchar(256);

IF OBJECT_ID(N'dbo.SectorDataTemplateItems', N'U') IS NOT NULL
    AND EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.SectorDataTemplateItems')
          AND name = 'UX_SectorDataTemplateItems_Template_Kind_SortOrder_Active')
BEGIN
    DROP INDEX [UX_SectorDataTemplateItems_Template_Kind_SortOrder_Active] ON [SectorDataTemplateItems];
END

DECLARE table_cursor2 CURSOR LOCAL FAST_FORWARD FOR
    SELECT name FROM (VALUES
        (N'SectorSegments'),
        (N'SectorDomains'),
        (N'SectorSegmentDomains'),
        (N'SectorModuleRules'),
        (N'SectorModuleDependencies'),
        (N'SectorDefaultSettings'),
        (N'SectorDataTemplates'),
        (N'SectorDataTemplateItems')
    ) AS t(name);

OPEN table_cursor2;
FETCH NEXT FROM table_cursor2 INTO @TableName;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'dbo.' + QUOTENAME(@TableName), N'U') IS NOT NULL
        AND EXISTS (
            SELECT 1 FROM sys.columns
            WHERE object_id = OBJECT_ID(N'dbo.' + QUOTENAME(@TableName)) AND name = 'IsManagedByCatalog')
    BEGIN
        DECLARE @sql2 nvarchar(max) = N'ALTER TABLE ' + QUOTENAME(@TableName)
            + N' DROP CONSTRAINT [DF_' + @TableName + N'_IsManagedByCatalog];'
            + N' ALTER TABLE ' + QUOTENAME(@TableName)
            + N' DROP COLUMN [IsManagedByCatalog]';
        EXEC sp_executesql @sql2;
    END
    FETCH NEXT FROM table_cursor2 INTO @TableName;
END;
CLOSE table_cursor2;
DEALLOCATE table_cursor2;
""");
    }
}
