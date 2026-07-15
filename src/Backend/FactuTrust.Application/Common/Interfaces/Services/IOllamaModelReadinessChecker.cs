namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Vérifie qu'un modèle Ollama peut être chargé avec la RAM disponible sur le serveur.
/// </summary>
public interface IOllamaModelReadinessChecker
{
    /// <summary>
    /// Évalue si le modèle peut tourner localement. Ne lance pas d'exception.
    /// </summary>
    Task<OllamaModelReadinessResult> CheckAsync(string modelName, CancellationToken cancellationToken = default);
}

/// <summary>Résultat de la vérification de disponibilité mémoire d'un modèle Ollama.</summary>
public sealed record OllamaModelReadinessResult(
    bool IsReady,
    string? UserMessage,
    double? RequiredGiB,
    double? AvailableGiB);
