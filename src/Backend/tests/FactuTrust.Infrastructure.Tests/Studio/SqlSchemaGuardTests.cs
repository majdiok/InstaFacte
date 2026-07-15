using FactuTrust.Application.Features.Studio.Common;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

public sealed class SqlSchemaGuardTests
{
    [Theory]
    [InlineData("AuditLogs")]
    [InlineData("CustomRecords")]
    [InlineData("CustomViewDefinitions")]
    [InlineData("TejXmlExportLogs")]
    [InlineData("UserDashboardLayouts")]
    [InlineData("__EFMigrationsHistory")]
    [InlineData("AspNetUsers")]
    [InlineData("AspNetRoles")]
    public void Denied_tables_are_blocked(string table)
    {
        Assert.True(SqlSchemaGuard.IsDenied(table));
    }

    [Theory]
    [InlineData("Clients")]
    [InlineData("Invoices")]
    [InlineData("Products")]
    [InlineData("Quotes")]
    public void Business_tables_are_allowed(string table)
    {
        Assert.False(SqlSchemaGuard.IsDenied(table));
    }

    [Theory]
    [InlineData("Clients", true)]
    [InlineData("Client_Name", true)]
    [InlineData("_x", true)]
    [InlineData("1abc", false)]
    [InlineData("a-b", false)]
    [InlineData("a b", false)]
    [InlineData("Clients;DROP TABLE x", false)]
    [InlineData("", false)]
    [InlineData("a'b", false)]
    public void Identifier_validation(string name, bool valid)
    {
        Assert.Equal(valid, SqlSchemaGuard.IsValidIdentifier(name));
    }

    [Fact]
    public void Too_long_identifier_rejected()
    {
        Assert.False(SqlSchemaGuard.IsValidIdentifier(new string('a', 129)));
    }

    [Fact]
    public void Quote_brackets_and_escapes()
    {
        Assert.Equal("[Clients]", SqlSchemaGuard.Quote("Clients"));
        Assert.Equal("[a]]b]", SqlSchemaGuard.Quote("a]b"));
    }

    [Theory]
    [InlineData("int", true)]
    [InlineData("decimal", true)]
    [InlineData("money", true)]
    [InlineData("float", true)]
    [InlineData("nvarchar", false)]
    [InlineData("bit", false)]
    [InlineData("datetime", false)]
    public void Numeric_type_detection(string type, bool numeric)
    {
        Assert.Equal(numeric, SqlSchemaGuard.IsNumericSqlType(type));
    }

    [Theory]
    [InlineData("nvarchar", true)]
    [InlineData("varchar", true)]
    [InlineData("text", true)]
    [InlineData("int", false)]
    [InlineData("datetime", false)]
    public void Text_type_detection(string type, bool text)
    {
        Assert.Equal(text, SqlSchemaGuard.IsTextSqlType(type));
    }
}
