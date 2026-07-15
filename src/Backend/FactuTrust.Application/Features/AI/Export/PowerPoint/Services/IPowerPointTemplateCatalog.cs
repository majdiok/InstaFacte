using FactuTrust.Application.Features.AI.Export.PowerPoint.DTOs;

namespace FactuTrust.Application.Features.AI.Export.PowerPoint.Services;

/// <summary>
/// Read-only catalogue of premium templates available to the UI.
/// Implementation lives in Infrastructure (depends on the concrete theme classes).
/// </summary>
public interface IPowerPointTemplateCatalog
{
    IReadOnlyList<PowerPointTemplateInfoDto> List();
}
