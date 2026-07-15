using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;

/// <summary>
/// Creates new slide parts attached to the current presentation. Encapsulates the OpenXml
/// boilerplate so builders can focus on shapes only.
/// </summary>
internal static class SlideFactory
{
    /// <summary>
    /// Creates a new <see cref="SlidePart"/>, attaches it to the presentation's slide id list and
    /// returns the empty <see cref="P.ShapeTree"/> on which the caller appends its shapes.
    /// </summary>
    public static (SlidePart slidePart, P.ShapeTree shapeTree, P.Slide slide) CreateSlide(SlideBuildContext context, string? speakerNotes = null)
    {
        var presentationPart = context.Document.PresentationPart
            ?? throw new InvalidOperationException("PresentationPart not initialised.");

        // Let the SDK generate the relationship id (rId1, rId2, ...) — matches the convention
        // used by PowerPoint Desktop itself when saving a .pptx and avoids custom rId strings.
        var slidePart = presentationPart.AddNewPart<SlidePart>();
        slidePart.AddPart(context.ActiveLayoutPart);

        // Build a minimal but valid Slide XML.
        var slide = new P.Slide(
            new P.CommonSlideData(
                new P.ShapeTree(
                    new P.NonVisualGroupShapeProperties(
                        new P.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                        new P.NonVisualGroupShapeDrawingProperties(),
                        new P.ApplicationNonVisualDrawingProperties()),
                    new P.GroupShapeProperties(new A.TransformGroup()))),
            new P.ColorMapOverride(new A.MasterColorMapping()));

        slidePart.Slide = slide;

        var shapeTree = slide.CommonSlideData!.ShapeTree!;

        if (!string.IsNullOrWhiteSpace(speakerNotes) && context.Request.IncludeSpeakerNotes)
        {
            AttachSpeakerNotes(slidePart, context, speakerNotes);
        }

        AppendSlideToPresentation(presentationPart, slidePart);
        context.IncrementSlideCount();

        return (slidePart, shapeTree, slide);
    }

    private static void AppendSlideToPresentation(PresentationPart presentationPart, SlidePart slidePart)
    {
        var presentation = presentationPart.Presentation
            ?? throw new InvalidOperationException("Presentation root missing.");
        var slideIdList = presentation.SlideIdList
            ?? throw new InvalidOperationException("SlideIdList not initialised.");

        var existingIds = slideIdList.ChildElements
            .OfType<P.SlideId>()
            .Select(s => s.Id!.Value)
            .DefaultIfEmpty(255U)
            .Max();

        var newId = Math.Max(existingIds + 1U, 256U);

        var relId = presentationPart.GetIdOfPart(slidePart);
        slideIdList.Append(new P.SlideId
        {
            Id = newId,
            RelationshipId = relId
        });
    }

    private static void AttachSpeakerNotes(SlidePart slidePart, SlideBuildContext context, string notes)
    {
        // The NotesMaster MUST exist before any NotesSlide can be attached — without that link
        // PowerPoint flags the file as corrupt and offers to repair it.
        var notesMasterPart = context.Document.PresentationPart?.NotesMasterPart
            ?? throw new InvalidOperationException(
                "NotesMasterPart is required when speaker notes are enabled. " +
                "Ensure CreatePresentationParts initialises the NotesMasterPart before building slides.");

        var notesSlidePart = slidePart.AddNewPart<NotesSlidePart>();
        // Two relationships are mandatory per ISO/IEC 29500-1 §13.3.5 / §13.3.6:
        //   1. NotesSlide → NotesMaster (template inheritance).
        //   2. NotesSlide → Slide (back-reference to the owning slide).
        notesSlidePart.AddPart(notesMasterPart);
        notesSlidePart.AddPart(slidePart);

        notesSlidePart.NotesSlide = new P.NotesSlide(
            new P.CommonSlideData(
                new P.ShapeTree(
                    new P.NonVisualGroupShapeProperties(
                        new P.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                        new P.NonVisualGroupShapeDrawingProperties(),
                        new P.ApplicationNonVisualDrawingProperties()),
                    new P.GroupShapeProperties(new A.TransformGroup()),
                    BuildNotesBodyPlaceholderShape(context, notes))),
            new P.ColorMapOverride(new A.MasterColorMapping()));
    }

    private static P.Shape BuildNotesBodyPlaceholderShape(SlideBuildContext context, string notes)
    {
        var dims = context.Dimensions;
        var theme = context.Theme;
        var paragraphs = notes.Split('\n', StringSplitOptions.None)
            .Select(line => OpenXmlPresentationHelpers.Paragraph(
                text: line,
                runProperties: OpenXmlPresentationHelpers.RunProperties(
                    fontSizeHundredths: 1200,
                    colorHex: theme.Colors.OnSurfaceHex,
                    fontFamily: theme.Fonts.BodyFamily),
                paragraphProperties: OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Left)))
            .Cast<A.Paragraph>()
            .ToList();

        if (paragraphs.Count == 0)
            paragraphs.Add(OpenXmlPresentationHelpers.EmptyParagraph());

        // Declare the placeholder type/index so PowerPoint can inherit styles from the
        // matching placeholder in the NotesMaster (type=body, idx=2).
        var placeholder = new P.PlaceholderShape { Type = P.PlaceholderValues.Body, Index = 2U };

        var shape = new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = context.NextShapeId(), Name = "NotesPlaceholder" },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties(placeholder)),
            new P.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = 457_200L, Y = 457_200L },
                    new A.Extents { Cx = dims.Width - 914_400L, Cy = dims.Height - 914_400L }),
                new A.PresetGeometry { Preset = A.ShapeTypeValues.Rectangle }));

        var bodyProperties = new A.BodyProperties
        {
            Anchor = A.TextAnchoringTypeValues.Top,
            Wrap = A.TextWrappingValues.Square
        };
        var textBody = new P.TextBody(bodyProperties, new A.ListStyle());
        foreach (var paragraph in paragraphs)
            textBody.Append(paragraph);
        shape.Append(textBody);

        return shape;
    }
}
