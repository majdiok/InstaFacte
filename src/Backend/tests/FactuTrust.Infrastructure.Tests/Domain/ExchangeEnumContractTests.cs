using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Smoke checks that exchange enum numeric values remain stable for API clients
/// that may still send integers alongside PascalCase strings.
/// </summary>
public sealed class ExchangeEnumContractTests
{
    [Fact]
    public void Thread_and_visibility_enum_values_are_stable()
    {
        Assert.Equal(0, (int)ExchangeThreadStatus.Open);
        Assert.Equal(1, (int)ExchangeThreadStatus.Closed);
        Assert.Equal(0, (int)ExchangeMessageVisibility.ClientVisible);
        Assert.Equal(1, (int)ExchangeMessageVisibility.InternalNote);
    }
}
