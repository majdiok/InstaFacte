using FactuTrust.Application.Features.AI.Export.PowerPoint.DTOs;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Services;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint;

/// <summary>
/// Builds the public-facing catalogue from the registered theme resolver. Keeps the visual
/// metadata source-of-truth co-located with the theme implementations.
/// </summary>
public sealed class PowerPointTemplateCatalog : IPowerPointTemplateCatalog
{
    private readonly IReadOnlyList<PowerPointTemplateInfoDto> _list;

    public PowerPointTemplateCatalog(IPowerPointThemeResolver resolver)
    {
        _list = resolver.All
            .Select(theme => new PowerPointTemplateInfoDto
            {
                Id = theme.Template,
                Key = theme.Template.ToString(),
                Name = theme.Name,
                Description = theme.Description,
                Category = theme.Category,
                IsDark = theme.IsDark,
                SortOrder = theme.SortOrder,
                PrimaryColorHex = "#" + theme.Colors.PrimaryHex,
                AccentColorHex = "#" + theme.Colors.AccentHex,
                BackgroundColorHex = "#" + theme.Colors.BackgroundHex,
                SurfaceColorHex = "#" + theme.Colors.SurfaceHex,
                OnSurfaceColorHex = "#" + theme.Colors.OnSurfaceHex,
                LinkColorHex = "#" + theme.Colors.AccentHex,
                TitleFontFamily = theme.Fonts.TitleFamily,
                BodyFontFamily = theme.Fonts.BodyFamily,
                PreviewGradientCss = theme.PreviewGradientCss,
                Engine = theme.Engine,
                PreviewThumbnailUrl = theme.PreviewThumbnailPath,
                PreviewThumbnailUrl4x3 = theme.PreviewThumbnailPath?.Replace("16x9", "4x3"),
                RequiresAttribution = theme.RequiredAttribution is not null,
                AttributionSummary = theme.RequiredAttribution?.AttributionText,
                CoverLayoutStyle = theme.CoverStyle
            })
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<PowerPointTemplateInfoDto> List() => _list;
}
