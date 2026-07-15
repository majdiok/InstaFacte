using DocumentFormat.OpenXml.Packaging;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Templates;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI.Export.PowerPoint;

/// <summary>
/// Validates that the four new cover layout styles introduced with the premium-V2 expansion
/// (<see cref="CoverLayoutStyle.NeonAccent"/>, <see cref="CoverLayoutStyle.EditorialMagazine"/>,
/// <see cref="CoverLayoutStyle.AsymmetricSplit"/>, <see cref="CoverLayoutStyle.GradientWaveHero"/>)
/// produce a structurally valid base template for both orientations.
/// </summary>
/// <remarks>
/// The legacy <see cref="CoverLayoutStyle"/> values (0–7) keep being exercised by the
/// <c>GenerateAsync_all_themes_produce_valid_pptx</c> theory in <c>PowerPointGeneratorTests</c>.
/// This file zooms in on the new master decoration helpers
/// (<c>MasterCornerGlow</c>, <c>MasterHorizontalRule</c>, <c>MasterGradientStripe</c>).
/// </remarks>
public sealed class CoverLayoutStyleTests
{
    public static IEnumerable<object[]> NewCoverStyles =>
        new[]
        {
            new object[] { CoverLayoutStyle.NeonAccent },
            new object[] { CoverLayoutStyle.EditorialMagazine },
            new object[] { CoverLayoutStyle.AsymmetricSplit },
            new object[] { CoverLayoutStyle.GradientWaveHero }
        };

    public static IEnumerable<object[]> NewCoverStylesAndOrientations =>
        from style in new[]
        {
            CoverLayoutStyle.NeonAccent,
            CoverLayoutStyle.EditorialMagazine,
            CoverLayoutStyle.AsymmetricSplit,
            CoverLayoutStyle.GradientWaveHero
        }
        from orientation in new[] { SlideOrientation.Widescreen16x9, SlideOrientation.Standard4x3 }
        select new object[] { style, orientation };

    [Theory]
    [MemberData(nameof(NewCoverStylesAndOrientations))]
    public void Build_produces_valid_pptx_for_new_cover_style(CoverLayoutStyle style, SlideOrientation orientation)
    {
        var definition = BuildSampleDefinition(style);

        var bytes = PowerPointBaseTemplateBuilder.Build(definition, orientation);

        Assert.True(bytes.Length > 4_000,
            $"Base template for {style}/{orientation} is suspiciously small ({bytes.Length} bytes).");

        using var ms = new MemoryStream(bytes, writable: false);
        using var doc = PresentationDocument.Open(ms, isEditable: false);
        Assert.NotNull(doc.PresentationPart);
        Assert.NotNull(doc.PresentationPart!.SlideMasterParts.First().ThemePart);
    }

    [Theory]
    [MemberData(nameof(NewCoverStyles))]
    public void Build_master_contains_decoration_shapes_for_new_style(CoverLayoutStyle style)
    {
        var definition = BuildSampleDefinition(style);
        var bytes = PowerPointBaseTemplateBuilder.Build(definition, SlideOrientation.Widescreen16x9);

        using var ms = new MemoryStream(bytes, writable: false);
        using var doc = PresentationDocument.Open(ms, isEditable: false);
        var masterPart = doc.PresentationPart!.SlideMasterParts.First();
        var shapes = masterPart.SlideMaster.CommonSlideData!.ShapeTree!
            .Descendants<DocumentFormat.OpenXml.Presentation.Shape>()
            .ToList();

        // Master always contains the background shape (id 2) plus at least one decoration (id 3+).
        Assert.True(shapes.Count >= 2,
            $"Master for {style} should contain background + at least one decoration shape; got {shapes.Count}.");
    }

    private static PowerPointThemeDefinition BuildSampleDefinition(CoverLayoutStyle style) =>
        new(
            Template: PowerPointTemplate.Standard, // arbitrary — only used for naming
            Name: $"TestCover-{style}",
            Description: "Synthetic definition exercising the new cover layout helper.",
            Category: PowerPointThemeCategory.Premium,
            IsDark: false,
            SortOrder: 999,
            Colors: new ThemeColors
            {
                PrimaryHex = "0F172A",
                SecondaryHex = "475569",
                AccentHex = "2563EB",
                AccentSoftHex = "DBEAFE",
                BackgroundHex = "FFFFFF",
                SurfaceHex = "F8FAFC",
                OnSurfaceHex = "0F172A",
                MutedHex = "94A3B8"
            },
            Fonts: new ThemeFonts { TitleFamily = "Inter", BodyFamily = "Inter", MonoFamily = "Consolas" },
            PreviewGradientCss: null,
            Engine: PowerPointThemeEngine.Hybrid,
            BaseTemplateKey: $"TestCover-{style}",
            CoverStyle: style,
            RequiredAttribution: null,
            PreviewThumbnailPath: null);
}
