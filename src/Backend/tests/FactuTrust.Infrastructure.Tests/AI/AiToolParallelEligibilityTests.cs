using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class AiToolParallelEligibilityTests
{
    private static OllamaSettings Settings(bool parallelTools = true, bool parallelDb = true) => new()
    {
        EnableParallelToolCalls = parallelTools,
        EnableParallelDbTools = parallelDb
    };

    [Theory]
    [InlineData("resolve_reporting_period")]
    [InlineData("generate_dashboard_config")]
    public void IsEligible_NoDbTool_WhenParallelToolCallsEnabled(string toolName)
    {
        Assert.True(AiToolParallelEligibility.IsEligible(toolName, Settings()));
    }

    [Fact]
    public void IsEligible_NoDbTool_WhenParallelToolCallsDisabled_ReturnsFalse()
    {
        Assert.False(AiToolParallelEligibility.IsEligible(
            "resolve_reporting_period",
            Settings(parallelTools: false)));
    }

    [Theory]
    [InlineData("get_sales_revenue")]
    [InlineData("get_client_aging")]
    [InlineData("get_stock_snapshot")]
    public void IsEligible_ReadOnlyDbTool_WhenParallelDbEnabled(string toolName)
    {
        Assert.True(AiToolParallelEligibility.IsEligible(toolName, Settings()));
    }

    [Fact]
    public void IsEligible_ReadOnlyDbTool_WhenParallelDbDisabled_ReturnsFalse()
    {
        Assert.False(AiToolParallelEligibility.IsEligible(
            "get_sales_revenue",
            Settings(parallelDb: false)));
    }

    [Theory]
    [InlineData("create_product")]
    [InlineData("update_product")]
    public void IsEligible_MutationTool_ReturnsFalse(string toolName)
    {
        Assert.False(AiToolParallelEligibility.IsEligible(toolName, Settings()));
    }
}