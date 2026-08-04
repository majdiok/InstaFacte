using System.Diagnostics;
using FactuTrust.Application.Common.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.AI;

/// <summary>
/// Estime la faisabilité de chargement d'un modèle Ollama selon la taille du fichier
/// modèle et la RAM disponible (même heuristique que <see cref="OllamaModelRecommender"/>).
/// </summary>
public sealed class OllamaModelReadinessChecker : IOllamaModelReadinessChecker
{
    private readonly IOllamaClient _ollamaClient;
    private readonly ILogger<OllamaModelReadinessChecker> _logger;

    public OllamaModelReadinessChecker(
        IOllamaClient ollamaClient,
        ILogger<OllamaModelReadinessChecker> logger)
    {
        _ollamaClient = ollamaClient;
        _logger = logger;
    }

    public async Task<OllamaModelReadinessResult> CheckAsync(
        string modelName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelName))
            return new OllamaModelReadinessResult(false, "Aucun modèle d'import IA configuré.", null, null);

        if (!await _ollamaClient.IsAvailableAsync(cancellationToken))
        {
            return new OllamaModelReadinessResult(
                false,
                "Le moteur IA InstaFact est indisponible. Vérifiez que le service est démarré sur le serveur.",
                null,
                null);
        }

        var (availableBytes, _) = GetAvailableRamBytes();
        var availableGiB = availableBytes / (1024.0 * 1024 * 1024);

        var models = await _ollamaClient.ListModelsAsync(cancellationToken);
        var match = models.FirstOrDefault(m =>
            ModelNameMatches(modelName, m.Name));

        if (match is null)
        {
            return new OllamaModelReadinessResult(
                false,
                $"Le modèle « {modelName} » n'est pas installé sur le moteur IA InstaFact. Contactez l'administrateur plateforme ou choisissez un autre modèle.",
                null,
                Math.Round(availableGiB, 1));
        }

        if (match.Size > 0)
        {
            var requiredWithOverhead = match.Size * 1.3;
            var ramThreshold = availableBytes * 0.7;
            if (requiredWithOverhead >= ramThreshold)
            {
                var requiredGiB = requiredWithOverhead / (1024.0 * 1024 * 1024);
                _logger.LogWarning(
                    "Modèle {Model} : RAM insuffisante (requis ~{RequiredGiB:F1} Go, disponible ~{AvailableGiB:F1} Go)",
                    modelName, requiredGiB, availableGiB);

                return new OllamaModelReadinessResult(
                    false,
                    $"Le modèle d'import « {modelName} » nécessite environ {requiredGiB:F1} Go de RAM, "
                    + $"mais seulement {availableGiB:F1} Go sont disponibles. "
                    + "Installez un modèle plus léger (ex. qwen2.5:7b-instruct) ou libérez de la mémoire.",
                    Math.Round(requiredGiB, 1),
                    Math.Round(availableGiB, 1));
            }
        }

        return new OllamaModelReadinessResult(true, null, null, Math.Round(availableGiB, 1));
    }

    private static (long AvailableBytes, long TotalBytes) GetAvailableRamBytes()
    {
        var gcInfo = GC.GetGCMemoryInfo();
        var totalRam = gcInfo.TotalAvailableMemoryBytes;
        var usedByProcess = Process.GetCurrentProcess().WorkingSet64;
        var availableRam = Math.Max(0, totalRam - usedByProcess);
        return (availableRam, totalRam);
    }

    private static bool ModelNameMatches(string requested, string installedName)
    {
        requested = requested.Trim();
        installedName = installedName.Trim();
        if (string.Equals(requested, installedName, StringComparison.OrdinalIgnoreCase))
            return true;

        var reqBase = requested.Split(':')[0];
        var insBase = installedName.Split(':')[0];
        return string.Equals(reqBase, insBase, StringComparison.OrdinalIgnoreCase);
    }
}
