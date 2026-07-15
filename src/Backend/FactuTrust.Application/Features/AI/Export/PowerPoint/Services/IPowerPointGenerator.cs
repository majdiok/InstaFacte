using FactuTrust.Application.Features.AI.Export.PowerPoint.DTOs;

namespace FactuTrust.Application.Features.AI.Export.PowerPoint.Services;

/// <summary>
/// Application contract for generating a PowerPoint deck from one or more assistant responses.
/// Implementation lives in the Infrastructure layer to keep DocumentFormat.OpenXml usage out
/// of the Application boundary.
/// </summary>
public interface IPowerPointGenerator
{
    /// <summary>
    /// Generates a .pptx in-memory based on the provided selections.
    /// The caller must verify the user is allowed to access every conversation/message referenced.
    /// </summary>
    Task<PowerPointGenerationResult> GenerateAsync(
        PowerPointExportRequestDto request,
        Guid userId,
        CancellationToken cancellationToken = default);
}
