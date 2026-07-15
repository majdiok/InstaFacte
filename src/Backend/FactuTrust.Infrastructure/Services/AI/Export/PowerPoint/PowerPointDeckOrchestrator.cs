using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint;

/// <summary>Shared slide-building pipeline used by legacy and hybrid generators.</summary>
internal static class PowerPointDeckOrchestrator
{
    internal static void RunBuilders(
        SlideBuildContext context,
        IReadOnlyList<AssistantResponseSlideModel> responseModels,
        PowerPointRenderingOptions renderingOptions,
        ILogger logger)
    {
        if (context.Request.IncludeCoverSlide)
        {
            context.ActiveLayoutPart = context.ResolveLayout(PowerPointSlideKind.Cover);
            new CoverSlideBuilder().Build(context);
        }

        if (context.Request.IncludeAgenda)
        {
            context.ActiveLayoutPart = context.ResolveLayout(PowerPointSlideKind.Agenda);
            new AgendaSlideBuilder(responseModels).Build(context);
        }

        if (context.Request.IncludeTableOfContents)
        {
            context.ActiveLayoutPart = context.ResolveLayout(PowerPointSlideKind.TableOfContents);
            var entries = responseModels
                .Select((r, i) => new TocEntry(r.Title, BuildSummary(r), i + 1))
                .ToList();
            new TocSlideBuilder(entries).Build(context);
        }

        var sectionIndex = 1;
        foreach (var response in responseModels)
        {
            if (context.SlideCount >= PowerPointDeckLimits.MaxSlidesPerDeck)
            {
                logger.LogWarning("Reached MaxSlidesPerDeck ({Max}), remaining responses skipped.", PowerPointDeckLimits.MaxSlidesPerDeck);
                break;
            }

            context.ActiveLayoutPart = context.ResolveLayout(PowerPointSlideKind.SectionDivider);
            new SectionDividerSlideBuilder(sectionIndex++).Build(context, response);

            if (renderingOptions.SemanticSectionSlidesEnabled && response.Sections.Count > 0)
            {
                var execSections = response.Sections
                    .Where(s => s.Kind == MarkdownSectionKind.ExecutiveSummary)
                    .ToList();
                foreach (var section in execSections)
                {
                    context.ActiveLayoutPart = context.ResolveLayout(PowerPointSlideKind.TitleAndContent);
                    new ExecutiveSummarySlideBuilder().BuildSection(context, response, section);
                }

                context.ActiveLayoutPart = context.ResolveLayout(PowerPointSlideKind.TwoColumn);
                new KpiSlideBuilder().Build(context, response);
                context.ActiveLayoutPart = context.ResolveLayout(PowerPointSlideKind.TitleAndContent);
                new ChartSlideBuilder().Build(context, response);
                new TableSlideBuilder().Build(context, response);

                var otherSections = response.Sections
                    .Where(s => s.Kind != MarkdownSectionKind.ExecutiveSummary &&
                                s.Kind != MarkdownSectionKind.KeyIndicators)
                    .ToList();
                if (otherSections.Count > 0)
                {
                    var sectionBuilder = new SectionTextSlideBuilder();
                    foreach (var section in otherSections)
                    {
                        context.ActiveLayoutPart = context.ResolveLayout(PowerPointSlideKind.TitleAndContent);
                        sectionBuilder.BuildGenericSection(context, response, section);
                    }
                }
            }
            else
            {
                context.ActiveLayoutPart = context.ResolveLayout(PowerPointSlideKind.TwoColumn);
                new KpiSlideBuilder().Build(context, response);
                context.ActiveLayoutPart = context.ResolveLayout(PowerPointSlideKind.TitleAndContent);
                new TableSlideBuilder().Build(context, response);
                new ChartSlideBuilder().Build(context, response);
                new MarkdownTextSlideBuilder().Build(context, response);
            }
        }

        if (context.Request.IncludeSources)
        {
            context.ActiveLayoutPart = context.ResolveLayout(PowerPointSlideKind.Credits);
            new SourcesSlideBuilder(responseModels).Build(context);
        }

        if (context.Request.IncludeAppendix)
        {
            context.ActiveLayoutPart = context.ResolveLayout(PowerPointSlideKind.TitleAndContent);
            new AppendixSlideBuilder(responseModels).Build(context);
        }

        if (context.Theme.RequiredAttribution is not null &&
            renderingOptions.RequireAttributionSlide)
        {
            context.ActiveLayoutPart = context.ResolveLayout(PowerPointSlideKind.Credits);
            new ThemeAttributionSlideBuilder().Build(context);
        }
    }

    private static string BuildSummary(AssistantResponseSlideModel response)
    {
        var bits = new List<string>();
        if (response.Kpis.Count > 0) bits.Add($"{response.Kpis.Count} KPI");
        if (response.Tables.Count > 0) bits.Add($"{response.Tables.Count} tableau{(response.Tables.Count > 1 ? "x" : string.Empty)}");
        if (response.Charts.Count > 0) bits.Add($"{response.Charts.Count} graphique{(response.Charts.Count > 1 ? "s" : string.Empty)}");
        if (!string.IsNullOrWhiteSpace(response.MarkdownText)) bits.Add("Texte");
        return string.Join(", ", bits);
    }
}
