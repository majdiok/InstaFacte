using FactuTrust.Infrastructure.MultiTenancy;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.MultiTenancy;

/// <summary>
/// CWE-89 hardening: <c>TenantDatabaseProvisioner</c> interpolates <c>databaseName</c> into raw DDL,
/// both as a bracket-quoted identifier and inside <c>N'...'</c> literals. These tests exercise the
/// pure validation/escaping helpers directly (no SQL Server needed).
/// </summary>
public sealed class TenantDatabaseProvisionerNameValidationTests
{
    [Theory]
    [InlineData("FactuTrust_Tenant_AB12CD34")] // real generated shape
    [InlineData("FactuTrust_Tenant_Template")]
    [InlineData("Some_Valid_Name123")]
    public void ValidateDatabaseName_accepts_generated_shape(string name)
    {
        TenantDatabaseProvisioner.ValidateDatabaseName(name); // must not throw
    }

    [Theory]
    [InlineData("FactuTrust_Tenant_AB12CD34'; DROP DATABASE Master; --")]
    [InlineData("Foo]Bar")]
    [InlineData("Foo Bar")] // space not allowed
    [InlineData("Foo;Bar")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void ValidateDatabaseName_rejects_anything_outside_safe_shape(string? name)
    {
        Assert.Throws<ArgumentException>(() => TenantDatabaseProvisioner.ValidateDatabaseName(name!));
    }

    [Fact]
    public void EscapeSqlIdentifier_escapes_closing_bracket_and_single_quote()
    {
        var escaped = TenantDatabaseProvisioner.EscapeSqlIdentifier("Foo]Bar'Baz");
        Assert.Equal("Foo]]Bar''Baz", escaped);
    }

    [Fact]
    public void EscapeSqlIdentifier_is_safe_to_embed_in_both_bracket_and_literal_contexts()
    {
        var name = "Foo]Bar'Baz";
        var escaped = TenantDatabaseProvisioner.EscapeSqlIdentifier(name);

        // Bracket context: every "]" must appear doubled so the identifier isn't closed early.
        Assert.Equal("[Foo]]Bar''Baz]", $"[{escaped}]");
        // Literal context: every "'" must appear doubled so the string literal isn't closed early.
        Assert.Equal("N'Foo]]Bar''Baz'", $"N'{escaped}'");
    }
}
