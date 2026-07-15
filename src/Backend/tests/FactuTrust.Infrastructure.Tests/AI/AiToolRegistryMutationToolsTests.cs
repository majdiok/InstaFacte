using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.AI.Tools;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class AiToolRegistryMutationToolsTests
{
    [Fact]
    public void Compliance_mode_excludes_all_mutation_tools()
    {
        var defs = AiToolRegistry.GetDefinitionsForMode(AssistantMode.Compliance, enableMutationTools: true);
        Assert.All(defs, d => Assert.False(d.IsMutating));
        Assert.DoesNotContain(defs, d => d.Name == "create_product");
        Assert.DoesNotContain(defs, d => d.Name == "record_invoice_payment");
    }

    [Fact]
    public void Default_mode_with_mutations_disabled_hides_mutation_tools_but_keeps_reads()
    {
        var defs = AiToolRegistry.GetDefinitionsForMode(AssistantMode.Default, enableMutationTools: false);
        Assert.DoesNotContain(defs, d => d.Name == "create_product");
        Assert.DoesNotContain(defs, d => d.Name == "accept_quote");
        Assert.Contains(defs, d => d.Name == "get_sales_revenue");
        Assert.Contains(defs, d => d.Name == "get_product_by_id");
    }

    [Fact]
    public void Default_mode_with_mutations_enabled_includes_new_tools()
    {
        var defs = AiToolRegistry.GetDefinitionsForMode(AssistantMode.Default, enableMutationTools: true);
        Assert.Contains(defs, d => d.Name == "create_product" && d.IsMutating);
        Assert.Contains(defs, d => d.Name == "get_product_by_id" && !d.IsMutating);
        Assert.Contains(defs, d => d.Name == "record_invoice_payment");
    }

    [Fact]
    public void ScreenAnalysis_mode_includes_dashboard_and_read_tools_only_subset()
    {
        var defs = AiToolRegistry.GetDefinitionsForMode(AssistantMode.ScreenAnalysis, enableMutationTools: true);
        Assert.Contains(defs, d => d.Name == "generate_dashboard_config");
        Assert.Contains(defs, d => d.Name == "propose_follow_up_prompts");
        Assert.Contains(defs, d => d.Name == "get_sales_revenue");
        Assert.DoesNotContain(defs, d => d.Name == "create_product");
    }

    [Fact]
    public void GetToolDefinition_is_case_insensitive()
    {
        Assert.NotNull(AiToolRegistry.GetToolDefinition("CREATE_PRODUCT"));
        Assert.Equal("create_product", AiToolRegistry.GetToolDefinition("Create_Product")!.Name);
    }
}
