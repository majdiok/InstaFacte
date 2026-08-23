using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Application.Features.AI;

public static class ModalModelCatalog
{
    public static UnifiedAiModelInfo ToUnified(string modelId, string? displayName = null)
    {
        var id = (modelId ?? string.Empty).Trim();
        return new UnifiedAiModelInfo(
            $"{ModelRef.ModalPrefix}{id}",
            "modal",
            string.IsNullOrWhiteSpace(displayName) ? id : displayName.Trim(),
            null,
            null,
            SupportsVision: AiModelCapabilityDetector.DetectVisionSupport(id),
            SupportsChat: AiModelCapabilityDetector.DetectChatCapable(id));
    }
}
