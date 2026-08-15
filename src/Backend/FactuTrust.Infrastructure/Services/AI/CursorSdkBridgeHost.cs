using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.Channels.Bridge;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.AI;

/// <summary>
/// Processus Node @cursor/sdk en 127.0.0.1. Ne démarre que si <see cref="CursorSdkSettings.Enabled"/>.
/// </summary>
public sealed class CursorSdkBridgeHost : ICursorAgentClient, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly CursorSdkSettings _settings;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<CursorSdkBridgeHost> _logger;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly string _bridgeDirectory;

    private Process? _process;
    private HttpClient? _http;
    private int _port;
    private bool _stopping;
    private int _disposed;

    public CursorSdkBridgeHost(
        IOptions<CursorSdkSettings> settings,
        IHostEnvironment environment,
        ILogger<CursorSdkBridgeHost> logger)
    {
        _settings = settings.Value;
        _environment = environment;
        _logger = logger;
        var outputDir = Path.Combine(AppContext.BaseDirectory, "CursorSdkBridge");
        var contentRootDir = Path.Combine(_environment.ContentRootPath, "CursorSdkBridge");
        var sourceDir = GetBakedSourceDirectory();
        if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
            sourceDir = Directory.Exists(contentRootDir) ? contentRootDir : null;
        _bridgeDirectory = BridgeDirectoryResolver.Resolve(
            _settings.BridgeDirectory,
            outputDir,
            sourceDir,
            dir => Directory.Exists(Path.Combine(dir, "node_modules")));
    }

    public bool IsEnabled => _settings.Enabled;

    private static string? GetBakedSourceDirectory()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var value = assembly
                .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), inherit: false)
                .OfType<System.Reflection.AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "CursorSdkBridgeSourceDirectory")?.Value;
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    public async Task TryAutoStartAsync()
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Pont Cursor SDK : désactivé (CursorSdk:Enabled=false).");
            return;
        }

        try
        {
            await StartProcessAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pont Cursor SDK : démarrage automatique impossible.");
        }
    }

    public async Task ShutdownAsync()
    {
        _stopping = true;
        if (Volatile.Read(ref _disposed) != 0)
            return;
        await StopProcessAsync();
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        ProbeHealthAsync(cancellationToken);

    public async Task<IReadOnlyList<CursorRemoteModelInfo>> ListModelsAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        if (!await ProbeHealthAsync(cancellationToken))
            return Array.Empty<CursorRemoteModelInfo>();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_settings.StartupTimeoutSeconds, 5, 60)));
        using var response = await _http!.PostAsJsonAsync(
            "/models",
            new { apiKey },
            JsonOptions,
            cts.Token);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Pont Cursor SDK : list models HTTP {Status}", (int)response.StatusCode);
            return Array.Empty<CursorRemoteModelInfo>();
        }

        var payload = await response.Content.ReadFromJsonAsync<CursorModelsResponse>(JsonOptions, cts.Token);
        return MapModels(payload?.Models);
    }

    public async IAsyncEnumerable<CursorAgentStreamEvent> RunChatAsync(
        CursorChatRunRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!await ProbeHealthAsync(cancellationToken))
        {
            yield return new CursorAgentStreamEvent("error", Error: "Le pont Cursor SDK est indisponible.");
            yield break;
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, "/runs")
        {
            Content = JsonContent.Create(ToRunBody(request), options: JsonOptions)
        };
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_settings.RunTimeoutSeconds, 15, 600)));

        HttpResponseMessage? response = null;
        Exception? sendError = null;
        try
        {
            response = await _http!.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        }
        catch (Exception ex)
        {
            sendError = ex;
        }

        if (sendError is not null)
        {
            yield return new CursorAgentStreamEvent("error", Error: sendError.Message, IsRetryable: true);
            yield break;
        }

        using var httpResponse = response!;
        if (!httpResponse.IsSuccessStatusCode)
        {
            var err = await httpResponse.Content.ReadAsStringAsync(cts.Token);
            yield return new CursorAgentStreamEvent("error", Error: err);
            yield break;
        }

        await using var stream = await httpResponse.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream && !cts.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cts.Token);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal))
                continue;
            var json = line["data:".Length..].Trim();
            CursorAgentStreamEvent? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<CursorAgentStreamEvent>(json, JsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            if (parsed is not null)
                yield return parsed;
        }
    }

    public async Task<string> ExtractAsync(
        CursorExtractRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await ProbeHealthAsync(cancellationToken))
            throw new InvalidOperationException("Le pont Cursor SDK est indisponible.");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_settings.ExtractTimeoutSeconds, 15, 600)));
        using var response = await _http!.PostAsJsonAsync("/extract", ToExtractBody(request), JsonOptions, cts.Token);
        var body = await response.Content.ReadAsStringAsync(cts.Token);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(body) ? $"HTTP {(int)response.StatusCode}" : body);

        var parsed = JsonSerializer.Deserialize<CursorExtractResponse>(body, JsonOptions);
        return parsed?.Text ?? string.Empty;
    }

    private async Task<bool> ProbeHealthAsync(CancellationToken cancellationToken)
    {
        if (!_settings.Enabled || _http is null)
            return false;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            using var response = await _http.GetAsync("/health", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task StartProcessAsync()
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            if (_stopping || _process is { HasExited: false })
                return;

            if (!Directory.Exists(Path.Combine(_bridgeDirectory, "node_modules")))
            {
                _logger.LogWarning(
                    "Pont Cursor SDK : node_modules absent dans {Dir}. Exécutez `npm ci` (Node >= 22.13).",
                    _bridgeDirectory);
                return;
            }

            var bridgeJs = Path.Combine(_bridgeDirectory, "bridge.js");
            if (!File.Exists(bridgeJs))
            {
                _logger.LogWarning("Pont Cursor SDK : bridge.js introuvable dans {Dir}.", _bridgeDirectory);
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = _settings.NodeExecutablePath,
                WorkingDirectory = _bridgeDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false)
            };
            psi.ArgumentList.Add("bridge.js");
            psi.Environment["CURSOR_SDK_BRIDGE_PORT"] = Math.Max(0, _settings.BridgePort).ToString();

            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            try
            {
                process.Start();
            }
            catch (Win32Exception ex)
            {
                _logger.LogError(ex, "Pont Cursor SDK : Node introuvable ({Node}).", _settings.NodeExecutablePath);
                process.Dispose();
                return;
            }

            _process = process;
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    _logger.LogWarning("Pont Cursor SDK stderr: {Line}", e.Data);
            };
            process.BeginErrorReadLine();

            var ready = await WaitForReadyAsync(process, TimeSpan.FromSeconds(Math.Clamp(_settings.StartupTimeoutSeconds, 5, 60)));
            if (ready is null)
            {
                _logger.LogWarning("Pont Cursor SDK : pas de ligne READY (timeout).");
                KillProcessUnlocked();
                return;
            }

            _port = ready.Value;
            _http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_port}/") };
            _http.Timeout = TimeSpan.FromSeconds(Math.Max(_settings.RunTimeoutSeconds, _settings.ExtractTimeoutSeconds) + 30);
            _logger.LogInformation("Pont Cursor SDK : prêt sur 127.0.0.1:{Port} (PID {Pid}).", _port, process.Id);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private static async Task<int?> WaitForReadyAsync(Process process, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline && !process.HasExited)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;
            var readTask = process.StandardOutput.ReadLineAsync();
            var completed = await Task.WhenAny(readTask, Task.Delay(remaining));
            if (completed != readTask)
                break;
            var line = await readTask;
            if (line is null)
                break;
            if (line.StartsWith("READY ", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(line["READY ".Length..].Trim(), out var port)
                && port > 0)
                return port;
        }

        return null;
    }

    private async Task StopProcessAsync()
    {
        try
        {
            await _lifecycleLock.WaitAsync();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            KillProcessUnlocked();
        }
        finally
        {
            try
            {
                _lifecycleLock.Release();
            }
            catch (ObjectDisposedException)
            {
                // already tearing down
            }
        }
    }

    private void KillProcessUnlocked()
    {
        _http?.Dispose();
        _http = null;
        var process = _process;
        _process = null;
        if (process is null)
            return;
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(3000);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Pont Cursor SDK : arrêt du process.");
        }
        finally
        {
            process.Dispose();
        }
    }

    private static object ToRunBody(CursorChatRunRequest request) => new
    {
        apiKey = request.ApiKey,
        model = new
        {
            id = request.Model.ProviderModelId,
            @params = request.Model.Params.Select(p => new { id = p.Id, value = p.Value }).ToList()
        },
        userText = request.UserText,
        images = request.Images.Select(i => new { data = i.Data, mimeType = i.MimeType }).ToList(),
        tools = request.Tools.Select(t => new
        {
            name = t.Name,
            description = t.Description,
            inputSchema = t.InputSchema
        }).ToList(),
        callbackUrl = request.CallbackUrl,
        callbackToken = request.CallbackToken,
        scratchDirectory = request.ScratchDirectory
    };

    private static object ToExtractBody(CursorExtractRequest request) => new
    {
        apiKey = request.ApiKey,
        model = new
        {
            id = request.Model.ProviderModelId,
            @params = request.Model.Params.Select(p => new { id = p.Id, value = p.Value }).ToList()
        },
        systemPrompt = request.SystemPrompt,
        userText = request.UserText,
        images = request.Images.Select(i => new { data = i.Data, mimeType = i.MimeType }).ToList(),
        scratchDirectory = request.ScratchDirectory
    };

    private static IReadOnlyList<CursorRemoteModelInfo> MapModels(IReadOnlyList<CursorSdkModelDto>? models)
    {
        if (models is null || models.Count == 0)
            return Array.Empty<CursorRemoteModelInfo>();

        var list = new List<CursorRemoteModelInfo>(models.Count);
        foreach (var model in models)
        {
            if (string.IsNullOrWhiteSpace(model.Id))
                continue;
            list.Add(new CursorRemoteModelInfo(
                model.Id,
                model.DisplayName ?? model.Id,
                model.Description,
                model.Aliases ?? new List<string>(),
                (model.Parameters ?? []).Select(p => new CursorRemoteModelParameter(
                    p.Id,
                    p.DisplayName,
                    (p.Values ?? []).Select(v => new CursorRemoteModelParameterValue(v.Value, v.DisplayName)).ToList()
                )).ToList(),
                (model.Variants ?? []).Select(v => new CursorRemoteModelVariant(
                    (v.Params ?? []).Select(p => new CursorModelParam(p.Id, p.Value)).ToList(),
                    v.DisplayName ?? model.DisplayName ?? model.Id,
                    v.Description,
                    v.IsDefault
                )).ToList()));
        }

        return list;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _stopping = true;
        await StopProcessAsync();
        _lifecycleLock.Dispose();
    }

    private sealed record CursorModelsResponse(List<CursorSdkModelDto>? Models);

    private sealed record CursorSdkModelDto(
        string Id,
        string? DisplayName,
        string? Description,
        List<string>? Aliases,
        List<CursorSdkParamDto>? Parameters,
        List<CursorSdkVariantDto>? Variants);

    private sealed record CursorSdkParamDto(
        string Id,
        string? DisplayName,
        List<CursorSdkParamValueDto>? Values);

    private sealed record CursorSdkParamValueDto(string Value, string? DisplayName);

    private sealed record CursorSdkVariantDto(
        List<CursorSdkParamKvDto>? Params,
        string? DisplayName,
        string? Description,
        bool IsDefault);

    private sealed record CursorSdkParamKvDto(string Id, string Value);

    private sealed record CursorExtractResponse(string? Text, string? Status);
}
