using System.Diagnostics;
using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.AI.Export.PowerPoint.DTOs;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Services;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint;

/// <summary>
/// End-to-end PowerPoint generator orchestrator. Composes the slide plan, applies the requested
/// theme, runs every builder, then returns the resulting in-memory bytes plus metadata.
/// </summary>
public sealed class PowerPointGenerator : IPowerPointGenerator
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IPowerPointThemeResolver _themeResolver;
    private readonly ILogger<PowerPointGenerator> _logger;
    private readonly PowerPointValidationOptions _validationOptions;
    private readonly PowerPointRenderingOptions _renderingOptions;

    public PowerPointGenerator(
        IConversationRepository conversationRepository,
        IPowerPointThemeResolver themeResolver,
        ILogger<PowerPointGenerator> logger,
        IOptions<PowerPointValidationOptions>? validationOptions = null,
        IOptions<PowerPointRenderingOptions>? renderingOptions = null)
    {
        _conversationRepository = conversationRepository;
        _themeResolver = themeResolver;
        _logger = logger;
        _validationOptions = validationOptions?.Value ?? PowerPointValidationOptions.Default;
        _renderingOptions = renderingOptions?.Value ?? new PowerPointRenderingOptions();
    }

    public async Task<PowerPointGenerationResult> GenerateAsync(
        PowerPointExportRequestDto request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var culture = ResolveCulture(request.Locale);
        var theme = _themeResolver.Resolve(request.Template);
        var dimensions = SlideDimensions.For(request.Orientation);

        var responseModels = await BuildResponseModelsAsync(request, culture, cancellationToken);
        if (responseModels.Count == 0)
            throw new InvalidOperationException("Aucune réponse exploitable n'a été trouvée dans la sélection.");

        using var ms = new MemoryStream();
        using (var document = PresentationDocument.Create(
            ms,
            DocumentFormat.OpenXml.PresentationDocumentType.Presentation,
            autoSave: false))
        {
            CreatePresentationParts(document, dimensions, theme);
            var presentationPart = document.PresentationPart!;
            var masterPart = presentationPart.SlideMasterParts.First();
            var layoutPart = masterPart.SlideLayoutParts.First();

            var context = new SlideBuildContext(
                document: document,
                masterPart: masterPart,
                layoutPart: layoutPart,
                theme: theme,
                dimensions: dimensions,
                request: request,
                culture: culture,
                authorName: ResolveAuthor(request),
                renderingOptions: _renderingOptions);

            RunBuilders(context, responseModels);

            cancellationToken.ThrowIfCancellationRequested();

            // Explicit save before the using-block closes the package. With autoSave: false
            // the SDK does not flush until Save() is called, which is the Microsoft-recommended
            // pattern for in-memory OOXML packaging and guarantees an atomic, complete write.
            document.Save();

            // Run the OpenXml validator while the document is still open so we can surface
            // structural issues (broken relationships, schema violations) before the bytes
            // ever reach the user. In production this is log-only; tests can opt into throw-mode.
            PowerPointValidationGate.ValidateIfEnabled(document, _validationOptions, _logger);
        }

        stopwatch.Stop();

        var bytes = ms.ToArray();
        var fileName = BuildFileName(request, theme.Template);
        var slideCount = CountSlidesIn(bytes);

        var conversationIds = responseModels.Select(r => r.ConversationId).Distinct().ToList();
        var messageIds = responseModels.Select(r => r.MessageId).ToList();

        _logger.LogDebug(
            "PowerPointGenerator produced {SlideCount} slides ({SizeBytes} bytes) in {DurationMs} ms for user {UserId}",
            slideCount,
            bytes.LongLength,
            stopwatch.ElapsedMilliseconds,
            userId);

        return new PowerPointGenerationResult
        {
            Content = bytes,
            FileName = fileName,
            SlideCount = slideCount,
            Duration = stopwatch.Elapsed,
            ConversationIds = conversationIds,
            MessageIds = messageIds
        };
    }

    private async Task<IReadOnlyList<AssistantResponseSlideModel>> BuildResponseModelsAsync(
        PowerPointExportRequestDto request,
        CultureInfo culture,
        CancellationToken cancellationToken)
    {
        var byConversation = new Dictionary<Guid, Conversation?>();
        var models = new List<AssistantResponseSlideModel>(request.Responses.Count);

        foreach (var selection in request.Responses)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!byConversation.TryGetValue(selection.ConversationId, out var conversation))
            {
                conversation = await _conversationRepository.GetByIdAsync(selection.ConversationId, cancellationToken);
                byConversation[selection.ConversationId] = conversation;
            }

            if (conversation is null) continue;

            var model = AssistantResponseParser.Parse(
                conversation,
                selection.MessageId,
                selection.CustomTitle,
                culture);
            if (model is null) continue;

            model = AssistantResponseEnricher.Enrich(model, _renderingOptions);
            model = ApplyContentFilter(model, selection.IncludeOnly);
            models.Add(model);
        }

        return models;
    }

    private static AssistantResponseSlideModel ApplyContentFilter(
        AssistantResponseSlideModel model,
        SlideContentBlock? includeOnly)
    {
        if (includeOnly is null || includeOnly.Value == SlideContentBlock.None || includeOnly.Value == SlideContentBlock.All)
            return model;

        var keepText = includeOnly.Value.HasFlag(SlideContentBlock.Text);
        var keepKpis = includeOnly.Value.HasFlag(SlideContentBlock.KpiCards);
        var keepTables = includeOnly.Value.HasFlag(SlideContentBlock.Tables);
        var keepCharts = includeOnly.Value.HasFlag(SlideContentBlock.Charts);
        var keepSources = includeOnly.Value.HasFlag(SlideContentBlock.Sources);

        return model with
        {
            MarkdownText = keepText ? model.MarkdownText : null,
            Kpis = keepKpis ? model.Kpis : Array.Empty<DashboardKpiBlock>(),
            Tables = keepTables ? model.Tables : Array.Empty<DashboardTableBlock>(),
            Charts = keepCharts ? model.Charts : Array.Empty<DashboardChartBlock>(),
            SourceTools = keepSources ? model.SourceTools : Array.Empty<string>(),
            Sections = keepText ? model.Sections : Array.Empty<MarkdownSectionModel>()
        };
    }

    private void RunBuilders(SlideBuildContext context, IReadOnlyList<AssistantResponseSlideModel> responseModels) =>
        PowerPointDeckOrchestrator.RunBuilders(context, responseModels, _renderingOptions, _logger);

    private static CultureInfo ResolveCulture(string? locale) =>
        PowerPointGenerationSupport.ResolveCulture(locale);

    private static string ResolveAuthor(PowerPointExportRequestDto request) =>
        PowerPointGenerationSupport.ResolveAuthor(request);

    private static string BuildFileName(PowerPointExportRequestDto request, PowerPointTemplate template) =>
        PowerPointGenerationSupport.BuildFileName(request, template);

    private static int CountSlidesIn(byte[] bytes) =>
        PowerPointGenerationSupport.CountSlidesIn(bytes);

    private static void CreatePresentationParts(PresentationDocument document, SlideDimensions dimensions, IPowerPointTheme theme)
    {
        var presentationPart = document.AddPresentationPart();
        presentationPart.Presentation = new P.Presentation();

        // Use SDK-generated relationship IDs (rId1, rId2, …) — the convention used by PowerPoint
        // itself when it serialises a .pptx. Custom strings like "rIdMaster1" are valid per spec
        // but trip up some strict OOXML consumers.
        var slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>();
        var slideMasterRelId = presentationPart.GetIdOfPart(slideMasterPart);
        BuildSlideMaster(slideMasterPart);

        var slideLayoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>();
        var slideLayoutRelId = slideMasterPart.GetIdOfPart(slideLayoutPart);
        BuildSlideLayout(slideLayoutPart);

        var themePart = slideMasterPart.AddNewPart<ThemePart>();
        BuildTheme(themePart, theme);

        // NotesMasterPart — mandatory when any slide has speaker notes. Without it PowerPoint
        // flags the file as corrupt even though the OpenXml SDK accepts it. The NotesMaster
        // shares the same theme as the SlideMaster per ISO/IEC 29500-1 §19.3.1.29.
        var notesMasterPart = presentationPart.AddNewPart<NotesMasterPart>();
        var notesMasterRelId = presentationPart.GetIdOfPart(notesMasterPart);
        BuildNotesMaster(notesMasterPart);
        notesMasterPart.AddPart(themePart);

        // The SlideMaster XML built above references the layout by a placeholder rId — patch it
        // now that the layout part exists and we know the SDK-generated rId.
        PatchSlideMasterLayoutRel(slideMasterPart, slideLayoutRelId);

        var presentation = presentationPart.Presentation;
        var slideSizeType = dimensions.Width == 12_192_000L
            ? P.SlideSizeValues.Screen16x9
            : P.SlideSizeValues.Screen4x3;

        // Children of <p:presentation> MUST appear in the schema-defined order:
        //   sldMasterIdLst → notesMasterIdLst → sldIdLst → sldSz → notesSz → defaultTextStyle
        // Violating this order makes OpenXml's strict validator flag the document as malformed
        // and PowerPoint Desktop refuses to open it. NotesMasterId only carries the rel-id
        // (no numeric Id, unlike SlideMasterId).
        presentation.Append(
            new P.SlideMasterIdList(new P.SlideMasterId { Id = 2147483648U, RelationshipId = slideMasterRelId }),
            new P.NotesMasterIdList(new P.NotesMasterId { Id = notesMasterRelId }),
            new P.SlideIdList(),
            new P.SlideSize { Cx = (int)dimensions.Width, Cy = (int)dimensions.Height, Type = slideSizeType },
            new P.NotesSize { Cx = 6858000, Cy = 9144000 },
            new P.DefaultTextStyle());
    }

    private static void PatchSlideMasterLayoutRel(SlideMasterPart masterPart, string layoutRelId)
    {
        var firstLayoutId = masterPart.SlideMaster?.SlideLayoutIdList?.GetFirstChild<P.SlideLayoutId>();
        if (firstLayoutId is not null)
            firstLayoutId.RelationshipId = layoutRelId;
    }

    private static void BuildSlideMaster(SlideMasterPart masterPart)
    {
        masterPart.SlideMaster = new P.SlideMaster(
            new P.CommonSlideData(
                new P.ShapeTree(
                    new P.NonVisualGroupShapeProperties(
                        new P.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                        new P.NonVisualGroupShapeDrawingProperties(),
                        new P.ApplicationNonVisualDrawingProperties()),
                    new P.GroupShapeProperties(new A.TransformGroup()))),
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
            new P.SlideLayoutIdList(new P.SlideLayoutId { Id = 2147483649U, RelationshipId = "PLACEHOLDER" }));
    }

    private static void BuildSlideLayout(SlideLayoutPart layoutPart)
    {
        layoutPart.SlideLayout = new P.SlideLayout(
            new P.CommonSlideData(
                new P.ShapeTree(
                    new P.NonVisualGroupShapeProperties(
                        new P.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                        new P.NonVisualGroupShapeDrawingProperties(),
                        new P.ApplicationNonVisualDrawingProperties()),
                    new P.GroupShapeProperties(new A.TransformGroup()))),
            new P.ColorMapOverride(new A.MasterColorMapping())) { Type = P.SlideLayoutValues.Blank };
    }

    private static void BuildTheme(ThemePart themePart, IPowerPointTheme selectedTheme)
    {
        var colors = selectedTheme.Colors;
        var fonts = selectedTheme.Fonts;
        var chart = colors.ChartSeriesHex;

        var theme = new A.Theme { Name = selectedTheme.Name };
        var themeElements = new A.ThemeElements(
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
            { Name = selectedTheme.Name },
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
            { Name = "Office" });

        theme.Append(themeElements);
        theme.Append(new A.ObjectDefaults());
        theme.Append(new A.ExtraColorSchemeList());

        themePart.Theme = theme;
    }

    /// <summary>
    /// Builds the canonical NotesMaster XML required by OOXML when any slide carries speaker
    /// notes. PowerPoint Desktop rejects (and offers to repair) .pptx files whose NotesSlides
    /// link to a non-existent master, even though the OpenXml SDK tolerates them.
    /// </summary>
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
