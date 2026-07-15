using FactuTrust.Application.Features.AI.Export.PowerPoint.DTOs;

namespace FactuTrust.Application.Features.AI.Export.PowerPoint.Services;

/// <summary>
/// Builds a structural preview of assistant responses for the export wizard (no .pptx generation).
/// </summary>
public interface IPowerPointExportPreviewService
{
    Task<PowerPointResponsePreviewDto?> PreviewResponseAsync(
        Guid conversationId,
        Guid messageId,
        string? customTitle,
        Guid userId,
        CancellationToken cancellationToken = default);
}
