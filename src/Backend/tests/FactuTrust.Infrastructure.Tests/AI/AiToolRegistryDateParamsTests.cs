using FactuTrust.Application.Features.AI.Tools;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class AiToolRegistryDateParamsTests
{
    [Theory]
    [InlineData("get_sales_revenue")]
    [InlineData("get_client_payments")]
    [InlineData("get_commercial_profit")]
    [InlineData("get_basket_metrics")]
    public void ReadOnlyDateTools_DoNotRequireFromToDates(string toolName)
    {
        var def = AiToolRegistry.GetToolDefinition(toolName);
        Assert.NotNull(def);
        Assert.DoesNotContain(def.RequiredParameters, p => p == "from_date");
        Assert.DoesNotContain(def.RequiredParameters, p => p == "to_date");
        Assert.True(def.Parameters.ContainsKey("preset"));
    }
}