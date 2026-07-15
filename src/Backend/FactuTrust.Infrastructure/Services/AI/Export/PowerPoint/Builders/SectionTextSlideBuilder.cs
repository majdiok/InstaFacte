using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Domain.Constants;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;
using A = DocumentFormat.OpenXml.Drawing;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;

/// <summary>
/// Renders semantic markdown sections as individual slides with section-specific layouts.
/// </summary>
public sealed class SectionTextSlideBuilder : IResponseSlideBuilder
{
    public int Build(SlideBuildContext context, AssistantResponseSlideModel response)
    {
        if (response.Sections.Count == 0)
            return 0;

        var slidesCreated = 0;
        foreach (var section in response.Sections)
        {
            if (section.Kind == MarkdownSectionKind.KeyIndicators)
                continue;

            if (section.Kind == MarkdownSectionKind.ExecutiveSummary)
            {
                slidesCreated += new ExecutiveSummarySlideBuilder().BuildSection(context, response, section);
                continue;
            }

            slidesCreated += BuildGenericSection(context, response, section);
        }

        return slidesCreated;
    }

    internal int BuildGenericSection(
        SlideBuildContext context,
        AssistantResponseSlideModel response,
        MarkdownSectionModel section)
    {
        var content = SectionSlideHelpers.StripDuplicateHeading(section.Content, section.Title);
        if (string.IsNullOrWhiteSpace(content))
            return 0;

        var styledContent = section.Kind == MarkdownSectionKind.Risks
            ? content
            : content;

        var paragraphs = (section.Kind == MarkdownSectionKind.Risks
            ? SectionSlideHelpers.ConvertRiskSection(styledContent, context.Theme)
            : SectionSlideHelpers.ConvertWithAmountHighlight(styledContent, context.Theme)).ToList();
        if (paragraphs.Count == 0)
            return 0;

        var pages = Paginate(paragraphs);
        var created = 0;
        foreach (var (page, pageIndex) in pages.Select((p, i) => (p, i)))
        {
            BuildPage(context, response, section.Title, page, pageIndex, pages.Count);
            created++;
        }
        return created;
    }

    private static IReadOnlyList<IReadOnlyList<A.Paragraph>> Paginate(IReadOnlyList<A.Paragraph> paragraphs)
    {
        var pages = new List<List<A.Paragraph>>();
        var current = new List<A.Paragraph>();
        var charsOnPage = 0;
        var bulletsOnPage = 0;

        foreach (var paragraph in paragraphs)
        {
            var textLen = paragraph.Descendants<A.Text>().Sum(t => (t.Text ?? string.Empty).Length);
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
        return pages;
    }

    private static void BuildPage(
        SlideBuildContext context,
        AssistantResponseSlideModel response,
        string sectionTitle,
        IReadOnlyList<A.Paragraph> paragraphs,
        int pageIndex,
        int totalPages)
    {
        var notes = BuildSpeakerNotes(response, sectionTitle, pageIndex);
        var (_, tree, _) = SlideFactory.CreateSlide(context, notes);

        var title = totalPages > 1
            ? $"{sectionTitle} ({pageIndex + 1}/{totalPages})"
            : sectionTitle;
        SlideHeaderFooterDecorator.AppendHeader(context, tree, title);

        var dims = context.Dimensions;
        var cloned = paragraphs.Select(p => (A.Paragraph)p.CloneNode(true)).ToList();
        var bodyShape = OpenXmlPresentationHelpers.Shape(
            shapeId: context.NextShapeId(),
            shapeName: "SectionBody",
            offsetX: dims.BodyLeft,
            offsetY: dims.BodyTop,
            width: dims.ContentWidth,
            height: dims.BodyHeight,
            fill: null,
            paragraphs: cloned);
        tree.Append(bodyShape);
        context.AppendContentFooter(tree);
    }

    private static string BuildSpeakerNotes(
        AssistantResponseSlideModel response,
        string sectionTitle,
        int pageIndex)
    {
        var lines = new List<string>
        {
            $"Réponse exportée depuis l'Assistant IA {BrandConstants.Name}",
            $"Section : {sectionTitle}",
            $"Conversation : {response.ConversationId:D}",
            $"Message : {response.MessageId:D}",
            $"Page : {pageIndex + 1}"
        };
        if (response.SourceTools.Count > 0)
            lines.Add($"Outils utilisés : {string.Join(", ", response.SourceTools.Distinct())}");
        return string.Join('\n', lines);
    }
}
