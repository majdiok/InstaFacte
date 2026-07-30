using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.AI;

/// <summary>
/// Platform-wide AI preferences (singleton row in the master database).
/// Holds the default LLM model used by the assistant for every tenant;
/// configured by an administrator from the platform back-office.
/// </summary>
public sealed class PlatformAiSettings : Entity
{
    /// <summary>Canonical model reference (e.g. "ollama:qwen2.5-coder:7b"); null = server default.</summary>
    public string? DefaultModelRef { get; private set; }

    /// <summary>Modèle dédié à l'import de factures ; null = configuration serveur (Ollama:InvoiceImportModel).</summary>
    public string? InvoiceImportModelRef { get; private set; }

    /// <summary>Modèle dédié à l'Assistant Studio (IA) ; null = modèle Assistant plateforme puis Ollama:DefaultModel.</summary>
    public string? StudioAiModelRef { get; private set; }

    /// <summary>Moteur d'inférence Ollama (GPU auto ou CPU uniquement).</summary>
    public OllamaInferenceDevice InferenceDevice { get; private set; } = OllamaInferenceDevice.Gpu;

    private PlatformAiSettings() { }

    public static PlatformAiSettings CreateDefaults() => new() { DefaultModelRef = null };

    public void SetDefaultModel(string? modelRef)
        => DefaultModelRef = string.IsNullOrWhiteSpace(modelRef) ? null : modelRef.Trim();

    public void SetInvoiceImportModel(string? modelRef)
        => InvoiceImportModelRef = string.IsNullOrWhiteSpace(modelRef) ? null : modelRef.Trim();

    public void SetStudioAiModel(string? modelRef)
        => StudioAiModelRef = string.IsNullOrWhiteSpace(modelRef) ? null : modelRef.Trim();

    public void SetInferenceDevice(OllamaInferenceDevice device)
    {
        if (!Enum.IsDefined(device))
            throw new ArgumentOutOfRangeException(nameof(device), device, "Valeur InferenceDevice invalide.");
        InferenceDevice = device;
    }
}
