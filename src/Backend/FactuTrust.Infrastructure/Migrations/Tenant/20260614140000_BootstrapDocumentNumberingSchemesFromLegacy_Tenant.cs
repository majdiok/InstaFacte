using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

[DbContext(typeof(TenantDbContext))]
[Migration("20260614140000_BootstrapDocumentNumberingSchemesFromLegacy_Tenant")]
public partial class BootstrapDocumentNumberingSchemesFromLegacy_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        BootstrapFromInvoiceSequences(migrationBuilder, "FAC", 0);
        BootstrapFromInvoiceSequences(migrationBuilder, "AVO", 1);
        BootstrapFromQuoteSequences(migrationBuilder);
        BootstrapFromCashOperationSequences(migrationBuilder, "ENC", 7);
        BootstrapFromCashOperationSequences(migrationBuilder, "DEP", 8);
        BootstrapFromBankDepositSequences(migrationBuilder);
        BootstrapFromInventorySequences(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }

    // camelCase keys to match NumberingFormatBlock's [JsonPropertyName] so the value deserializes
    // correctly. (Older databases may still hold PascalCase rows written by a previous version of this
    // migration; NumberingSchemeDefaults.DeserializeBlocks reads case-insensitively to cover both.)
    private static string FormatJson(string prefix) =>
        "[{\"type\":0,\"value\":\"" + prefix + "\",\"order\":0},{\"type\":1,\"value\":\"-\",\"order\":1},{\"type\":9,\"value\":null,\"order\":2},{\"type\":1,\"value\":\"-\",\"order\":3},{\"type\":5,\"value\":null,\"order\":4}]";

    private static void BootstrapFromInvoiceSequences(MigrationBuilder migrationBuilder, string prefix, int documentType)
    {
        var formatJson = FormatJson(prefix);
        migrationBuilder.Sql($@"
IF OBJECT_ID(N'dbo.DocumentNumberingSchemes', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.InvoiceNumberSequences', N'U') IS NOT NULL
BEGIN
    INSERT INTO DocumentNumberingSchemes
        (Id, TenantId, DocumentType, FiscalYear, StartNumber, CurrentSequence, FormatBlocksJson, IsFormatLocked, CreatedAt, UpdatedAt)
    SELECT NEWID(), s.TenantId, {documentType}, s.FiscalYear, s.CurrentSequence + 1, s.CurrentSequence, N'{formatJson}',
        CASE WHEN s.CurrentSequence > 0 THEN 1 ELSE 0 END, GETUTCDATE(), GETUTCDATE()
    FROM InvoiceNumberSequences s
    WHERE s.Prefix = N'{prefix}' AND s.CurrentSequence > 0
      AND NOT EXISTS (SELECT 1 FROM DocumentNumberingSchemes d WHERE d.TenantId = s.TenantId AND d.DocumentType = {documentType} AND d.FiscalYear = s.FiscalYear);
END");
    }

    private static void BootstrapFromQuoteSequences(MigrationBuilder migrationBuilder)
    {
        var formatJson = FormatJson("DEV");
        migrationBuilder.Sql($@"
IF OBJECT_ID(N'dbo.DocumentNumberingSchemes', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.QuoteNumberSequences', N'U') IS NOT NULL
BEGIN
    INSERT INTO DocumentNumberingSchemes
        (Id, TenantId, DocumentType, FiscalYear, StartNumber, CurrentSequence, FormatBlocksJson, IsFormatLocked, CreatedAt, UpdatedAt)
    SELECT NEWID(), s.TenantId, 2, s.FiscalYear, s.CurrentSequence + 1, s.CurrentSequence, N'{formatJson}',
        CASE WHEN s.CurrentSequence > 0 THEN 1 ELSE 0 END, GETUTCDATE(), GETUTCDATE()
    FROM QuoteNumberSequences s
    WHERE s.Prefix = N'DEV' AND s.CurrentSequence > 0
      AND NOT EXISTS (SELECT 1 FROM DocumentNumberingSchemes d WHERE d.TenantId = s.TenantId AND d.DocumentType = 2 AND d.FiscalYear = s.FiscalYear);
END");
    }

    private static void BootstrapFromCashOperationSequences(MigrationBuilder migrationBuilder, string prefix, int documentType)
    {
        var formatJson = FormatJson(prefix);
        migrationBuilder.Sql($@"
IF OBJECT_ID(N'dbo.DocumentNumberingSchemes', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.CashExpenseNumberSequences', N'U') IS NOT NULL
BEGIN
    INSERT INTO DocumentNumberingSchemes
        (Id, TenantId, DocumentType, FiscalYear, StartNumber, CurrentSequence, FormatBlocksJson, IsFormatLocked, CreatedAt, UpdatedAt)
    SELECT NEWID(), s.TenantId, {documentType}, s.FiscalYear, s.CurrentSequence + 1, s.CurrentSequence, N'{formatJson}',
        CASE WHEN s.CurrentSequence > 0 THEN 1 ELSE 0 END, GETUTCDATE(), GETUTCDATE()
    FROM CashExpenseNumberSequences s
    WHERE s.Prefix = N'{prefix}' AND s.CurrentSequence > 0
      AND NOT EXISTS (SELECT 1 FROM DocumentNumberingSchemes d WHERE d.TenantId = s.TenantId AND d.DocumentType = {documentType} AND d.FiscalYear = s.FiscalYear);
END");
    }

    private static void BootstrapFromBankDepositSequences(MigrationBuilder migrationBuilder)
    {
        var formatJson = FormatJson("REM");
        migrationBuilder.Sql($@"
IF OBJECT_ID(N'dbo.DocumentNumberingSchemes', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.BankDepositNumberSequences', N'U') IS NOT NULL
BEGIN
    INSERT INTO DocumentNumberingSchemes
        (Id, TenantId, DocumentType, FiscalYear, StartNumber, CurrentSequence, FormatBlocksJson, IsFormatLocked, CreatedAt, UpdatedAt)
    SELECT NEWID(), s.TenantId, 9, s.FiscalYear, s.CurrentSequence + 1, s.CurrentSequence, N'{formatJson}',
        CASE WHEN s.CurrentSequence > 0 THEN 1 ELSE 0 END, GETUTCDATE(), GETUTCDATE()
    FROM BankDepositNumberSequences s
    WHERE s.CurrentSequence > 0
      AND NOT EXISTS (SELECT 1 FROM DocumentNumberingSchemes d WHERE d.TenantId = s.TenantId AND d.DocumentType = 9 AND d.FiscalYear = s.FiscalYear);
END");
    }

    private static void BootstrapFromInventorySequences(MigrationBuilder migrationBuilder)
    {
        const string inventoryFormatJson = "[{\"type\":0,\"value\":\"INVE\",\"order\":0},{\"type\":1,\"value\":\"-\",\"order\":1},{\"type\":5,\"value\":null,\"order\":2}]";
        migrationBuilder.Sql($@"
IF OBJECT_ID(N'dbo.DocumentNumberingSchemes', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.InventoryNumberSequences', N'U') IS NOT NULL
BEGIN
    INSERT INTO DocumentNumberingSchemes
        (Id, TenantId, DocumentType, FiscalYear, StartNumber, CurrentSequence, FormatBlocksJson, IsFormatLocked, CreatedAt, UpdatedAt)
    SELECT NEWID(), COALESCE((SELECT TOP 1 TenantId FROM InvoiceNumberSequences), (SELECT TOP 1 TenantId FROM QuoteNumberSequences), '00000000-0000-0000-0000-000000000000'),
        6, s.Year, s.LastSequence + 1, s.LastSequence, N'{inventoryFormatJson}',
        CASE WHEN s.LastSequence > 0 THEN 1 ELSE 0 END, GETUTCDATE(), GETUTCDATE()
    FROM InventoryNumberSequences s
    WHERE s.LastSequence > 0
      AND NOT EXISTS (SELECT 1 FROM DocumentNumberingSchemes d WHERE d.DocumentType = 6 AND d.FiscalYear = s.Year);
END");
    }
}