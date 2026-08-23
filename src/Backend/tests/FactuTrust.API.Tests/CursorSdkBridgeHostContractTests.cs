using FactuTrust.Application.Configuration;
using FactuTrust.Infrastructure.Services.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.API.Tests;

public sealed class CursorSdkBridgeHostContractTests
{
    [Fact]
    public async Task DisposeAsync_WhenDisabled_IsIdempotent()
    {
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(x => x.ContentRootPath).Returns(Path.GetTempPath());
        env.SetupGet(x => x.ApplicationName).Returns("tests");
        env.SetupGet(x => x.EnvironmentName).Returns("Development");

        var host = new CursorSdkBridgeHost(
            Options.Create(new CursorSdkSettings { Enabled = false }),
            env.Object,
            NullLogger<CursorSdkBridgeHost>.Instance);

        await host.ShutdownAsync();
        await host.DisposeAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task FakeBridge_HealthModelsExtractAndRuns_MatchContract()
    {
        var node = FindNode();
        if (node is null)
            return;

        var dir = Path.Combine(Path.GetTempPath(), "ft-cursor-bridge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "node_modules"));
        await File.WriteAllTextAsync(Path.Combine(dir, "bridge.js"), FakeBridgeSource);

        var env = new Mock<IHostEnvironment>();
        env.SetupGet(x => x.ContentRootPath).Returns(dir);
        env.SetupGet(x => x.ApplicationName).Returns("tests");
        env.SetupGet(x => x.EnvironmentName).Returns("Development");

        await using var host = new CursorSdkBridgeHost(
            Options.Create(new CursorSdkSettings
            {
                Enabled = true,
                NodeExecutablePath = node,
                BridgeDirectory = dir,
                StartupTimeoutSeconds = 15,
                RunTimeoutSeconds = 15,
                ExtractTimeoutSeconds = 15
            }),
            env.Object,
            NullLogger<CursorSdkBridgeHost>.Instance);

        try
        {
            await host.TryAutoStartAsync();
            Assert.True(await host.IsAvailableAsync());

            var models = await host.ListModelsAsync("cursor_test");
            Assert.Contains(models, m => m.Id == "composer-2.5");

            var extracted = await host.ExtractAsync(new(
                "cursor_test",
                FactuTrust.Application.Features.AI.ModelRef.Parse("cursor:composer-2.5"),
                "system",
                "user",
                Array.Empty<FactuTrust.Application.Common.Interfaces.Services.CursorImagePayload>(),
                Path.Combine(dir, "scratch-extract")));
            Assert.Contains("FAC-1", extracted);

            var events = new List<FactuTrust.Application.Common.Interfaces.Services.CursorAgentStreamEvent>();
            await foreach (var ev in host.RunChatAsync(new(
                "cursor_test",
                FactuTrust.Application.Features.AI.ModelRef.Parse("cursor:composer-2.5"),
                "bonjour",
                Array.Empty<FactuTrust.Application.Common.Interfaces.Services.CursorImagePayload>(),
                Array.Empty<FactuTrust.Application.Common.Interfaces.Services.CursorToolSpec>(),
                Guid.NewGuid(),
                "http://127.0.0.1:9/internal/ai/cursor-tools/" + Guid.NewGuid(),
                "token",
                Path.Combine(dir, "scratch-run"))))
            {
                events.Add(ev);
            }

            Assert.Contains(events, e => e.Type == "assistant_text");
            Assert.Contains(events, e => e.Type is "done" or "started");
        }
        finally
        {
            await host.ShutdownAsync();
            try { Directory.Delete(dir, true); } catch { /* temp */ }
        }
    }

    [Fact]
    public async Task RealBridge_RunChat_DoesNotFailOnDisallowedToolsConfiguration()
    {
        var node = FindNode();
        var bridgeDir = FindRealBridgeDirectory();
        if (node is null || bridgeDir is null)
            return;

        var env = new Mock<IHostEnvironment>();
        env.SetupGet(x => x.ContentRootPath).Returns(bridgeDir);
        env.SetupGet(x => x.ApplicationName).Returns("tests");
        env.SetupGet(x => x.EnvironmentName).Returns("Development");

        await using var host = new CursorSdkBridgeHost(
            Options.Create(new CursorSdkSettings
            {
                Enabled = true,
                NodeExecutablePath = node,
                BridgeDirectory = bridgeDir,
                StartupTimeoutSeconds = 30,
                RunTimeoutSeconds = 30,
                ExtractTimeoutSeconds = 30
            }),
            env.Object,
            NullLogger<CursorSdkBridgeHost>.Instance);

        await host.TryAutoStartAsync();
        if (!await host.IsAvailableAsync())
            return;

        var events = new List<FactuTrust.Application.Common.Interfaces.Services.CursorAgentStreamEvent>();
        await foreach (var ev in host.RunChatAsync(new(
            "cursor_test_key",
            FactuTrust.Application.Features.AI.ModelRef.Parse("cursor:composer-2.5"),
            "bonjour",
            Array.Empty<FactuTrust.Application.Common.Interfaces.Services.CursorImagePayload>(),
            Array.Empty<FactuTrust.Application.Common.Interfaces.Services.CursorToolSpec>(),
            Guid.NewGuid(),
            "http://127.0.0.1:9/internal/ai/cursor-tools/" + Guid.NewGuid(),
            "token",
            Path.Combine(bridgeDir, "scratch-integration"))))
        {
            events.Add(ev);
        }

        var firstError = events.FirstOrDefault(e => e.Type == "error");
        if (firstError?.Error is not null)
        {
            Assert.DoesNotContain(
                "Unknown tool name(s) in disallowedTools",
                firstError.Error,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "sandboxing is not supported",
                firstError.Error,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string? FindRealBridgeDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Backend", "FactuTrust.API", "CursorSdkBridge");
            if (File.Exists(Path.Combine(candidate, "bridge.js"))
                && File.Exists(Path.Combine(candidate, "bridge-agent-options.js"))
                && Directory.Exists(Path.Combine(candidate, "node_modules", "@cursor", "sdk")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static string? FindNode()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "node",
                Arguments = "-v",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = System.Diagnostics.Process.Start(psi);
            process?.WaitForExit(5000);
            return process is { ExitCode: 0 } ? "node" : null;
        }
        catch
        {
            return null;
        }
    }

    private const string FakeBridgeSource = """
import http from "node:http";

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url || "/", "http://127.0.0.1");
  if (req.method === "GET" && url.pathname === "/health") {
    res.writeHead(200, { "Content-Type": "application/json" });
    res.end(JSON.stringify({ ok: true, sdk: true, node: process.version }));
    return;
  }
  if (req.method === "POST" && url.pathname === "/models") {
    res.writeHead(200, { "Content-Type": "application/json" });
    res.end(JSON.stringify({
      models: [{ id: "composer-2.5", displayName: "Composer 2.5", variants: [] }]
    }));
    return;
  }
  if (req.method === "POST" && url.pathname === "/extract") {
    res.writeHead(200, { "Content-Type": "application/json" });
    res.end(JSON.stringify({ text: "{\"invoiceNumber\":\"FAC-1\"}", status: "finished" }));
    return;
  }
  if (req.method === "POST" && url.pathname === "/runs") {
    res.writeHead(200, {
      "Content-Type": "text/event-stream; charset=utf-8",
      "Cache-Control": "no-cache"
    });
    res.write(`data: ${JSON.stringify({ type: "started", agentId: "ag-1", runId: "run-1" })}\n\n`);
    res.write(`data: ${JSON.stringify({ type: "assistant_text", text: "ok" })}\n\n`);
    res.write(`data: ${JSON.stringify({ type: "done", status: "finished", text: "ok" })}\n\n`);
    res.end();
    return;
  }
  res.writeHead(404);
  res.end();
});

server.listen(0, "127.0.0.1", () => {
  process.stdout.write(`READY ${server.address().port}\n`);
});
""";
}
