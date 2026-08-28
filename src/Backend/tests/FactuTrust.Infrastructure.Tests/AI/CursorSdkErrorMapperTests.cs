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
    public void ToUserMessage_maps_sandbox_unsupported_error()
    {
        var message = CursorSdkErrorMapper.ToUserMessage(
            "Local SDK sandboxing was requested, but sandboxing is not supported in this environment. Disable `local.sandboxOptions.enabled` or remove `~/.cursor/sandbox.json` to run without sandboxing.",
            out var shouldLog);

        Assert.Contains("sandbox", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("administrateur", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("~/.cursor/sandbox.json", message, StringComparison.Ordinal);
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
    public void ToUserMessage_never_echoes_unknown_errors_and_flags_server_side_logging()
    {
        const string raw = "Upstream timeout: connection refused at internal-host:9443";
        var message = CursorSdkErrorMapper.ToUserMessage(raw, out var shouldLog);

        Assert.NotEqual(raw, message);
        Assert.DoesNotContain("internal-host", message, StringComparison.Ordinal);
        Assert.True(shouldLog);
    }

    [Fact]
    public void ToUserMessage_handles_null_as_generic_failure()
    {
        var message = CursorSdkErrorMapper.ToUserMessage(null, out var shouldLog);

        Assert.Equal("L'inférence Cursor a échoué.", message);
        Assert.False(shouldLog);
    }
}
