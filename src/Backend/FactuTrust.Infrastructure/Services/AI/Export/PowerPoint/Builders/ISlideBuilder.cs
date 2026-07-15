using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;

/// <summary>
/// Common contract for all slide builders. Each builder is stateless and idempotent — repeated
/// calls produce identical output for identical input. Builders never write XML strings directly:
/// they always use <see cref="OpenXmlPresentationHelpers"/>.
/// </summary>
public interface ISlideBuilder
{
    /// <summary>
    /// Append one or more slides to the deck. Returns the number of slides created.
    /// </summary>
    int Build(SlideBuildContext context);
}

/// <summary>Builder variant that receives a specific response model.</summary>
public interface IResponseSlideBuilder
{
    int Build(SlideBuildContext context, AssistantResponseSlideModel response);
}
