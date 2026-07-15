using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.AI;

/// <summary>
/// Limite le nombre de générations LLM Ollama exécutées en parallèle (singleton partagé par tout le processus).
/// Protège un moteur Ollama mono-instance / CPU : sans cette porte, des générations concurrentes (plusieurs
/// comptes utilisant l'assistant en même temps) se sérialisent côté Ollama et finissent par dépasser le délai
/// HTTP, ce qui se traduisait par une « Génération interrompue » silencieuse.
///
/// L'attente de la porte se fait pendant que la connexion SSE reste maintenue côté client par le heartbeat,
/// donc le navigateur n'expire pas : la requête patiente puis aboutit.
/// </summary>
public sealed class OllamaGenerationGate : IOllamaGenerationGate, IDisposable
{
    private readonly SemaphoreSlim _semaphore;

    public OllamaGenerationGate(IOptions<OllamaSettings> settings)
    {
        var max = Math.Max(1, settings.Value.MaxConcurrentGenerations);
        _semaphore = new SemaphoreSlim(max, max);
    }

    /// <summary>
    /// Acquiert une place. Le jeton retourné doit être libéré (dispose) à la fin de la génération.
    /// Propage l'annulation (déconnexion client) sans consommer de place.
    /// </summary>
    public async Task<IOllamaGateLease> AcquireAsync(CancellationToken cancellationToken = default)
    {
        var waitSw = System.Diagnostics.Stopwatch.StartNew();
        await _semaphore.WaitAsync(cancellationToken);
        return new OllamaGateLease(_semaphore, waitSw.ElapsedMilliseconds);
    }

    public void Dispose() => _semaphore.Dispose();

}
