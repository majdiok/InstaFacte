namespace FactuTrust.Domain.Enums;

/// <summary>
/// Moteur d'inférence Ollama choisi par l'administrateur plateforme.
/// </summary>
public enum OllamaInferenceDevice
{
    /// <summary>Ollama décide (offload GPU automatique si disponible) — comportement historique.</summary>
    Gpu = 0,

    /// <summary>Toutes les couches sur CPU (<c>num_gpu = 0</c> sur chaque requête Ollama).</summary>
    CpuOnly = 1
}