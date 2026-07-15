using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;

/// <summary>
/// Resolves a concrete <see cref="IPowerPointTheme"/> instance from a <see cref="PowerPointTemplate"/>.
/// </summary>
public interface IPowerPointThemeResolver
{
    IPowerPointTheme Resolve(PowerPointTemplate template);

    IReadOnlyList<IPowerPointTheme> All { get; }
}
