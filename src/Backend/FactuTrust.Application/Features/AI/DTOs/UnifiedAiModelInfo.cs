namespace FactuTrust.Application.Features.AI.DTOs;

/// <summary>
/// Model entry for the assistant selector (Ollama local + cloud providers).
/// </summary>
public sealed record UnifiedAiModelInfo(
    string ModelRef,
    string ProviderKey,
    string DisplayLabel,
    long? SizeBytes,
    DateTime? ModifiedAtUtc,
    bool SupportsVision = false);
