using FactuTrust.Application.Features.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class CursorSdkErrorMapperTests
{
    [Fact]
    public void ToUserMessage_maps_disallowed_tools_configuration_error()
    {
        var message = CursorSdkErrorMapper.ToUserMessage(
            "Unknown tool name(s) in disallowedTools: write. Valid tool names: edit, read",
            out var shouldLog);

        Assert.Contains("administrateur", message, StringComparison.OrdinalIgnoreCase);
        Assert.True(shouldLog);
    }

    [Fact]
    public void ToUserMessage_maps_invalid_api_key()
    {
        var message = CursorSdkErrorMapper.ToUserMessage("Invalid User API Key", out var shouldLog);

        Assert.Contains("Clé API Cursor", message);
        Assert.False(shouldLog);
    }

    [Fact]
    public void ToUserMessage_preserves_unknown_errors()
    {
        const string raw = "Upstream timeout";
        var message = CursorSdkErrorMapper.ToUserMessage(raw, out var shouldLog);

        Assert.Equal(raw, message);
        Assert.False(shouldLog);
    }

    [Fact]
    public void ToUserMessage_handles_null_as_generic_failure()
    {
        var message = CursorSdkErrorMapper.ToUserMessage(null, out var shouldLog);

        Assert.Equal("L'inférence Cursor a échoué.", message);
        Assert.False(shouldLog);
    }
}
