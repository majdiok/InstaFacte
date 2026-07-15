using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Channels.Bridge;
using FactuTrust.Application.Features.Channels.DTOs;
using Hangfire;
using Microsoft.Extensions.Options;

namespace FactuTrust.API.Services.Channels;

/// <summary>
/// Hôte du pont WhatsApp (processus Node enfant piloté par l'API). Singleton : une seule session.
///
/// Cycle : pré-checks (node / node_modules) → <c>Process</c> Node (stdin/stdout redirigés, UTF-8) →
/// boucle de lecture stdout (protocole <see cref="WhatsAppBridgeProtocol"/>) → messages filtrés
/// (<see cref="WhatsAppBridgeMessageFilter"/>) et mis en file Hangfire vers
/// <see cref="ChannelInboundOrchestrator"/> (reprise exacte de l'ancien webhook) → réponses via
/// stdin corrélées par identifiant. Sortie inattendue → redémarrage backoff exponentiel. Arrêt
/// gracieux : « shutdown » puis kill de l'arbre de processus (Chromium compris).
///
/// Confidentialité : jamais de contenu de message ni de QR brut dans les logs (longueurs/ids seulement).
/// </summary>
public sealed class WhatsAppBridgeHost : IWhatsAppBridge, IAsyncDisposable
{
    private readonly ChannelsSettings _settings;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WhatsAppBridgeHost> _logger;
    private readonly string _bridgeDirectory;
    private readonly string _sessionDirectory;

    private readonly SemaphoreSlim _stdinLock = new(1, 1);
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pendingSends = new();

    private readonly object _statusLock = new();
    private BridgeStatus _status;

    private Process? _process;
    private CancellationTokenSource? _processCts;
    private volatile bool _stopping;
    private int _restartAttempts;
    private long _sendCounter;
    private int _disposed;

    public WhatsAppBridgeHost(
        IOptions<ChannelsSettings> settings,
        IServiceScopeFactory scopeFactory,
        ILogger<WhatsAppBridgeHost> logger)
    {
        _settings = settings.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
        // Résolution intelligente : configuré > sortie avec node_modules > source dev avec
        // node_modules (le build ne copie pas node_modules — Chromium lourd) > repli sortie.
        _bridgeDirectory = BridgeDirectoryResolver.Resolve(
            _settings.BridgeDirectory,
            Path.Combine(AppContext.BaseDirectory, "WhatsAppBridge"),
            GetBakedSourceDirectory(),
            dir => Directory.Exists(Path.Combine(dir, "node_modules")));
        _sessionDirectory = string.IsNullOrWhiteSpace(_settings.SessionDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "whatsapp-session")
            : _settings.SessionDirectory;
        _status = new BridgeStatus(BridgeState.Stopped, null, null, DateTime.UtcNow);
    }

    /// <summary>
    /// Chemin source du pont baké au build (attribut d'assembly « WhatsAppBridgeSourceDirectory »,
    /// cf. FactuTrust.API.csproj). Inexistant sur une machine de production : le résolveur replie
    /// alors sur le dossier de sortie.
    /// </summary>
    private static string? GetBakedSourceDirectory() =>
        typeof(WhatsAppBridgeHost).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), inherit: false)
            .OfType<System.Reflection.AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "WhatsAppBridgeSourceDirectory")?.Value;

    public BridgeStatus Status
    {
        get { lock (_statusLock) return _status; }
    }

    // ── Cycle de vie ──

    /// <summary>Démarrage automatique (appelé par le hosted service quand AutoStart est actif).</summary>
    public Task TryAutoStartAsync() => StartProcessAsync();

    public async Task RestartAsync()
    {
        if (!FeatureEnabled)
        {
            ApplyState(BridgeState.Stopped);
            return;
        }

        await StopProcessAsync(graceful: true);
        Interlocked.Exchange(ref _restartAttempts, 0);
        await StartProcessAsync();
    }

    public async Task LogoutAsync()
    {
        if (!FeatureEnabled)
            return;

        try
        {
            await WriteLineAsync(WhatsAppBridgeProtocol.SerializeLogout(), CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pont WhatsApp : commande logout non transmise.");
        }

        // whatsapp-web.js émet « disconnected » après logout : on redémarre pour réafficher un QR neuf.
        await Task.Delay(TimeSpan.FromSeconds(1));
        await RestartAsync();
    }

    /// <summary>Arrête le processus du pont sans libérer les sémaphores (appelé par le hosted service).</summary>
    public async Task ShutdownAsync()
    {
        _stopping = true;
        await StopProcessAsync(graceful: true);
    }

    public async ValueTask DisposeAsync()
    {
        // Idempotent : le hosted service (StopAsync) ET le conteneur DI disposent le singleton.
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        await ShutdownAsync();
        _stdinLock.Dispose();
        _lifecycleLock.Dispose();
    }

    private bool FeatureEnabled => _settings.Enabled && _settings.WhatsAppEnabled;

    private async Task StartProcessAsync()
    {
        if (!FeatureEnabled || _stopping)
        {
            ApplyState(BridgeState.Stopped);
            return;
        }

        await _lifecycleLock.WaitAsync();
        try
        {
            if (_stopping || _process is { HasExited: false })
                return;

            if (!Directory.Exists(Path.Combine(_bridgeDirectory, "node_modules")))
            {
                ApplyState(BridgeState.DependenciesMissing,
                    $"Dépendances Node absentes. Ouvrez un terminal dans « {_bridgeDirectory} » " +
                    "et exécutez « npm ci » (une seule fois), puis cliquez sur Redémarrer.");
                _logger.LogWarning("Pont WhatsApp : node_modules absent dans {Dir}. Exécutez `npm ci` puis Redémarrer.", _bridgeDirectory);
                return;
            }

            var bridgeJs = Path.Combine(_bridgeDirectory, "bridge.js");
            if (!File.Exists(bridgeJs))
            {
                ApplyState(BridgeState.DependenciesMissing, $"bridge.js introuvable dans {_bridgeDirectory}");
                _logger.LogWarning("Pont WhatsApp : bridge.js introuvable dans {Dir}.", _bridgeDirectory);
                return;
            }

            Directory.CreateDirectory(_sessionDirectory);

            var psi = new ProcessStartInfo
            {
                FileName = _settings.NodeExecutablePath,
                WorkingDirectory = _bridgeDirectory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false),
                StandardInputEncoding = new UTF8Encoding(false)
            };
            psi.ArgumentList.Add("bridge.js");
            psi.Environment["WA_SESSION_DIR"] = _sessionDirectory;

            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            try
            {
                process.Start();
            }
            catch (Win32Exception ex)
            {
                ApplyState(BridgeState.NodeMissing, $"Node introuvable ({_settings.NodeExecutablePath}) : {ex.Message}");
                _logger.LogError(ex, "Pont WhatsApp : impossible de lancer Node ({Node}).", _settings.NodeExecutablePath);
                process.Dispose();
                return;
            }

            var cts = new CancellationTokenSource();
            _process = process;
            _processCts = cts;
            ApplyState(BridgeState.Initializing);
            _logger.LogInformation(
                "Pont WhatsApp : processus Node démarré (PID {Pid}, pont : {Dir}).", process.Id, _bridgeDirectory);

            _ = Task.Run(() => ReadStdoutLoopAsync(process, cts.Token));
            _ = Task.Run(() => ReadStderrLoopAsync(process, cts.Token));
            _ = Task.Run(() => MonitorExitAsync(process, cts));
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private async Task StopProcessAsync(bool graceful)
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            _processCts?.Cancel();
            var process = _process;
            _process = null;
            if (process is null)
                return;

            try
            {
                if (!process.HasExited)
                {
                    if (graceful)
                    {
                        await TryWriteShutdownAsync(process);
                        if (!await WaitForExitAsync(process, TimeSpan.FromSeconds(5)))
                            process.Kill(entireProcessTree: true);
                    }
                    else
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Pont WhatsApp : arrêt du processus.");
            }
            finally
            {
                process.Dispose();
            }

            // Réveille les envois en attente pour éviter des timeouts inutiles.
            foreach (var pending in _pendingSends.Values)
                pending.TrySetResult(false);
            _pendingSends.Clear();
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private async Task MonitorExitAsync(Process process, CancellationTokenSource cts)
    {
        try
        {
            await process.WaitForExitAsync();
        }
        catch
        {
            // ignoré : la sortie est traitée ci-dessous
        }

        if (_stopping || cts.IsCancellationRequested)
            return; // arrêt intentionnel : pas de redémarrage

        var attempt = Interlocked.Increment(ref _restartAttempts);
        var exponent = Math.Min(attempt - 1, 10);
        var delaySeconds = Math.Min(
            _settings.RestartMaxBackoffSeconds,
            _settings.RestartMinBackoffSeconds * (int)Math.Pow(2, exponent));
        delaySeconds = Math.Max(1, delaySeconds);

        ApplyState(BridgeState.Disconnected, $"Processus terminé, redémarrage dans {delaySeconds}s.");
        _logger.LogWarning("Pont WhatsApp : processus terminé, redémarrage dans {Delay}s (tentative {Attempt}).",
            delaySeconds, attempt);

        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        if (!_stopping)
            await StartProcessAsync();
    }

    // ── Lecture stdout / stderr ──

    private async Task ReadStdoutLoopAsync(Process process, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await process.StandardOutput.ReadLineAsync(ct);
                if (line is null)
                    break; // stdout fermé (processus terminé)
                HandleLine(line);
            }
        }
        catch (OperationCanceledException)
        {
            // arrêt normal
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Pont WhatsApp : lecture stdout interrompue.");
        }
    }

    private async Task ReadStderrLoopAsync(Process process, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await process.StandardError.ReadLineAsync(ct);
                if (line is null)
                    break;
                _logger.LogDebug("Pont WhatsApp (stderr) : {Line}", line);
            }
        }
        catch (OperationCanceledException)
        {
            // arrêt normal
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Pont WhatsApp : lecture stderr interrompue.");
        }
    }

    private void HandleLine(string line)
    {
        var evt = WhatsAppBridgeProtocol.ParseLine(line);
        switch (evt)
        {
            case null:
                _logger.LogDebug("Pont WhatsApp : ligne stdout ignorée (non exploitable).");
                break;
            case BridgeStateEvent stateEvent:
                ApplyState(stateEvent.State);
                if (stateEvent.State == BridgeState.Ready)
                    Interlocked.Exchange(ref _restartAttempts, 0);
                break;
            case BridgeQrEvent qrEvent:
                ApplyQr(qrEvent.Qr);
                break;
            case BridgeSentAck ack:
                if (_pendingSends.TryRemove(ack.Id, out var tcs))
                    tcs.TrySetResult(ack.Ok);
                break;
            case BridgeMessageEvent message:
                HandleInboundMessage(message);
                break;
        }
    }

    private void HandleInboundMessage(BridgeMessageEvent message)
    {
        if (!WhatsAppBridgeMessageFilter.ShouldForward(
                message.FromMe, message.IsStatus, message.ExternalUserId, message.MessageType, message.Text))
        {
            return;
        }

        var text = message.Text.Length > _settings.MaxInboundMessageLength
            ? message.Text[.._settings.MaxInboundMessageLength]
            : message.Text;

        var dto = new ChannelInboundMessageDto
        {
            Channel = "whatsapp",
            ExternalMessageId = message.ExternalMessageId,
            ExternalUserId = message.ExternalUserId,
            ExternalChatId = message.ExternalChatId,
            Text = text,
            SentAtUnixSeconds = message.SentAtUnixSeconds
        };

        try
        {
            // IBackgroundJobClient résolu dans un scope (robuste quelle que soit sa durée de vie).
            using var scope = _scopeFactory.CreateScope();
            var jobs = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
            jobs.Enqueue<ChannelInboundOrchestrator>(o => o.ProcessAsync(dto, CancellationToken.None));
            _logger.LogInformation(
                "Pont WhatsApp : message {Id} mis en file ({Length} caractères).",
                message.ExternalMessageId, text.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pont WhatsApp : échec de mise en file du message {Id}.", message.ExternalMessageId);
        }
    }

    // ── Envoi sortant ──

    public async Task<bool> SendTextAsync(string chatId, string text, CancellationToken cancellationToken = default)
    {
        if (!FeatureEnabled)
            return false;
        if (string.IsNullOrWhiteSpace(chatId) || string.IsNullOrWhiteSpace(text))
            return false;
        if (Status.State != BridgeState.Ready)
            return false;

        var process = _process;
        if (process is null || process.HasExited)
            return false;

        var id = Interlocked.Increment(ref _sendCounter).ToString();
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingSends[id] = tcs;
        try
        {
            await WriteLineAsync(WhatsAppBridgeProtocol.SerializeSend(id, chatId, text), cancellationToken);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _settings.SendTimeoutSeconds)));
            using var registration = timeoutCts.Token.Register(() => tcs.TrySetResult(false));
            return await tcs.Task;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pont WhatsApp : envoi échoué ({Length} caractères).", text.Length);
            return false;
        }
        finally
        {
            _pendingSends.TryRemove(id, out _);
        }
    }

    // ── Écriture stdin (sérialisée) ──

    private async Task WriteLineAsync(string line, CancellationToken cancellationToken)
    {
        var process = _process;
        if (process is null || process.HasExited)
            throw new InvalidOperationException("Pont WhatsApp non démarré.");

        await _stdinLock.WaitAsync(cancellationToken);
        try
        {
            await process.StandardInput.WriteLineAsync(line.AsMemory(), cancellationToken);
            await process.StandardInput.FlushAsync(cancellationToken);
        }
        finally
        {
            _stdinLock.Release();
        }
    }

    private async Task TryWriteShutdownAsync(Process process)
    {
        try
        {
            await _stdinLock.WaitAsync();
            try
            {
                await process.StandardInput.WriteLineAsync(WhatsAppBridgeProtocol.SerializeShutdown());
                await process.StandardInput.FlushAsync();
            }
            finally
            {
                _stdinLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Pont WhatsApp : commande shutdown non transmise (kill à suivre).");
        }
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    // ── État ──

    private void ApplyState(BridgeState state, string? error = null)
    {
        lock (_statusLock)
        {
            var qr = state == BridgeState.WaitingQr ? _status.Qr : null;
            _status = new BridgeStatus(state, qr, error, DateTime.UtcNow);
        }
    }

    private void ApplyQr(string qr)
    {
        lock (_statusLock)
        {
            _status = new BridgeStatus(BridgeState.WaitingQr, qr, null, DateTime.UtcNow);
        }
    }
}
