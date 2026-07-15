namespace FactuTrust.Application.Features.AI.DTOs;

/// <summary>
/// Result of the hardware-aware local model recommendation engine.
/// Returned by GET /api/ai/recommendation (never persisted).
/// </summary>
public sealed record AiModelRecommendationDto(
    string RecommendedModelRef,
    string DisplayLabel,
    string Reason,
    AiHardwareProfileDto HardwareProfile);

public sealed record AiHardwareProfileDto(
    long TotalRamBytes,
    long AvailableRamBytes,
    int CpuCores,
    AiGpuInfoDto? Gpu);

public sealed record AiGpuInfoDto(
    string? Name,
    long? VramBytes,
    bool IsAccelerated);

/// <summary>
/// Lightweight status indicating whether any AI provider is configured for the tenant.
/// </summary>
public sealed record AiConfiguredStatusDto(
    bool HasOllamaModels,
    bool HasCloudProvider,
    bool IsFullyConfigured);

/// <summary>
/// The AI model effectively used for the current tenant — configured by an administrator
/// in the back-office, or the server default fallback. Returned by GET /api/ai/active-model.
/// </summary>
public sealed record AiActiveModelDto(
    string ModelRef,
    string DisplayLabel,
    bool SupportsVision);
