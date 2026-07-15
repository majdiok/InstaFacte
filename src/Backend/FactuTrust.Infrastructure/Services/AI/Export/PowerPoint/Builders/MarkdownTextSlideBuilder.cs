using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Domain.Constants;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;
using A = DocumentFormat.OpenXml.Drawing;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;

/// <summary>
/// Renders the markdown body of an assistant response across one or more slides, paginating when
/// the content exceeds reasonable per-slide limits (<see cref="PowerPointDeckLimits"/>).
/// </summary>
public sealed class MarkdownTextSlideBuilder : IResponseSlideBuilder
{
    public int Build(SlideBuildContext context, AssistantResponseSlideModel response)
    {
        if (string.IsNullOrWhiteSpace(response.MarkdownText))
            return 0;

        var cleaned = SectionSlideHelpers.StripDuplicateHeading(response.MarkdownText!, response.Title);
        var paragraphs = MarkdownToOpenXmlConverter.Convert(cleaned, context.Theme).ToList();
        if (paragraphs.Count == 0)
            return 0;

        var pages = Paginate(paragraphs);
        var slidesCreated = 0;

        foreach (var (page, pageIndex) in pages.Select((p, i) => (p, i)))
        {
            BuildPageSlide(context, response, page, pageIndex, pages.Count);
            slidesCreated++;
        }

        return slidesCreated;
    }

    private static IReadOnlyList<IReadOnlyList<A.Paragraph>> Paginate(IReadOnlyList<A.Paragraph> paragraphs)
    {
        var pages = new List<List<A.Paragraph>>();
        var current = new List<A.Paragraph>();
        var charsOnPage = 0;
        var bulletsOnPage = 0;

        foreach (var paragraph in paragraphs)
        {
            var textLen = EstimateLength(paragraph);
            var isBullet = paragraph.ParagraphProperties?.GetFirstChild<A.CharacterBullet>() is not null;

            var wouldExceedChars = charsOnPage + textLen > PowerPointDeckLimits.MaxCharsPerTextSlide;
            var wouldExceedBullets = bulletsOnPage + (isBullet ? 1 : 0) > PowerPointDeckLimits.MaxBulletsPerTextSlide;

            if ((wouldExceedChars || wouldExceedBullets) && current.Count > 0)
            {
                pages.Add(current);
                current = new List<A.Paragraph>();
                charsOnPage = 0;
                bulletsOnPage = 0;
            }

            current.Add(paragraph);
            charsOnPage += textLen;
            if (isBullet) bulletsOnPage++;
        }

        if (current.Count > 0)
            pages.Add(current);

        return pages.Count == 0 ? new List<List<A.Paragraph>>() : pages;
    }

    private static int EstimateLength(A.Paragraph paragraph)
    {
        return paragraph.Descendants<A.Text>().Sum(t => (t.Text ?? string.Empty).Length);
    }

    private static void BuildPageSlide(
        SlideBuildContext context,
        AssistantResponseSlideModel response,
        IReadOnlyList<A.Paragraph> paragraphs,
        int pageIndex,
        int totalPages)
    {
        var (_, tree, _) = SlideFactory.CreateSlide(context, BuildSpeakerNotes(response, pageIndex));

        var title = totalPages > 1
            ? $"{response.Title} ({pageIndex + 1}/{totalPages})"
            : response.Title;
        SlideHeaderFooterDecorator.AppendHeader(context, tree, title);

        var dims = context.Dimensions;

        // We need to clone paragraphs because each builder owns the OpenXml tree it adds.
        var clonedParagraphs = paragraphs
            .Select(p => (A.Paragraph)p.CloneNode(true))
            .ToList();

        var bodyShape = OpenXmlPresentationHelpers.Shape(
            shapeId: context.NextShapeId(),
            shapeName: "MarkdownBody",
            offsetX: dims.BodyLeft,
            offsetY: dims.BodyTop,
            width: dims.ContentWidth,
            height: dims.BodyHeight,
            fill: null,
            paragraphs: clonedParagraphs);
        tree.Append(bodyShape);
        context.AppendContentFooter(tree);
    }

    private static string BuildSpeakerNotes(AssistantResponseSlideModel response, int pageIndex)
    {
        var lines = new List<string>
        {
            $"Réponse exportée depuis l'Assistant IA {BrandConstants.Name}",
            $"Conversation : {response.ConversationId:D}",
            $"Message : {response.MessageId:D}",
            $"Page : {pageIndex + 1}"
        };
        if (response.SourceTools.Count > 0)
        {
            lines.Add($"Outils utilisés : {string.Join(", ", response.SourceTools.Distinct())}");
        }
        return string.Join('\n', lines);
    }
}
