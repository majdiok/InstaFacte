namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>Limite les générations Ollama concurrentes (singleton processus).</summary>
public interface IOllamaGenerationGate
{
    Task<IOllamaGateLease> AcquireAsync(CancellationToken cancellationToken = default);
}

/// <summary>Jeton de la porte Ollama avec durée d'attente avant acquisition.</summary>
public interface IOllamaGateLease : IDisposable
{
    long WaitMilliseconds { get; }
}