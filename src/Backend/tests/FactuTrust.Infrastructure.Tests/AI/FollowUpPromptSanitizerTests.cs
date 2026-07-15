using FactuTrust.Infrastructure.Services.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class FollowUpPromptSanitizerTests
{
    [Fact]
    public void SanitizePromptsJson_AcceptsValidArray()
    {
        var r = FollowUpPromptSanitizer.SanitizePromptsJson("""["a","b"]""");
        Assert.True(r.Success);
        Assert.Contains("a", r.Data, StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizePromptsJson_RejectsHtml()
    {
        var r = FollowUpPromptSanitizer.SanitizePromptsJson("""["<script>"]""");
        Assert.False(r.Success);
    }
}
