using Xunit;

namespace FactuTrust.Infrastructure.Tests.Migrations;

public sealed class BootstrapDocumentNumberingMigrationTests
{
    [Fact]
    public void BootstrapMigration_ReferencesCashExpenseTable_NotCashOperationTable()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "FactuTrust.Infrastructure",
            "Migrations",
            "Tenant",
            "20260614140000_BootstrapDocumentNumberingSchemesFromLegacy_Tenant.cs"));

        Assert.True(File.Exists(sourcePath), $"Migration source not found: {sourcePath}");

        var source = File.ReadAllText(sourcePath);

        Assert.Contains("CashExpenseNumberSequences", source);
        Assert.DoesNotContain("CashOperationNumberSequences", source);
        Assert.Contains("IF OBJECT_ID(N'dbo.DocumentNumberingSchemes', N'U') IS NOT NULL", source);
    }
}