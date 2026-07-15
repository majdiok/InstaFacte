using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactuTrust.Infrastructure.Services.Templates;

/// <summary>
/// Palette d'un modèle visuel. Permet aux presets (orange / bleu / moderne…) de partager la même
/// structure de rendu tout en variant les couleurs.
/// </summary>
public sealed record DocumentTemplateTheme
{
    public Color Primary { get; init; } = Color.FromHex("#1A3D82");
    public Color Accent { get; init; } = Color.FromHex("#28C4AC");
    public Color TableHeaderBg { get; init; } = Color.FromHex("#1A3D82");
    public Color TableHeaderText { get; init; } = Colors.White;
    public Color TotalBandBg { get; init; } = Color.FromHex("#1A3D82");
    public Color TotalBandText { get; init; } = Colors.White;
    public Color RowAltBg { get; init; } = Colors.Grey.Lighten4;
    public Color BorderColor { get; init; } = Colors.Grey.Lighten1;
    public Color MutedText { get; init; } = Colors.Grey.Darken2;
}
