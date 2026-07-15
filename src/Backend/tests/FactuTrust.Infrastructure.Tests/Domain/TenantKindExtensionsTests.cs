using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class TenantKindExtensionsTests
{
    [Theory]
    [InlineData("AccountingFirm", TenantKind.AccountingFirm)]
    [InlineData("accountingFirm", TenantKind.AccountingFirm)]
    [InlineData("Company", TenantKind.Company)]
    [InlineData("company", TenantKind.Company)]
    [InlineData("1", TenantKind.AccountingFirm)]
    [InlineData("0", TenantKind.Company)]
    public void FromApiValue_parses_known_values(string input, TenantKind expected)
    {
        Assert.Equal(expected, TenantKindExtensions.FromApiValue(input));
    }

    [Fact]
    public void FromApiValue_null_or_empty_defaults_to_company()
    {
        Assert.Equal(TenantKind.Company, TenantKindExtensions.FromApiValue(null));
        Assert.Equal(TenantKind.Company, TenantKindExtensions.FromApiValue(""));
    }
}
