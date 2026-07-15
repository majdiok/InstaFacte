using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;
using A = DocumentFormat.OpenXml.Drawing;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;

/// <summary>
/// Renders an optional appendix slide containing the raw transcription of selected responses in
/// compact form. Useful for archiving the exact LLM output alongside the visual deck.
/// </summary>
public sealed class AppendixSlideBuilder : ISlideBuilder
{
    private readonly IReadOnlyList<AssistantResponseSlideModel> _responses;

    public AppendixSlideBuilder(IReadOnlyList<AssistantResponseSlideModel> responses)
    {
        _responses = responses;
    }

    public int Build(SlideBuildContext context)
    {
        if (_responses.Count == 0)
            return 0;

        var transcript = BuildTranscript();
        var (_, tree, _) = SlideFactory.CreateSlide(context, speakerNotes: transcript);

        SlideHeaderFooterDecorator.AppendHeader(context, tree, "Annexe — Transcript brut");

        var theme = context.Theme;
        var dims = context.Dimensions;

        var paragraphs = new List<A.Paragraph>
        {
            OpenXmlPresentationHelpers.Paragraph(
                text: "Le transcript intégral des réponses sélectionnées est accessible dans les notes du présentateur.",
                runProperties: OpenXmlPresentationHelpers.RunProperties(
                    fontSizeHundredths: 1300,
                    colorHex: theme.Colors.OnSurfaceHex,
                    fontFamily: theme.Fonts.BodyFamily),
                paragraphProperties: OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Left)),
            OpenXmlPresentationHelpers.EmptyParagraph()
        };

        var maxSummaryItems = 6;
        var index = 1;
        foreach (var response in _responses.Take(maxSummaryItems))
        {
            paragraphs.Add(OpenXmlPresentationHelpers.Paragraph(
                text: $"• {index:D2}. {response.Title}",
                runProperties: OpenXmlPresentationHelpers.RunProperties(
                    fontSizeHundredths: 1100,
                    colorHex: theme.Colors.MutedHex,
                    fontFamily: theme.Fonts.BodyFamily),
                paragraphProperties: OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Left)));
            index++;
        }

        if (_responses.Count > maxSummaryItems)
        {
            paragraphs.Add(OpenXmlPresentationHelpers.Paragraph(
                text: $"… et {_responses.Count - maxSummaryItems} autres réponses (voir notes)",
                runProperties: OpenXmlPresentationHelpers.RunProperties(
                    fontSizeHundredths: 1100,
                    colorHex: theme.Colors.MutedHex,
                    italic: true,
                    fontFamily: theme.Fonts.BodyFamily),
                paragraphProperties: OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Left)));
        }

        var bodyShape = OpenXmlPresentationHelpers.Shape(
            shapeId: context.NextShapeId(),
            shapeName: "AppendixBody",
            offsetX: dims.BodyLeft,
            offsetY: dims.BodyTop,
            width: dims.ContentWidth,
            height: dims.BodyHeight,
            fill: null,
            paragraphs: paragraphs);
        tree.Append(bodyShape);

        return 1;
    }

    private string BuildTranscript()
    {
        var sb = new System.Text.StringBuilder();
        var index = 1;
        foreach (var response in _responses)
        {
            sb.AppendLine($"### {index:D2}. {response.Title}");
            sb.AppendLine($"Conversation : {response.ConversationId:D}");
            sb.AppendLine($"Message : {response.MessageId:D}");
            sb.AppendLine();
            sb.AppendLine(response.MarkdownText?.Trim() ?? "(aucun contenu textuel)");
            sb.AppendLine();
            sb.AppendLine();
            index++;
        }
        return sb.ToString();
    }
}
