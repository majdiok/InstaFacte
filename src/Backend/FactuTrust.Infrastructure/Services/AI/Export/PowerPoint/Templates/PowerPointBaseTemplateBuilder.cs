using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Templates;

/// <summary>
/// Builds hybrid base .pptx files with decorated slide masters and standard named layouts.
/// Each theme gets visually distinct master backgrounds (solid, accent band, decorative shape).
/// </summary>
public static class PowerPointBaseTemplateBuilder
{
    public static byte[] Build(PowerPointThemeDefinition definition, SlideOrientation orientation)
    {
        var dimensions = SlideDimensions.For(orientation);
        using var ms = new MemoryStream();
        using (var document = PresentationDocument.Create(ms, PresentationDocumentType.Presentation, autoSave: false))
        {
            BuildPackage(document, definition, dimensions);
            document.Save();
        }

        return ms.ToArray();
    }

    private static void BuildPackage(
        PresentationDocument document,
        PowerPointThemeDefinition definition,
        SlideDimensions dimensions)
    {
        var presentationPart = document.AddPresentationPart();
        presentationPart.Presentation = new P.Presentation();

        var masterPart = presentationPart.AddNewPart<SlideMasterPart>();
        var masterRelId = presentationPart.GetIdOfPart(masterPart);
        BuildSlideMaster(masterPart, definition, dimensions);

        var layoutRelIds = new List<string>();
        foreach (var mapping in HybridLayoutNames.StandardMappings)
        {
            var layoutPart = masterPart.AddNewPart<SlideLayoutPart>();
            layoutRelIds.Add(masterPart.GetIdOfPart(layoutPart));
            BuildSlideLayout(layoutPart, mapping.LayoutName, definition);
        }

        PatchSlideMasterLayoutList(masterPart, layoutRelIds);

        var themePart = masterPart.AddNewPart<ThemePart>();
        ApplyTheme(themePart, definition);

        var notesMasterPart = presentationPart.AddNewPart<NotesMasterPart>();
        var notesMasterRelId = presentationPart.GetIdOfPart(notesMasterPart);
        BuildNotesMaster(notesMasterPart);
        notesMasterPart.AddPart(themePart);

        var slideSizeType = dimensions.Width == 12_192_000L
            ? P.SlideSizeValues.Screen16x9
            : P.SlideSizeValues.Screen4x3;

        presentationPart.Presentation.Append(
            new P.SlideMasterIdList(new P.SlideMasterId { Id = 2147483648U, RelationshipId = masterRelId }),
            new P.NotesMasterIdList(new P.NotesMasterId { Id = notesMasterRelId }),
            new P.SlideIdList(),
            new P.SlideSize { Cx = (int)dimensions.Width, Cy = (int)dimensions.Height, Type = slideSizeType },
            new P.NotesSize { Cx = 6858000, Cy = 9144000 },
            new P.DefaultTextStyle());
    }

    private static void BuildSlideMaster(
        SlideMasterPart masterPart,
        PowerPointThemeDefinition definition,
        SlideDimensions dimensions)
    {
        var colors = definition.Colors;
        var bgFill = definition.IsDark ? colors.BackgroundHex : colors.SurfaceHex;
        var accent = colors.AccentHex;

        var shapeTree = new P.ShapeTree(
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(new A.TransformGroup()));

        // Full-bleed master background
        shapeTree.Append(MasterBackgroundShape(dimensions, bgFill, 2U));

        // Decorative accent per cover style
        AppendMasterDecoration(shapeTree, definition, dimensions);

        masterPart.SlideMaster = new P.SlideMaster(
            new P.CommonSlideData(shapeTree),
            new P.ColorMap
            {
                Background1 = A.ColorSchemeIndexValues.Light1,
                Text1 = A.ColorSchemeIndexValues.Dark1,
                Background2 = A.ColorSchemeIndexValues.Light2,
                Text2 = A.ColorSchemeIndexValues.Dark2,
                Accent1 = A.ColorSchemeIndexValues.Accent1,
                Accent2 = A.ColorSchemeIndexValues.Accent2,
                Accent3 = A.ColorSchemeIndexValues.Accent3,
                Accent4 = A.ColorSchemeIndexValues.Accent4,
                Accent5 = A.ColorSchemeIndexValues.Accent5,
                Accent6 = A.ColorSchemeIndexValues.Accent6,
                Hyperlink = A.ColorSchemeIndexValues.Hyperlink,
                FollowedHyperlink = A.ColorSchemeIndexValues.FollowedHyperlink
            },
            new P.SlideLayoutIdList());
    }

    private static void AppendMasterDecoration(
        P.ShapeTree shapeTree,
        PowerPointThemeDefinition definition,
        SlideDimensions dimensions)
    {
        var accent = definition.Colors.AccentHex;
        var soft = definition.Colors.AccentSoftHex;

        switch (definition.CoverStyle)
        {
            case CoverLayoutStyle.DarkCinematic:
                shapeTree.Append(MasterAccentBar(dimensions, accent, 3U, heightPx: 12));
                shapeTree.Append(MasterBlockDecoration(dimensions, soft, 4U, 0.75, 0.15));
                break;
            case CoverLayoutStyle.GradientHero:
            case CoverLayoutStyle.CorporateBlue:
                shapeTree.Append(MasterAccentBar(dimensions, accent, 3U, heightPx: 80, atBottom: false));
                shapeTree.Append(MasterSideBand(dimensions, soft, 5U, 0.35));
                break;
            case CoverLayoutStyle.BoldEditorial:
                shapeTree.Append(MasterSideBand(dimensions, accent, 3U, 0.08, left: true));
                break;
            case CoverLayoutStyle.MinimalSerif:
            case CoverLayoutStyle.SplitPhoto:
                shapeTree.Append(MasterSideBand(dimensions, soft, 3U, 0.42, left: false));
                break;
            case CoverLayoutStyle.MedicalClean:
                shapeTree.Append(MasterAccentBar(dimensions, accent, 3U, heightPx: 6));
                shapeTree.Append(MasterBlockDecoration(dimensions, soft, 4U, 0.85, 0.75, sizePx: 180));
                break;
            case CoverLayoutStyle.NeonAccent:
                shapeTree.Append(MasterAccentBar(dimensions, accent, 3U, heightPx: 6));
                shapeTree.Append(MasterAccentBar(dimensions, accent, 4U, heightPx: 6, atBottom: true));
                shapeTree.Append(MasterCornerGlow(dimensions, soft, 5U));
                break;
            case CoverLayoutStyle.EditorialMagazine:
                shapeTree.Append(MasterSideBand(dimensions, accent, 3U, 0.04, left: true));
                shapeTree.Append(MasterHorizontalRule(dimensions, definition.Colors.SecondaryHex, 4U, 0.30));
                shapeTree.Append(MasterHorizontalRule(dimensions, definition.Colors.SecondaryHex, 5U, 0.85));
                break;
            case CoverLayoutStyle.AsymmetricSplit:
                shapeTree.Append(MasterSideBand(dimensions, soft, 3U, 0.40, left: false));
                shapeTree.Append(MasterAccentBar(dimensions, accent, 4U, heightPx: 10));
                break;
            case CoverLayoutStyle.GradientWaveHero:
                shapeTree.Append(MasterGradientStripe(dimensions, accent, 3U, 0.00, 0.40));
                shapeTree.Append(MasterGradientStripe(dimensions, soft, 4U, 0.40, 0.80));
                shapeTree.Append(MasterAccentBar(dimensions, accent, 5U, heightPx: 4, atBottom: true));
                break;
            default:
                shapeTree.Append(MasterAccentBar(dimensions, accent, 3U, heightPx: 8));
                break;
        }
    }

    private static P.Shape MasterBackgroundShape(SlideDimensions dims, string fillHex, uint id) =>
        OpenXmlPresentationHelpers.Shape(
            id, "MasterBackground", 0, 0, dims.Width, dims.Height,
            OpenXmlPresentationHelpers.SolidFill(fillHex),
            new[] { OpenXmlPresentationHelpers.EmptyParagraph() });

    private static P.Shape MasterAccentBar(
        SlideDimensions dims, string fillHex, uint id, int heightPx, bool atBottom = false)
    {
        var y = atBottom ? dims.Height - OpenXmlPresentationHelpers.PxToEmu(heightPx) : 0L;
        return OpenXmlPresentationHelpers.Shape(
            id, "MasterAccentBar", 0, y, dims.Width, OpenXmlPresentationHelpers.PxToEmu(heightPx),
            OpenXmlPresentationHelpers.SolidFill(fillHex),
            new[] { OpenXmlPresentationHelpers.EmptyParagraph() });
    }

    private static P.Shape MasterSideBand(
        SlideDimensions dims, string fillHex, uint id, double widthRatio, bool left = true)
    {
        var w = (long)(dims.Width * widthRatio);
        var x = left ? 0L : dims.Width - w;
        return OpenXmlPresentationHelpers.Shape(
            id, "MasterSideBand", x, 0, w, dims.Height,
            OpenXmlPresentationHelpers.SolidFill(fillHex),
            new[] { OpenXmlPresentationHelpers.EmptyParagraph() });
    }

    private static P.Shape MasterBlockDecoration(
        SlideDimensions dims, string fillHex, uint id,
        double cxRatio, double cyRatio, int sizePx = 240)
    {
        var size = OpenXmlPresentationHelpers.PxToEmu(sizePx);
        var x = (long)(dims.Width * cxRatio) - size / 2;
        var y = (long)(dims.Height * cyRatio) - size / 2;
        return OpenXmlPresentationHelpers.Shape(
            id, "MasterBlock", x, y, size, size,
            OpenXmlPresentationHelpers.SolidFill(fillHex),
            new[] { OpenXmlPresentationHelpers.EmptyParagraph() });
    }

    /// <summary>Soft circular glow anchored in the top-right corner — used by <see cref="CoverLayoutStyle.NeonAccent"/>.</summary>
    private static P.Shape MasterCornerGlow(SlideDimensions dims, string fillHex, uint id)
    {
        var size = OpenXmlPresentationHelpers.PxToEmu(420);
        var x = dims.Width - (size * 2 / 3);
        var y = -size / 3;
        return OpenXmlPresentationHelpers.Shape(
            id, "MasterCornerGlow", x, y, size, size,
            OpenXmlPresentationHelpers.SolidFill(fillHex),
            new[] { OpenXmlPresentationHelpers.EmptyParagraph() });
    }

    /// <summary>
    /// Thin horizontal rule positioned at <paramref name="yRatio"/> (0..1). Used by editorial layouts to
    /// frame the content with hairline separators.
    /// </summary>
    private static P.Shape MasterHorizontalRule(
        SlideDimensions dims, string fillHex, uint id, double yRatio)
    {
        var thickness = OpenXmlPresentationHelpers.PxToEmu(1);
        var y = (long)(dims.Height * yRatio);
        return OpenXmlPresentationHelpers.Shape(
            id, "MasterHorizontalRule", 0, y, dims.Width, thickness,
            OpenXmlPresentationHelpers.SolidFill(fillHex),
            new[] { OpenXmlPresentationHelpers.EmptyParagraph() });
    }

    /// <summary>
    /// Single coloured stripe spanning the vertical band [<paramref name="fromYRatio"/>..<paramref name="toYRatio"/>].
    /// Stacking several stripes with successive colors simulates a multi-stop gradient hero.
    /// </summary>
    private static P.Shape MasterGradientStripe(
        SlideDimensions dims, string fillHex, uint id, double fromYRatio, double toYRatio)
    {
        var top = (long)(dims.Height * fromYRatio);
        var bottom = (long)(dims.Height * toYRatio);
        var height = Math.Max(bottom - top, OpenXmlPresentationHelpers.PxToEmu(8));
        return OpenXmlPresentationHelpers.Shape(
            id, "MasterGradientStripe", 0, top, dims.Width, height,
            OpenXmlPresentationHelpers.SolidFill(fillHex),
            new[] { OpenXmlPresentationHelpers.EmptyParagraph() });
    }

    private static void BuildSlideLayout(SlideLayoutPart layoutPart, string layoutName, PowerPointThemeDefinition definition)
    {
        layoutPart.SlideLayout = new P.SlideLayout(
            new P.CommonSlideData(
                new P.ShapeTree(
                    new P.NonVisualGroupShapeProperties(
                        new P.NonVisualDrawingProperties { Id = 1U, Name = layoutName },
                        new P.NonVisualGroupShapeDrawingProperties(),
                        new P.ApplicationNonVisualDrawingProperties()),
                    new P.GroupShapeProperties(new A.TransformGroup()))),
            new P.ColorMapOverride(new A.MasterColorMapping()))
        {
            Type = layoutName == HybridLayoutNames.Cover
                ? P.SlideLayoutValues.Title
                : P.SlideLayoutValues.Blank
        };
    }

    private static void PatchSlideMasterLayoutList(SlideMasterPart masterPart, IReadOnlyList<string> layoutRelIds)
    {
        var list = masterPart.SlideMaster!.SlideLayoutIdList!;
        list.RemoveAllChildren<P.SlideLayoutId>();
        uint id = 2147483649U;
        foreach (var relId in layoutRelIds)
        {
            list.Append(new P.SlideLayoutId { Id = id++, RelationshipId = relId });
        }
    }

    private static void ApplyTheme(ThemePart themePart, PowerPointThemeDefinition definition)
    {
        var colors = definition.Colors;
        var fonts = definition.Fonts;
        var chart = colors.ChartSeriesHex;

        var theme = new A.Theme { Name = definition.Name };
        theme.Append(new A.ThemeElements(
            new A.ColorScheme(
                new A.Dark1Color(new A.RgbColorModelHex { Val = colors.PrimaryHex }),
                new A.Light1Color(new A.RgbColorModelHex { Val = colors.BackgroundHex }),
                new A.Dark2Color(new A.RgbColorModelHex { Val = colors.SecondaryHex }),
                new A.Light2Color(new A.RgbColorModelHex { Val = colors.SurfaceHex }),
                new A.Accent1Color(new A.RgbColorModelHex { Val = colors.AccentHex }),
                new A.Accent2Color(new A.RgbColorModelHex { Val = chart.Count > 1 ? chart[1] : colors.SuccessHex }),
                new A.Accent3Color(new A.RgbColorModelHex { Val = chart.Count > 2 ? chart[2] : colors.WarningHex }),
                new A.Accent4Color(new A.RgbColorModelHex { Val = chart.Count > 3 ? chart[3] : colors.DangerHex }),
                new A.Accent5Color(new A.RgbColorModelHex { Val = chart.Count > 4 ? chart[4] : colors.MutedHex }),
                new A.Accent6Color(new A.RgbColorModelHex { Val = chart.Count > 5 ? chart[5] : colors.AccentSoftHex }),
                new A.Hyperlink(new A.RgbColorModelHex { Val = colors.AccentHex }),
                new A.FollowedHyperlinkColor(new A.RgbColorModelHex { Val = colors.SecondaryHex }))
            { Name = definition.Name },
            new A.FontScheme(
                new A.MajorFont(
                    new A.LatinFont { Typeface = fonts.TitleFamily },
                    new A.EastAsianFont { Typeface = string.Empty },
                    new A.ComplexScriptFont { Typeface = string.Empty }),
                new A.MinorFont(
                    new A.LatinFont { Typeface = fonts.BodyFamily },
                    new A.EastAsianFont { Typeface = string.Empty },
                    new A.ComplexScriptFont { Typeface = string.Empty }))
            { Name = fonts.TitleFamily },
            new A.FormatScheme(
                new A.FillStyleList(
                    new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor }),
                    new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor }),
                    new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor })),
                new A.LineStyleList(
                    new A.Outline(OpenXmlPresentationHelpers.SolidFill("CFD8DC")) { Width = 9525 },
                    new A.Outline(OpenXmlPresentationHelpers.SolidFill("90A4AE")) { Width = 19050 },
                    new A.Outline(OpenXmlPresentationHelpers.SolidFill("455A64")) { Width = 28575 }),
                new A.EffectStyleList(
                    new A.EffectStyle(new A.EffectList()),
                    new A.EffectStyle(new A.EffectList()),
                    new A.EffectStyle(new A.EffectList())),
                new A.BackgroundFillStyleList(
                    new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor }),
                    new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor }),
                    new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor })))
            { Name = "Office" }));
        theme.Append(new A.ObjectDefaults());
        theme.Append(new A.ExtraColorSchemeList());
        themePart.Theme = theme;
    }

    private static void BuildNotesMaster(NotesMasterPart notesMasterPart)
    {
        var notesMaster = new P.NotesMaster();

        var shapeTree = new P.ShapeTree(
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(new A.TransformGroup()),
            BuildNotesPlaceholderShape(
                id: 2U,
                name: "Header Placeholder 1",
                placeholderType: P.PlaceholderValues.Header,
                offsetX: 0L,
                offsetY: 0L,
                width: 6_858_000L,
                height: 457_200L),
            BuildNotesPlaceholderShape(
                id: 3U,
                name: "Date Placeholder 2",
                placeholderType: P.PlaceholderValues.DateAndTime,
                offsetX: 3_429_000L,
                offsetY: 0L,
                width: 3_429_000L,
                height: 457_200L),
            BuildSlideImagePlaceholder(id: 4U, name: "Slide Image Placeholder 3"),
            BuildNotesPlaceholderShape(
                id: 5U,
                name: "Notes Placeholder 4",
                placeholderType: P.PlaceholderValues.Body,
                offsetX: 685_800L,
                offsetY: 4_343_400L,
                width: 5_486_400L,
                height: 4_114_800L,
                placeholderIndex: 2U),
            BuildNotesPlaceholderShape(
                id: 6U,
                name: "Footer Placeholder 5",
                placeholderType: P.PlaceholderValues.Footer,
                offsetX: 0L,
                offsetY: 8_686_800L,
                width: 6_858_000L,
                height: 457_200L),
            BuildNotesPlaceholderShape(
                id: 7U,
                name: "Slide Number Placeholder 6",
                placeholderType: P.PlaceholderValues.SlideNumber,
                offsetX: 3_429_000L,
                offsetY: 8_686_800L,
                width: 3_429_000L,
                height: 457_200L));

        notesMaster.Append(new P.CommonSlideData(shapeTree));
        notesMaster.Append(new P.ColorMap
        {
            Background1 = A.ColorSchemeIndexValues.Light1,
            Text1 = A.ColorSchemeIndexValues.Dark1,
            Background2 = A.ColorSchemeIndexValues.Light2,
            Text2 = A.ColorSchemeIndexValues.Dark2,
            Accent1 = A.ColorSchemeIndexValues.Accent1,
            Accent2 = A.ColorSchemeIndexValues.Accent2,
            Accent3 = A.ColorSchemeIndexValues.Accent3,
            Accent4 = A.ColorSchemeIndexValues.Accent4,
            Accent5 = A.ColorSchemeIndexValues.Accent5,
            Accent6 = A.ColorSchemeIndexValues.Accent6,
            Hyperlink = A.ColorSchemeIndexValues.Hyperlink,
            FollowedHyperlink = A.ColorSchemeIndexValues.FollowedHyperlink
        });
        notesMaster.Append(BuildDefaultNotesStyle());

        notesMasterPart.NotesMaster = notesMaster;
    }

    private static P.Shape BuildNotesPlaceholderShape(
        uint id,
        string name,
        P.PlaceholderValues placeholderType,
        long offsetX,
        long offsetY,
        long width,
        long height,
        uint? placeholderIndex = null)
    {
        var placeholder = new P.PlaceholderShape { Type = placeholderType };
        if (placeholderIndex.HasValue)
            placeholder.Index = placeholderIndex.Value;

        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = name },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties(placeholder)),
            new P.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = offsetX, Y = offsetY },
                    new A.Extents { Cx = width, Cy = height }),
                new A.PresetGeometry { Preset = A.ShapeTypeValues.Rectangle }),
            new P.TextBody(
                new A.BodyProperties(),
                new A.ListStyle(),
                new A.Paragraph(new A.EndParagraphRunProperties { Language = "fr-FR" })));
    }

    private static P.Shape BuildSlideImagePlaceholder(uint id, string name)
    {
        var placeholder = new P.PlaceholderShape { Type = P.PlaceholderValues.SlideImage, Index = 1U };
        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = name },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true, NoRotation = true }),
                new P.ApplicationNonVisualDrawingProperties(placeholder)),
            new P.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = 685_800L, Y = 457_200L },
                    new A.Extents { Cx = 5_486_400L, Cy = 3_886_200L }),
                new A.PresetGeometry { Preset = A.ShapeTypeValues.Rectangle }),
            new P.TextBody(
                new A.BodyProperties { Anchor = A.TextAnchoringTypeValues.Center },
                new A.ListStyle(),
                new A.Paragraph()));
    }

    private static P.NotesStyle BuildDefaultNotesStyle()
    {
        var style = new P.NotesStyle();
        style.Append(new A.Level1ParagraphProperties(new A.DefaultRunProperties { FontSize = 1200 })
        { LeftMargin = 0, Indent = 0, Alignment = A.TextAlignmentTypeValues.Left });
        style.Append(new A.Level2ParagraphProperties(new A.DefaultRunProperties { FontSize = 1200 }) { LeftMargin = 457_200, Indent = 0 });
        style.Append(new A.Level3ParagraphProperties(new A.DefaultRunProperties { FontSize = 1200 }) { LeftMargin = 914_400, Indent = 0 });
        style.Append(new A.Level4ParagraphProperties(new A.DefaultRunProperties { FontSize = 1200 }) { LeftMargin = 1_371_600, Indent = 0 });
        style.Append(new A.Level5ParagraphProperties(new A.DefaultRunProperties { FontSize = 1200 }) { LeftMargin = 1_828_800, Indent = 0 });
        style.Append(new A.Level6ParagraphProperties(new A.DefaultRunProperties { FontSize = 1200 }) { LeftMargin = 2_286_000, Indent = 0 });
        style.Append(new A.Level7ParagraphProperties(new A.DefaultRunProperties { FontSize = 1200 }) { LeftMargin = 2_743_200, Indent = 0 });
        style.Append(new A.Level8ParagraphProperties(new A.DefaultRunProperties { FontSize = 1200 }) { LeftMargin = 3_200_400, Indent = 0 });
        style.Append(new A.Level9ParagraphProperties(new A.DefaultRunProperties { FontSize = 1200 }) { LeftMargin = 3_657_600, Indent = 0 });
        return style;
    }
}
