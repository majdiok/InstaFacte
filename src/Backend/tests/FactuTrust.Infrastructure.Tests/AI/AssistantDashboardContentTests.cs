using FactuTrust.Application.Features.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class AssistantDashboardContentTests
{
    [Fact]
    public void HasDashboardFence_returns_false_for_null_or_empty()
    {
        Assert.False(AssistantDashboardContent.HasDashboardFence(null));
        Assert.False(AssistantDashboardContent.HasDashboardFence(string.Empty));
        Assert.False(AssistantDashboardContent.HasDashboardFence("   "));
    }

    [Fact]
    public void HasDashboardFence_returns_false_when_no_json_fence_present()
    {
        const string content = "Voici une analyse textuelle sans bloc JSON.";
        Assert.False(AssistantDashboardContent.HasDashboardFence(content));
    }

    [Fact]
    public void HasDashboardFence_returns_true_for_valid_dashboard_fence()
    {
        const string content = """
        Analyse :

        ```json
        {"title":"Stock","sections":[{"type":"chart","title":"S","data":{}}]}
        ```
        """;

        Assert.True(AssistantDashboardContent.HasDashboardFence(content));
    }

    [Fact]
    public void HasDashboardFence_returns_false_when_json_lacks_sections_array()
    {
        const string content = """
        ```json
        {"title":"Stock","sections":"not-an-array"}
        ```
        """;

        Assert.False(AssistantDashboardContent.HasDashboardFence(content));
    }

    [Fact]
    public void HasDashboardFence_returns_false_when_json_lacks_title()
    {
        const string content = """
        ```json
        {"sections":[]}
        ```
        """;

        Assert.False(AssistantDashboardContent.HasDashboardFence(content));
    }

    [Fact]
    public void HasDashboardFence_ignores_unrelated_json_payloads()
    {
        const string content = """
        ```json
        ["follow-up question 1","follow-up question 2"]
        ```
        """;

        Assert.False(AssistantDashboardContent.HasDashboardFence(content));
    }

    [Fact]
    public void HasDashboardFence_skips_malformed_fence_then_finds_valid_one()
    {
        const string content = """
        ```json
        { invalid json here
        ```

        Puis un vrai dashboard :

        ```json
        {"title":"Ventes","sections":[]}
        ```
        """;

        Assert.True(AssistantDashboardContent.HasDashboardFence(content));
    }

    [Fact]
    public void HasDashboardFence_does_not_match_ft_meta_fences()
    {
        const string content = """
        ```ft-meta
        {"suggestedPrompts":["a","b"]}
        ```
        """;

        Assert.False(AssistantDashboardContent.HasDashboardFence(content));
    }

    [Fact]
    public void BuildAppendix_wraps_payload_in_json_fence_with_leading_blank_line()
    {
        const string payload = """{"title":"Stock","sections":[]}""";

        var result = AssistantDashboardContent.BuildAppendix(payload);

        Assert.StartsWith("\n\n```json\n", result);
        Assert.EndsWith("\n```\n", result);
        Assert.Contains(payload, result);
    }

    [Fact]
    public void BuildAppendix_output_is_round_trippable_via_HasDashboardFence()
    {
        const string payload = """{"title":"Stock","sections":[{"type":"chart","title":"S","data":{}}]}""";

        var content = "Analyse courte de l'assistant." + AssistantDashboardContent.BuildAppendix(payload);

        Assert.True(AssistantDashboardContent.HasDashboardFence(content));
    }
}
