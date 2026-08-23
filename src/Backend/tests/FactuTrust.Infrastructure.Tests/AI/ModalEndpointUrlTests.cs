using FactuTrust.Application.Features.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class ModalEndpointUrlTests
{
    [Fact]
    public void TryNormalize_Empty_SucceedsWithNull()
    {
        Assert.True(ModalEndpointUrl.TryNormalize("  ", out var normalized, out var error));
        Assert.Null(normalized);
        Assert.Null(error);
    }

    [Fact]
    public void TryNormalize_HttpsV1_TrimsSlash()
    {
        Assert.True(ModalEndpointUrl.TryNormalize(
            "https://example--ep-kimi-k3-server.us-west.modal.direct/v1/",
            out var normalized,
            out var error));
        Assert.Equal("https://example--ep-kimi-k3-server.us-west.modal.direct/v1", normalized);
        Assert.Null(error);
    }

    [Fact]
    public void TryNormalize_StripsChatCompletionsAndAddsV1()
    {
        Assert.True(ModalEndpointUrl.TryNormalize(
            "https://example--ep-kimi-k3-server.us-west.modal.direct/v1/chat/completions",
            out var normalized,
            out _));
        Assert.Equal("https://example--ep-kimi-k3-server.us-west.modal.direct/v1", normalized);
    }

    [Fact]
    public void TryNormalize_AppendsV1WhenMissing()
    {
        Assert.True(ModalEndpointUrl.TryNormalize(
            "https://example--ep-kimi-k3-server.us-west.modal.direct",
            out var normalized,
            out _));
        Assert.Equal("https://example--ep-kimi-k3-server.us-west.modal.direct/v1", normalized);
    }

    [Fact]
    public void TryNormalize_Http_Fails()
    {
        Assert.False(ModalEndpointUrl.TryNormalize(
            "http://example--ep-kimi-k3-server.us-west.modal.direct/v1",
            out var normalized,
            out var error));
        Assert.Null(normalized);
        Assert.Contains("HTTPS", error, StringComparison.OrdinalIgnoreCase);
    }
}
